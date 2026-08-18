using System.Text.Json;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// The Razorpay leg: create an order, verify the checkout handler, and accept the webhook.
///
/// An order writes a <see cref="PaymentStatus.Pending"/> payment row straight away, keyed by
/// the gateway order id. That row is what the webhook and the browser callback both settle,
/// so a capture can arrive twice, out of order, or only over the webhook, and the invoice is
/// credited exactly once either way.
/// </summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly ILogger<PaymentsController> _log;

    public PaymentsController(
        GymDbContext db, IPaymentGateway gateway, INotificationDispatcher notifier,
        IClock clock, ILogger<PaymentsController> log)
    {
        _db = db;
        _gateway = gateway;
        _notifier = notifier;
        _clock = clock;
        _log = log;
    }

    /// <summary>Tells the client whether checkout can be offered and with which public key.</summary>
    [HttpGet("gateway")]
    [Authorize]
    public IActionResult Gateway() => Ok(new
    {
        provider = _gateway.Name,
        isLive = _gateway.IsLive,
        keyId = _gateway.PublicKeyId,
        notice = _gateway.IsLive
            ? null
            : "Razorpay keys are not configured, so checkout runs in sandbox simulation and no money moves."
    });

    [HttpPost("razorpay/order")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateOrderResponse>> CreateOrder(CreateOrderRequest request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.Include(i => i.Member)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null) return NotFound();

        if (invoice.AmountDue <= 0)
            return Problem("Nothing is outstanding on that invoice.", statusCode: StatusCodes.Status400BadRequest);

        var amount = request.Amount is { } requested && requested > 0
            ? Math.Min(requested, invoice.AmountDue)
            : invoice.AmountDue;

        GatewayOrder order;
        try
        {
            order = await _gateway.CreateOrderAsync(
                amount,
                receipt: invoice.InvoiceNumber.Replace('/', '-'),
                notes: new Dictionary<string, string>
                {
                    ["invoiceId"] = invoice.Id.ToString(),
                    ["invoiceNumber"] = invoice.InvoiceNumber,
                    ["memberCode"] = invoice.Member.MemberCode
                },
                ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

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
                ReceivedBy = User.Identity?.Name,
                Notes = order.IsSimulated ? "Simulated order — no Razorpay key configured." : null
            });
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new CreateOrderResponse
        {
            OrderId = order.OrderId,
            KeyId = order.KeyId,
            AmountInr = order.AmountInr,
            Currency = order.Currency,
            Receipt = order.Receipt,
            IsSimulated = order.IsSimulated,
            InvoiceNumber = invoice.InvoiceNumber,
            MemberName = invoice.Member.FullName,
            MemberEmail = invoice.Member.Email,
            MemberPhone = invoice.Member.Phone,
            Notice = order.IsSimulated
                ? "Sandbox simulation: the payment will be recorded and stamped as simulated."
                : null
        });
    }

    /// <summary>
    /// Settles the order from the browser handler. The webhook is the source of truth in
    /// production, but the desk should not have to wait for it before printing a receipt.
    /// </summary>
    [HttpPost("razorpay/verify")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Verify(VerifyPaymentRequest request, CancellationToken ct)
    {
        var verification = _gateway.VerifyCheckoutSignature(
            request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature ?? string.Empty);

        if (!verification.IsValid)
        {
            _log.LogWarning("Rejected Razorpay checkout callback for order {OrderId}: {Reason}",
                request.RazorpayOrderId, verification.Reason);
            return Problem($"Signature check failed: {verification.Reason}", statusCode: StatusCodes.Status400BadRequest);
        }

        var settled = await SettleAsync(
            request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature,
            payloadJson: null, ct);

        return settled is null
            ? NotFound(new ProblemDetails { Title = "Unknown order", Detail = "No invoice is waiting on that order id." })
            : Ok(new
            {
                paymentId = settled.Id,
                status = settled.Status.ToString(),
                invoiceStatus = settled.Invoice.Status.ToString(),
                amountDue = settled.Invoice.AmountDue,
                simulated = verification.Reason
            });
    }

    /// <summary>
    /// Razorpay's server-to-server callback. Anonymous by necessity and authenticated by the
    /// HMAC over the raw body — which is why the body is read verbatim rather than model-bound.
    /// </summary>
    [HttpPost("razorpay/webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
        var verification = _gateway.VerifyWebhookSignature(raw, signature);
        if (!verification.IsValid)
        {
            _log.LogWarning("Rejected Razorpay webhook: {Reason}", verification.Reason);
            return Unauthorized();
        }

        string? eventName = null, orderId = null, paymentId = null;
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            eventName = root.TryGetProperty("event", out var e) ? e.GetString() : null;

            if (root.TryGetProperty("payload", out var payload) &&
                payload.TryGetProperty("payment", out var payment) &&
                payment.TryGetProperty("entity", out var entity))
            {
                orderId = entity.TryGetProperty("order_id", out var o) ? o.GetString() : null;
                paymentId = entity.TryGetProperty("id", out var p) ? p.GetString() : null;
            }
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "Razorpay webhook body was not valid JSON");
            return BadRequest();
        }

        _log.LogInformation("Razorpay webhook {Event} for order {OrderId}", eventName, orderId);

        // Anything that is not a capture is acknowledged and ignored — Razorpay retries on
        // a non-2xx, and there is nothing to retry for an event we do not act on.
        if (eventName is not ("payment.captured" or "order.paid") || orderId is null)
            return Ok(new { received = true, handled = false });

        var settled = await SettleAsync(orderId, paymentId, null, raw, ct);
        return Ok(new { received = true, handled = settled is not null });
    }

    /// <summary>
    /// Flips the pending row to captured and credits the invoice. Safe to call more than once:
    /// an already-captured row short-circuits, which is what makes webhook retries harmless.
    /// </summary>
    private async Task<Payment?> SettleAsync(
        string orderId, string? paymentId, string? signature, string? payloadJson, CancellationToken ct)
    {
        var payment = await _db.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.GatewayOrderId == orderId, ct);

        if (payment is null) return null;
        if (payment.Status == PaymentStatus.Captured) return payment;

        payment.Status = PaymentStatus.Captured;
        payment.PaidAtUtc = _clock.UtcNow;
        payment.GatewayPaymentId = paymentId ?? payment.GatewayPaymentId;
        payment.GatewaySignature = signature ?? payment.GatewaySignature;
        payment.GatewayPayloadJson = payloadJson ?? payment.GatewayPayloadJson;

        var invoice = payment.Invoice;
        invoice.AmountPaid = GstCalculator.Round(invoice.AmountPaid + payment.Amount);
        invoice.AmountDue = GstCalculator.Round(invoice.GrandTotal - invoice.AmountPaid);
        InvoiceService.ApplyStatus(invoice, _clock.Today);

        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Razorpay order {OrderId} captured ₹{Amount} against {Invoice}",
            orderId, payment.Amount, invoice.InvoiceNumber);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = invoice.MemberId,
            Kind = NotificationKind.PaymentReceived,
            Title = "Payment received",
            Body = $"₹{payment.Amount:N0} against {invoice.InvoiceNumber}. Thank you.",
            ActionUrl = $"/portal/billing/{invoice.Id}",
            Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
        }, ct);

        return payment;
    }
}
