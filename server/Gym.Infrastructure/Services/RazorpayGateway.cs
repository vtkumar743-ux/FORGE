using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gym.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

public class RazorpayOptions
{
    public const string SectionName = "Razorpay";
    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    /// <summary>
    /// Set by startup. Outside Development an unconfigured gateway refuses to transact rather
    /// than silently simulating — a fake "paid" in production would corrupt the audit trail.
    /// </summary>
    public bool AllowSimulator { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(KeySecret);
}

/// <summary>
/// Razorpay orders and signature verification. With sandbox keys present this talks to the
/// real API. With none, and only in Development, it stands in a deterministic simulator so
/// the whole lead → invoice → payment demo runs end to end before the client's keys arrive;
/// every simulated payment is stamped as such on the Payment row.
/// </summary>
public class RazorpayGateway : IPaymentGateway
{
    private const string OrdersEndpoint = "https://api.razorpay.com/v1/orders";

    private readonly RazorpayOptions _options;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<RazorpayGateway> _log;

    public RazorpayGateway(RazorpayOptions options, IHttpClientFactory http, ILogger<RazorpayGateway> log)
    {
        _options = options;
        _http = http;
        _log = log;
    }

    public string Name => "razorpay";
    public bool IsLive => _options.IsConfigured;
    public string? PublicKeyId => _options.IsConfigured ? _options.KeyId : null;

    public async Task<GatewayOrder> CreateOrderAsync(
        decimal amountInr, string receipt, IReadOnlyDictionary<string, string>? notes = null,
        CancellationToken ct = default)
    {
        // Razorpay works in paise; anything else silently bills a hundredth of the amount.
        var paise = (long)decimal.Round(amountInr * 100m, 0, MidpointRounding.AwayFromZero);

        if (!_options.IsConfigured)
        {
            if (!_options.AllowSimulator)
                throw new InvalidOperationException(
                    "Razorpay:KeyId and Razorpay:KeySecret are not configured. " +
                    "Set them before taking online payments.");

            var simulated = $"order_SIM{Hash($"{receipt}:{paise}")[..14].ToUpperInvariant()}";
            _log.LogWarning("Razorpay is unconfigured — issuing simulated order {OrderId} for {Amount}",
                simulated, amountInr);
            return new GatewayOrder(simulated, null, amountInr, "INR", receipt, IsSimulated: true);
        }

        var payload = new Dictionary<string, object?>
        {
            ["amount"] = paise,
            ["currency"] = "INR",
            ["receipt"] = receipt,
            ["payment_capture"] = 1
        };
        if (notes is { Count: > 0 }) payload["notes"] = notes;

        using var client = _http.CreateClient("razorpay");
        using var request = new HttpRequestMessage(HttpMethod.Post, OrdersEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.KeyId}:{_options.KeySecret}")));

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _log.LogError("Razorpay order creation failed ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Razorpay refused the order ({(int)response.StatusCode}).");
        }

        using var document = JsonDocument.Parse(body);
        var id = document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Razorpay returned an order without an id.");

        return new GatewayOrder(id, _options.KeyId, amountInr, "INR", receipt, IsSimulated: false);
    }

    public GatewayVerification VerifyCheckoutSignature(string orderId, string paymentId, string signature)
    {
        if (!_options.IsConfigured)
        {
            return _options.AllowSimulator
                ? new GatewayVerification(true, "simulated — no Razorpay key configured")
                : GatewayVerification.Fail("Razorpay is not configured.");
        }

        var expected = HmacHex($"{orderId}|{paymentId}", _options.KeySecret);
        return FixedTimeEquals(expected, signature)
            ? GatewayVerification.Ok
            : GatewayVerification.Fail("Checkout signature does not match.");
    }

    public GatewayVerification VerifyWebhookSignature(string rawBody, string? signature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            // An unsigned webhook is an open endpoint for crediting invoices — never outside dev.
            return _options.AllowSimulator
                ? new GatewayVerification(true, "unverified — Razorpay:WebhookSecret is not set")
                : GatewayVerification.Fail("Razorpay:WebhookSecret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(signature))
            return GatewayVerification.Fail("Missing X-Razorpay-Signature header.");

        var expected = HmacHex(rawBody, _options.WebhookSecret);
        return FixedTimeEquals(expected, signature)
            ? GatewayVerification.Ok
            : GatewayVerification.Fail("Webhook signature does not match.");
    }

    private static string HmacHex(string message, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)))
            .ToLower(CultureInfo.InvariantCulture);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Constant-time compare — a length-or-content leak here is a forgery oracle.</summary>
    private static bool FixedTimeEquals(string expected, string actual)
    {
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(actual.Trim());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
