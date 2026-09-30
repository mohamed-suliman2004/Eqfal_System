using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Eqfal.API.Services
{
    public class EzonePayGatewayService : IPaymentGatewayService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EzonePayGatewayService> _logger;

        private readonly string? _baseUrl;
        private readonly string? _apiKey;
        private readonly string? _webhookSecret;

        public EzonePayGatewayService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<EzonePayGatewayService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _baseUrl = _configuration["EzonePay:BaseUrl"]?.TrimEnd('/');
            _apiKey = _configuration["EzonePay:ApiKey"];
            _webhookSecret = _configuration["EzonePay:WebhookSecret"];
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<PaymentInvoiceResult?> CreateInvoiceAsync(
            decimal amount,
            string currency,
            string description,
            IReadOnlyDictionary<string, string>? invoiceParams,
            CancellationToken ct = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("[EzonePay] Service is not configured with BaseUrl or ApiKey");
                return null;
            }

            try
            {
                string orderReference = invoiceParams != null && invoiceParams.TryGetValue("subscriptionPaymentId", out var refId) 
                    ? refId 
                    : Guid.NewGuid().ToString();

                string fullName = invoiceParams != null && invoiceParams.TryGetValue("customerName", out var name) 
                    ? name 
                    : "زبون إقفال";

                string phone = invoiceParams != null && invoiceParams.TryGetValue("customerPhone", out var ph) 
                    ? ph 
                    : "0910000000";

                var nameParts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                string firstName = nameParts.Length > 0 ? nameParts[0] : "زبون";
                string lastName = nameParts.Length > 1 ? nameParts[1] : "-";

                var payload = new
                {
                    title = description,
                    orderReference = orderReference,
                    isUniqueOrderReference = true,
                    amount = amount,
                    currency = 1, // 1 = LYD
                    note = description,
                    maxUsageCount = 1,
                    customer = new
                    {
                        firstName = firstName,
                        lastName = lastName,
                        phoneNumber = phone
                    }
                };

                string jsonContent = JsonSerializer.Serialize(payload);
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/payment-link/new")
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                _logger.LogInformation("[EzonePay] Creating payment link for OrderRef: {OrderRef}, Amount: {Amount}", orderReference, amount);

                using var response = await _httpClient.SendAsync(request, ct);
                string responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[EzonePay] Create link failed with code {Code}: {Body}", response.StatusCode, responseBody);
                    return null;
                }

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // بعض ردود إيزون باي تكون في root أو داخل كائن data
                JsonElement targetElement = root.TryGetProperty("data", out var dataEl) ? dataEl : root;

                string? paymentId = null;
                if (targetElement.TryGetProperty("id", out var idProp))
                {
                    paymentId = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : idProp.GetString();
                }
                else if (targetElement.TryGetProperty("paymentLinkId", out var plProp))
                {
                    paymentId = plProp.ValueKind == JsonValueKind.Number ? plProp.GetInt64().ToString() : plProp.GetString();
                }

                string? paymentUrl = null;
                if (targetElement.TryGetProperty("link", out var linkProp)) paymentUrl = linkProp.GetString();
                else if (targetElement.TryGetProperty("paymentUrl", out var urlProp)) paymentUrl = urlProp.GetString();
                else if (targetElement.TryGetProperty("url", out var uProp)) paymentUrl = uProp.GetString();

                if (string.IsNullOrEmpty(paymentId) || string.IsNullOrEmpty(paymentUrl))
                {
                    _logger.LogError("[EzonePay] Could not extract paymentId or paymentUrl from response: {Body}", responseBody);
                    return null;
                }

                return new PaymentInvoiceResult(paymentId, paymentUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EzonePay] Exception while creating invoice link");
                return null;
            }
        }

        public async Task<PaymentStatusResult?> GetPaymentAsync(string paymentId, CancellationToken ct = default)
        {
            if (!IsConfigured) return null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/payment-link/{paymentId}");
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await _httpClient.SendAsync(request, ct);
                string responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[EzonePay] GetPayment failed with code {Code}: {Body}", response.StatusCode, responseBody);
                    return null;
                }

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                JsonElement target = root.TryGetProperty("data", out var dataEl) ? dataEl : root;

                int currentUsageCount = 0;
                if (target.TryGetProperty("currentUsageCount", out var usageProp) && usageProp.TryGetInt32(out var uVal))
                    currentUsageCount = uVal;

                bool isEnabled = true;
                if (target.TryGetProperty("isEnabled", out var enabledProp))
                    isEnabled = enabledProp.GetBoolean();

                decimal amount = 0;
                if (target.TryGetProperty("amount", out var amountProp) && amountProp.TryGetDecimal(out var amtVal))
                    amount = amtVal;

                // استنتاج الحالة المعتمد في المواصفات:
                // currentUsageCount > 0 => Settled
                // !isEnabled => Cancelled
                // غير ذلك => Pending
                string status = "Pending";
                if (currentUsageCount > 0)
                    status = "Settled";
                else if (!isEnabled)
                    status = "Cancelled";

                return new PaymentStatusResult(paymentId, status, amount, "LYD");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EzonePay] Exception while getting payment status for ID: {PaymentId}", paymentId);
                return null;
            }
        }

        public bool VerifyWebhookSignature(string rawBody, string? signatureHeader)
        {
            if (string.IsNullOrEmpty(_webhookSecret) || string.IsNullOrEmpty(signatureHeader))
            {
                _logger.LogWarning("[EzonePay Webhook] Missing secret or signature header");
                return false;
            }

            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(_webhookSecret);
                var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

                using var hmac = new HMACSHA256(keyBytes);
                var hashBytes = hmac.ComputeHash(bodyBytes);
                var computedSignature = Convert.ToBase64String(hashBytes);

                // مقارنة آمنة في الزمن الثابت (Constant Time)
                var computedBytes = Encoding.UTF8.GetBytes(computedSignature);
                var receivedBytes = Encoding.UTF8.GetBytes(signatureHeader);

                return CryptographicOperations.FixedTimeEquals(computedBytes, receivedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EzonePay Webhook] Error calculating or verifying signature");
                return false;
            }
        }
    }
}
