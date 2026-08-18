using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

/// <summary>One billable line before tax has been worked out.</summary>
public record DraftLine(
    string Description,
    decimal UnitPrice,
    int Quantity = 1,
    decimal DiscountAmount = 0m,
    decimal GstRatePercent = 5m,
    string? SacOrHsnCode = "999723",
    int? PlanId = null,
    int? ProductId = null,
    /// <summary>True when UnitPrice already contains the tax — the norm for memberships.</summary>
    bool PriceIncludesGst = true);

/// <summary>
/// Issues GST invoices and records payments against them. Two invariants matter here and
/// are enforced in one place rather than at every call site: invoice numbers are gap-free
/// and unique inside an Indian financial year, and an invoice's status is always derived
/// from the payments that exist rather than set by hand.
/// </summary>
public class InvoiceService
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<InvoiceService> _log;

    public InvoiceService(GymDbContext db, IClock clock, ILogger<InvoiceService> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    /// <summary>
    /// Builds an unsaved invoice with its lines, tax split and totals. The caller adds it to
    /// the context so a sale can commit the subscription and its invoice in one transaction.
    /// </summary>
    public async Task<Invoice> BuildAsync(
        Member member,
        Branch branch,
        IReadOnlyCollection<DraftLine> lines,
        DateOnly issuedOn,
        int dueInDays = 0,
        int? subscriptionId = null,
        int? orderId = null,
        string? notes = null,
        string? customerGstin = null,
        CancellationToken ct = default)
    {
        if (lines.Count == 0)
            throw new InvalidOperationException("An invoice needs at least one line.");

        var supplierGstin = branch.Gstin;
        var interState = GstCalculator.IsInterState(supplierGstin, customerGstin);

        var invoice = new Invoice
        {
            InvoiceNumber = await NextNumberAsync(issuedOn, ct),
            MemberId = member.Id,
            BranchId = branch.Id,
            SubscriptionId = subscriptionId,
            OrderId = orderId,
            IssuedOn = issuedOn,
            DueOn = issuedOn.AddDays(Math.Max(0, dueInDays)),
            Status = InvoiceStatus.Issued,
            SupplierGstin = supplierGstin,
            PlaceOfSupply = GstCalculator.PlaceOfSupply(branch.State),
            CustomerGstin = customerGstin,
            Notes = notes
        };

        foreach (var draft in lines)
        {
            var lineGross = draft.UnitPrice * draft.Quantity - draft.DiscountAmount;
            var split = draft.PriceIncludesGst
                ? GstCalculator.FromGross(lineGross, draft.GstRatePercent, interState)
                : GstCalculator.FromNet(lineGross, draft.GstRatePercent, interState);

            invoice.Lines.Add(new InvoiceLine
            {
                Description = draft.Description,
                SacOrHsnCode = draft.SacOrHsnCode,
                Quantity = draft.Quantity,
                UnitPrice = GstCalculator.Round(draft.UnitPrice),
                DiscountAmount = GstCalculator.Round(draft.DiscountAmount),
                TaxableValue = split.TaxableValue,
                GstRatePercent = draft.GstRatePercent,
                CgstAmount = split.Cgst,
                SgstAmount = split.Sgst,
                IgstAmount = split.Igst,
                LineTotal = split.Gross,
                PlanId = draft.PlanId,
                ProductId = draft.ProductId
            });
        }

        Total(invoice);
        return invoice;
    }

    /// <summary>Recomputes header totals from the lines. Safe to call after any line edit.</summary>
    public static void Total(Invoice invoice)
    {
        invoice.TaxableValue = GstCalculator.Round(invoice.Lines.Sum(l => l.TaxableValue));
        invoice.DiscountTotal = GstCalculator.Round(invoice.Lines.Sum(l => l.DiscountAmount));
        invoice.SubTotal = GstCalculator.Round(invoice.TaxableValue + invoice.DiscountTotal);
        invoice.CgstAmount = GstCalculator.Round(invoice.Lines.Sum(l => l.CgstAmount));
        invoice.SgstAmount = GstCalculator.Round(invoice.Lines.Sum(l => l.SgstAmount));
        invoice.IgstAmount = GstCalculator.Round(invoice.Lines.Sum(l => l.IgstAmount));

        var exact = invoice.TaxableValue + invoice.CgstAmount + invoice.SgstAmount + invoice.IgstAmount;
        var (grand, roundOff) = GstCalculator.ApplyRoundOff(exact);
        invoice.RoundOff = roundOff;
        invoice.GrandTotal = grand;
        invoice.AmountDue = GstCalculator.Round(grand - invoice.AmountPaid);
    }

    /// <summary>
    /// Appends a payment and re-derives the invoice status. Payments are never edited or
    /// deleted — a correction is a refund row, which is what makes the trail non-repudiable.
    /// </summary>
    public async Task<Payment> RecordPaymentAsync(
        Invoice invoice,
        decimal amount,
        PaymentMode mode,
        string? receivedBy,
        string? idempotencyKey = null,
        string? gatewayName = null,
        string? gatewayOrderId = null,
        string? gatewayPaymentId = null,
        string? gatewaySignature = null,
        string? gatewayPayloadJson = null,
        string? chequeNumber = null,
        string? bankReference = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (amount <= 0) throw new InvalidOperationException("A payment must be greater than zero.");

        if (idempotencyKey is not null)
        {
            var existing = await _db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, ct);
            // A retried webhook or a double-clicked "Record payment" must not credit twice.
            if (existing is not null) return existing;
        }

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
            MemberId = invoice.MemberId,
            BranchId = invoice.BranchId,
            Amount = GstCalculator.Round(amount),
            Mode = mode,
            Status = PaymentStatus.Captured,
            PaidAtUtc = _clock.UtcNow,
            ReceivedBy = receivedBy,
            IdempotencyKey = idempotencyKey,
            GatewayName = gatewayName,
            GatewayOrderId = gatewayOrderId,
            GatewayPaymentId = gatewayPaymentId,
            GatewaySignature = gatewaySignature,
            GatewayPayloadJson = gatewayPayloadJson,
            ChequeNumber = chequeNumber,
            BankReference = bankReference,
            Notes = notes
        };

        _db.Payments.Add(payment);
        invoice.AmountPaid = GstCalculator.Round(invoice.AmountPaid + payment.Amount);
        invoice.AmountDue = GstCalculator.Round(invoice.GrandTotal - invoice.AmountPaid);
        ApplyStatus(invoice, _clock.Today);

        _log.LogInformation("Payment of {Amount} ({Mode}) recorded against {Invoice}",
            payment.Amount, mode, invoice.InvoiceNumber);

        return payment;
    }

    /// <summary>Status is a function of money received and the due date — never set by hand.</summary>
    public static void ApplyStatus(Invoice invoice, DateOnly today)
    {
        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Refunded) return;

        invoice.Status = invoice.AmountDue <= 0.01m
            ? InvoiceStatus.Paid
            : invoice.AmountPaid > 0
                ? (invoice.DueOn < today ? InvoiceStatus.Overdue : InvoiceStatus.PartiallyPaid)
                : (invoice.DueOn < today ? InvoiceStatus.Overdue : InvoiceStatus.Issued);
    }

    /// <summary>
    /// FRG/26-27/000412 — gap-free within the Indian financial year (April–March). The
    /// sequence is read from the highest number already issued in that year rather than a
    /// counter table, so a restored database keeps numbering where it left off.
    /// </summary>
    public async Task<string> NextNumberAsync(DateOnly issuedOn, CancellationToken ct = default)
    {
        var startYear = issuedOn.Month >= 4 ? issuedOn.Year : issuedOn.Year - 1;
        var prefix = $"FRG/{startYear % 100:D2}-{(startYear + 1) % 100:D2}/";

        var highest = await _db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (highest is not null && int.TryParse(highest[prefix.Length..], out var parsed))
            next = parsed + 1;

        // Rows added in this unit of work are not in the database yet but must not collide.
        var pending = _db.ChangeTracker.Entries<Invoice>()
            .Where(e => e.State == EntityState.Added && e.Entity.InvoiceNumber.StartsWith(prefix))
            .Select(e => int.TryParse(e.Entity.InvoiceNumber[prefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{Math.Max(next, pending + 1):D6}";
    }
}
