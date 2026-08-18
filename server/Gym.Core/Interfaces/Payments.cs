namespace Gym.Core.Interfaces;

/// <summary>
/// An order handed to the checkout widget. <paramref name="IsSimulated"/> is true when no
/// Razorpay key is configured — the desk can still complete a demo collection, and the
/// receipt says plainly that no money moved.
/// </summary>
public record GatewayOrder(
    string OrderId,
    string? KeyId,
    decimal AmountInr,
    string Currency,
    string Receipt,
    bool IsSimulated);

public record GatewayVerification(bool IsValid, string? Reason)
{
    public static readonly GatewayVerification Ok = new(true, null);
    public static GatewayVerification Fail(string reason) => new(false, reason);
}

/// <summary>
/// Razorpay in v1. The seam exists because the signature checks and the order call are the
/// only gateway-shaped things in the domain — everything else is an Invoice and a Payment row.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }
    /// <summary>False when keys are absent and the sandbox simulator is standing in.</summary>
    bool IsLive { get; }
    string? PublicKeyId { get; }

    Task<GatewayOrder> CreateOrderAsync(
        decimal amountInr, string receipt, IReadOnlyDictionary<string, string>? notes = null,
        CancellationToken ct = default);

    /// <summary>Verifies the checkout handler's razorpay_signature (HMAC of "orderId|paymentId").</summary>
    GatewayVerification VerifyCheckoutSignature(string orderId, string paymentId, string signature);

    /// <summary>Verifies the X-Razorpay-Signature header against the raw webhook body.</summary>
    GatewayVerification VerifyWebhookSignature(string rawBody, string? signature);
}
