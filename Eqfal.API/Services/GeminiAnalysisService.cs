using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eqfal.API.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Eqfal.API.Services
{
    /// <summary>
    /// AI-powered message analysis using Gemini Flash API.
    /// Used as a fallback when the regex-based analyzer returns incomplete results ("مسودة").
    /// </summary>
    public class GeminiAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string _model;
        private readonly ILogger<GeminiAnalysisService> _logger;

        public GeminiAnalysisService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeminiAnalysisService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = configuration["Gemini:ApiKey"];
            _model = configuration["Gemini:Model"] ?? "gemini-3.6-flash";
            _logger = logger;
        }

        /// <summary>
        /// Analyzes a financial message using Gemini AI and returns structured data.
        /// Returns null if the API call fails or the response is unparsable.
        /// </summary>
        public async Task<GeminiAnalysisResult?> AnalyzeAsync(string messageText, bool isOutgoing, IEnumerable<DynamicKeyword>? customKeywords = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("[GeminiAI] API key is not configured. Skipping AI analysis.");
                return null;
            }

            try
            {
                string direction = isOutgoing ? "صادرة (أنا أرسلتها)" : "واردة (أنا استلمتها)";

                string customKwText = "";
                if (customKeywords != null && customKeywords.Any())
                {
                    var groups = customKeywords
                        .Where(k => !string.IsNullOrWhiteSpace(k.Word) && !string.IsNullOrWhiteSpace(k.Type))
                        .GroupBy(k => k.Type.Trim())
                        .Select(g => $"- تصنيف \"{g.Key}\": {string.Join("، ", g.Select(k => k.Word.Trim()).Distinct())}");

                    if (groups.Any())
                    {
                        customKwText = $@"

الكلمات المفتاحية الخاصة بهذا المستخدم (أعطها أولوية قصوى إذا وُجدت في الرسالة):
{string.Join("\n", groups)}";
                    }
                }

                string prompt = $@"أنت خبير ومحلل مالي ذكي متخصص في استخراج وتدقيق بيانات الحوالات والتحويلات المالية لمكاتب الصرافة والشركات في ليبيا والعالم العربي.

مهمتك: تحليل الرسالة التالية بدقة فائقة وتحديد ما إذا كانت معاملة مالية حقيقية أم محادثة عامة/خربشة، واستخراج الأركان المالية دون أي تخمين أو تلفيق.{customKwText}

الرسالة ({direction}):
---
{messageText}
---

القواعد الصارمة:
1. التحقق من طبيعة الرسالة (is_financial):
   - تكون true فقط إذا كانت الرسالة تتضمن طلباً أو تأكيداً لعملية مالية حقيقية (تسليم، استلام، تحويل، إيداع، قبض، إلخ) بمبلغ واضح.
   - تكون false إذا كانت الرسالة: محادثة عادية، سوالف، تهنئة، نكتة، حديث عن دفعات تخرج أو سنوات دراسية (مثل ""دفعة 2007"")، خربشة كيبورد (مثل ""طؤط..."")، أو مجرد أرقام هواتف/أكواد. وفي هذه الحالة تكون جميع الحقول الأخرى null أو فارغة.

2. حقل المبلغ (amount):
   - رقم المبلغ المالي الصافي فقط (مثال: 7660). فواصل الآلاف مثل 22.000 تُحوّل إلى 22000.
   - إياك واعتبار أرقام السنوات والدفعات (مثل 2007)، أرقام الهواتف، أو أكواد الطرود (مثل كود 444 أو U-539) كمبالغ مالية.
   - إذا لم يوجد مبلغ مالي قطعي، أرجع null.

3. حقل العملة (currency):
   - رمز العملة بـ 3 أحرف (USD, LYD, EUR, TRY, SAR, AED, EGP, GBP, TND).
   - $ أو دولار -> USD. دينار أو د.ل -> LYD. يورو أو € -> EUR. ليرة -> TRY.
   - تنبيه قطعي: إذا لم تُذكر أي عملة أو رمز عملة إطلاقاً في الرسالة، أرجع null. إياك وافتراض عملة من عندك!

4. حقل التصنيف (category):
   - اختر حصراً إما ""تسليم"" أو ""استلام"" وفق اتجاه المعاملة:
     * إذا كانت الرسالة صادرة (أنا أرسلتها):
       - أفعال الاستلام والقبض والشحن (استلام/استلمت/قبض/وصلني): تُصنف كـ ""استلام"".
       - أفعال التسليم والصرف والدفع والتحويل (تسليم/سلمت/صرف/دفعت/حولت): تُصنف كـ ""تسليم"".
       - الإيداع: يُصنف كـ ""تسليم""، والسحب: يُصنف كـ ""استلام"".
     * إذا كانت الرسالة واردة (أنا استلمتها من الطرف الآخر):
       - إذا قال الطرف الآخر أفعال استلام (استلام/استلمت/قبضت): تُصنف عندي كـ ""تسليم"" (هو استلم فأن سلمته).
       - إذا قال الطرف الآخر أفعال تسليم (تسليم/سلمت/دفعت/حولت): تُصنف عندي كـ ""استلام"" (هو سلمني فأن استلمت).
       - الإيداع: يُصنف كـ ""استلام""، والسحب: يُصنف كـ ""تسليم"".
     * إذا لم يتضح نوع المعاملة صراحة، فالافتراضي: الصادرة ""تسليم"" والواردة ""استلام"".

5. حقل الطرف (party):
   - اسم الشخص أو الشركة أو المكتب الطرف في المعاملة (مثل: ""شركة الواثقون""، ""مكتب الريادة""، ""محمد الشريف"").
   - يُمنع منعاً باتاً وضع: أرقام هواتف، أكواد، عناوين شوارع، خربشة حروف، أو كلمات المحادثات اليومية (مثل: ""نشرحلهم""، ""صفوها""، ""الحمدلله"").
   - إذا لم يُذكر طرف صراحة بالاسم، أرجع """".

المطلوب: أرجع النتيجة بصيغة JSON فقط بدون أي شرح إضافي:
{{
  ""is_financial"": true أو false,
  ""amount"": رقم أو null,
  ""currency"": ""رمز العملة"" أو null,
  ""category"": ""تسليم"" أو ""استلام"" أو """",
  ""party"": ""اسم الطرف"" أو """"
}}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        maxOutputTokens = 256,
                        responseMimeType = "application/json"
                    }
                };

                string jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("[GeminiAI] API returned {StatusCode}: {Error}", response.StatusCode, errorBody.Length > 200 ? errorBody[..200] : errorBody);
                    return null;
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[GeminiAI] Raw response length: {Length}", responseJson.Length);

                // Parse Gemini response structure
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    _logger.LogWarning("[GeminiAI] No candidates in response");
                    return null;
                }

                var firstCandidate = candidates[0];
                if (!firstCandidate.TryGetProperty("content", out var contentEl) ||
                    !contentEl.TryGetProperty("parts", out var parts) ||
                    parts.GetArrayLength() == 0)
                {
                    _logger.LogWarning("[GeminiAI] No content/parts in response");
                    return null;
                }

                string aiText = parts[0].GetProperty("text").GetString() ?? "";
                _logger.LogInformation("[GeminiAI] AI text: {Text}", aiText.Length > 300 ? aiText[..300] : aiText);

                // Clean JSON (remove markdown code blocks if present)
                aiText = aiText.Trim();
                if (aiText.StartsWith("```json")) aiText = aiText[7..];
                if (aiText.StartsWith("```")) aiText = aiText[3..];
                if (aiText.EndsWith("```")) aiText = aiText[..^3];
                aiText = aiText.Trim();

                // Parse the AI's JSON response
                using var aiDoc = JsonDocument.Parse(aiText);
                var aiRoot = aiDoc.RootElement;

                var result = new GeminiAnalysisResult();

                if (aiRoot.TryGetProperty("is_financial", out var finEl))
                {
                    if (finEl.ValueKind == JsonValueKind.False)
                        result.IsFinancial = false;
                    else if (finEl.ValueKind == JsonValueKind.True)
                        result.IsFinancial = true;
                }

                if (!result.IsFinancial)
                {
                    _logger.LogInformation("[GeminiAI] AI determined message is NON-FINANCIAL");
                    return result;
                }

                if (aiRoot.TryGetProperty("amount", out var amountEl))
                {
                    if (amountEl.ValueKind == JsonValueKind.Number)
                        result.Amount = amountEl.GetDecimal();
                    else if (amountEl.ValueKind == JsonValueKind.String && decimal.TryParse(amountEl.GetString(), out decimal parsedAmt))
                        result.Amount = parsedAmt;
                }

                if (aiRoot.TryGetProperty("currency", out var currEl))
                    result.Currency = currEl.GetString()?.Trim().ToUpperInvariant() ?? "";

                if (aiRoot.TryGetProperty("category", out var catEl))
                {
                    string rawCat = catEl.GetString()?.Trim() ?? "";
                    if (rawCat.Contains("استلام"))
                        result.Category = "استلام";
                    else if (rawCat.Contains("تسليم"))
                        result.Category = "تسليم";
                    else
                        result.Category = rawCat;
                }

                if (aiRoot.TryGetProperty("party", out var partyEl))
                    result.Party = partyEl.GetString()?.Trim() ?? "";

                _logger.LogInformation("[GeminiAI] Parsed: IsFin={IsFin}, Amount={Amount}, Currency={Currency}, Category={Category}, Party={Party}",
                    result.IsFinancial, result.Amount, result.Currency, result.Category, result.Party);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GeminiAI] Failed to analyze message");
                return null;
            }
        }

        public async Task<(bool success, string details, GeminiAnalysisResult? result)> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return (false, "ApiKey is null or whitespace in configuration", null);
            }

            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = "أرجع JSON فقط: {\"amount\": 100, \"currency\": \"USD\", \"category\": \"استلام\", \"party\": \"شركة الواثقون\"}" }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        responseMimeType = "application/json"
                    }
                };

                string jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, content);

                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", null);
                }

                return (true, responseBody, new GeminiAnalysisResult { Amount = 100, Currency = "USD", Category = "استلام", Party = "شركة الواثقون" });
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.GetType().Name}: {ex.Message} -> Inner: {ex.InnerException?.Message}", null);
            }
        }
    }

    public class GeminiAnalysisResult
    {
        public bool IsFinancial { get; set; } = true;
        public decimal? Amount { get; set; }
        public string Currency { get; set; } = "";
        public string Category { get; set; } = "";
        public string Party { get; set; } = "";
    }
}
