using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoTo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LoTo.Infrastructure.Services;

public class MoMoPaymentService : IPaymentService
{
    private readonly string _partnerCode;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _apiUrl;
    private readonly string _ipnUrl;
    private readonly string _redirectUrl;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MoMoPaymentService> _logger;

    private static readonly Dictionary<string, long> PlanPrices = new()
    {
        ["yearly"] = 50000,
    };

    public MoMoPaymentService(IConfiguration config, HttpClient httpClient, ILogger<MoMoPaymentService> logger)
    {
        _partnerCode = config["MoMo:PartnerCode"] ?? "MOMOBKUN20180529";
        _accessKey = config["MoMo:AccessKey"] ?? "klm05TvNBzhg7h7j";
        _secretKey = config["MoMo:SecretKey"] ?? "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa";
        _apiUrl = config["MoMo:ApiUrl"] ?? "https://test-payment.momo.vn/v2/gateway/api/create";
        _ipnUrl = config["MoMo:IpnUrl"] ?? "https://localhost/api/payment/momo-ipn";
        _redirectUrl = config["MoMo:RedirectUrl"] ?? "http://localhost:5173/premium/result";
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PaymentResult> CreatePaymentAsync(Guid userId, string planType, CancellationToken ct = default)
    {
        if (!PlanPrices.TryGetValue(planType, out var amount))
            throw new ArgumentException("Plan type khong hop le");

        var orderId = $"LOTO_{userId:N}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var requestId = $"REQ_{Guid.NewGuid():N}";
        var orderInfo = $"Nang cap Premium Lo To Online - Goi {planType}";
        var extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { userId = userId.ToString(), planType })));

        var rawSignature = $"accessKey={_accessKey}&amount={amount}&extraData={extraData}" +
            $"&ipnUrl={_ipnUrl}&orderId={orderId}&orderInfo={orderInfo}" +
            $"&partnerCode={_partnerCode}&redirectUrl={_redirectUrl}" +
            $"&requestId={requestId}&requestType=captureWallet";

        var signature = SignHmacSha256(rawSignature, _secretKey);

        var body = new
        {
            partnerCode = _partnerCode,
            accessKey = _accessKey,
            requestId,
            amount,
            orderId,
            orderInfo,
            redirectUrl = _redirectUrl,
            ipnUrl = _ipnUrl,
            extraData,
            requestType = "captureWallet",
            signature,
            lang = "vi",
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_apiUrl, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            _logger.LogInformation("MoMo response: {Response}", responseBody);

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var resultCode = result.GetProperty("resultCode").GetInt32();

            if (resultCode != 0)
            {
                var message = result.GetProperty("message").GetString();
                throw new InvalidOperationException($"MoMo error: {message} (code: {resultCode})");
            }

            var payUrl = result.GetProperty("payUrl").GetString() ?? "";
            var qrCodeUrl = result.TryGetProperty("qrCodeUrl", out var qr) ? qr.GetString() : null;

            return new PaymentResult(orderId, payUrl, qrCodeUrl, amount, DateTime.UtcNow.AddMinutes(15));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "MoMo API request failed");
            throw new InvalidOperationException("Khong the ket noi MoMo");
        }
    }

    public Task<bool> VerifyCallbackAsync(string signature, string rawBody, CancellationToken ct = default)
    {
        var computed = SignHmacSha256(rawBody, _secretKey);
        return Task.FromResult(string.Equals(computed, signature, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<QueryPaymentResult> QueryPaymentAsync(string orderId, CancellationToken ct = default)
    {
        var requestId = $"REQ_{Guid.NewGuid():N}";
        var rawSignature = $"accessKey={_accessKey}&orderId={orderId}&partnerCode={_partnerCode}&requestId={requestId}";
        var signature = SignHmacSha256(rawSignature, _secretKey);

        var queryUrl = _apiUrl.Replace("/create", "/query");
        var body = new
        {
            partnerCode = _partnerCode,
            requestId,
            orderId,
            signature,
            lang = "vi",
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(queryUrl, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            _logger.LogInformation("MoMo query response for {OrderId}: {Response}", orderId, responseBody);

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var resultCode = result.GetProperty("resultCode").GetInt32();
            var message = result.GetProperty("message").GetString() ?? "";
            var transId = result.TryGetProperty("transId", out var tid) ? tid.ToString() : null;

            return new QueryPaymentResult(resultCode, message, transId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "MoMo query API failed for order {OrderId}", orderId);
            throw new InvalidOperationException("Khong the ket noi MoMo");
        }
    }

    private static string SignHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
