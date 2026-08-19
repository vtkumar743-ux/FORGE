using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

/// <summary>
/// Opening a gateway order against an invoice — the one place it happens, whether the desk
/// starts it from the admin panel or the member starts it from the portal.
///
/// Creating an order writes a <see cref="PaymentStatus.Pending"/> row keyed by the gateway
/// order id. That row is what the browser callback and the webhook both settle, so a capture
/// can arrive twice, out of order, or only over the webhook and the invoice is credited once.
/// </summary>
public class PaymentOrderService
{
    private readonly GymDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IClock _clock;
    private readonly ILogger<PaymentOrderService> _log;

    public PaymentOrderService(
        GymDbContext db, IPaymentGateway gateway, IClock clock, ILogger<PaymentOrderService> log)
    {
        _db = db;
        _gateway = gateway;
        _clock = clock;
        _log = log;
    }

    public record OpenedOrder(GatewayOrder Order, Invoice Invoice, decimal Amount);

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when the invoice has nothing outstanding
    /// or the gateway refuses; callers turn that into the right status code for their surface.
    /// </summary>
    public async Task<OpenedOrder> OpenAsync(
        int invoiceId, decimal? requestedAmount, string? actor, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Member)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException("No such invoice.");

        if (invoice.AmountDue <= 0)
            throw new InvalidOperationException("Nothing is outstanding on that invoice.");

        var amount = requestedAmount is { } requested && requested > 0
            ? Math.Min(requested, invoice.AmountDue)
            : invoice.AmountDue;

        var order = await _gateway.CreateOrderAsync(
            amount,
            receipt: invoice.InvoiceNumber.Replace('/', '-'),
            notes: new Dictionary<string, string>
            {
                ["invoiceId"] = invoice.Id.ToString(),
                ["invoiceNumber"] = invoice.InvoiceNumber,
                ["memberCode"] = invoice.Member.MemberCode
            },
            ct);

        var key = $"rzp-{order.OrderId}";
        var pending = await _db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == key, ct);
        if (pending is null)
        {
            _db.Payments.Add(new Payment
            {
                InvoiceId = invoice.Id,
                MemberId = invoice.MemberId,
                BranchId = invoice.BranchId,
                Amount = amount,
                Mode = PaymentMode.RazorpayLink,
                Status = PaymentStatus.Pending,
                PaidAtUtc = _clock.UtcNow,
                GatewayName = order.IsSimulated ? "razorpay-sandbox-simulator" : "razorpay",
                GatewayOrderId = order.OrderId,
                IdempotencyKey = key,
                ReceivedBy = actor,
                Notes = order.IsSimulated ? "Simulated order — no Razorpay key configured." : null
            });
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Opened {Gateway} order {OrderId} for ₹{Amount} against {Invoice}",
                order.IsSimulated ? "simulated" : "razorpay", order.OrderId, amount, invoice.InvoiceNumber);
        }

        return new OpenedOrder(order, invoice, amount);
    }
}
