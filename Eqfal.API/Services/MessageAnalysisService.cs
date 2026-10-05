using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eqfal.API.Data;
using Eqfal.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eqfal.API.Services
{
    public class MessageAnalysisService : IMessageAnalysisService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MessageAnalysisService> _logger;
        private readonly GeminiAnalysisService _gemini;

        public MessageAnalysisService(AppDbContext context, ILogger<MessageAnalysisService> logger, GeminiAnalysisService gemini)
        {
            _context = context;
            _logger = logger;
            _gemini = gemini;
        }

        public async Task<MessageAnalysisResult> AnalyzeMessageAsync(int userId, string text, string defaultParty = "", bool isOutgoing = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return NonFinancialResult("");
            }

            string rawNormalized = NormalizeArabicNumerals(text.Trim());

            // Separate glued words and numbers: "استلام200" -> "استلام 200", "200دينار" -> "200 دينار"
            rawNormalized = Regex.Replace(rawNormalized, @"([\p{L}])(\d+)", "$1 $2");
            rawNormalized = Regex.Replace(rawNormalized, @"(\d+)([\p{L}])", "$1 $2");

            // 1. Strip URLs entirely from text before any analysis
            string textWithoutUrls = StripUrls(rawNormalized);

            // If the message is completely a URL or contains no meaningful content after removing URLs -> Reject immediately
            if (string.IsNullOrWhiteSpace(textWithoutUrls))
            {
                _logger.LogInformation("[Analysis] Ignored: Message is purely a URL/link: {Raw}", text.Length > 80 ? text[..80] : text);
                return NonFinancialResult("رابط إلكتروني - غير مالي");
            }

            List<DynamicKeyword> userKeywords = new();
            try
            {
                userKeywords = await _context.DynamicKeywords
                    .Where(k => k.UserId == userId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load user keywords for user {UserId}", userId);
            }

            // 2. Extract Category
            bool categoryFromKeyword = false;
            bool isConflicted = false;
            string? category = ExtractCategoryByUserKeywords(textWithoutUrls, userKeywords, isOutgoing, out categoryFromKeyword, out isConflicted);

            bool hasExplicitCategory = false;
            if (isConflicted)
            {
                // Strict rule: if keyword exists in both receipt and delivery, force Category to empty and Status to draft
                category = "";
                hasExplicitCategory = false;
            }
            else if (string.IsNullOrEmpty(category))
            {
                category = ExtractExplicitCategory(textWithoutUrls, isOutgoing);
                if (!string.IsNullOrEmpty(category))
                {
                    hasExplicitCategory = true;
                }
            }
            else
            {
                hasExplicitCategory = true;
            }

            // 3. Extract Currency
            string? explicitCurrency = ExtractExplicitCurrency(textWithoutUrls, userKeywords);
            bool hasExplicitCurrency = !string.IsNullOrEmpty(explicitCurrency);
            string currency = explicitCurrency ?? "LYD";

            // 4. Extract Amount with strict financial pattern matching
            bool foundWithCurrency = false;
            bool foundWithFinancialVerb = false;
            decimal? amount = ExtractSmartAmount(textWithoutUrls, userKeywords, out foundWithCurrency, out foundWithFinancialVerb);

            // 5. Extract Party
            string? party = ExtractParty(textWithoutUrls, userKeywords);
            bool partyExtractedFromText = !string.IsNullOrWhiteSpace(party);
            if (string.IsNullOrWhiteSpace(party) && !string.IsNullOrWhiteSpace(defaultParty))
            {
                party = defaultParty.Trim();
            }

            bool hasValidAmount = amount.HasValue && amount.Value > 0;

            // 6. Strict Financial Validation (The 4 Pillars Rule):
            // A message is ONLY considered financial if:
            // - It has a valid extracted amount > 0
            // AND
            // - At least one explicit financial anchor exists IN THE TEXT:
            //   (a) Explicit currency mentioned in text (or found directly with amount e.g. 500$, 200 دينار), OR
            //   (b) Explicit financial category/verb (تسليم/استلام/قبض/صرف/إيداع/سحب/شيك/حوالة...)
            // Standalone numbers or numbers next to random words without currency or financial action are 100% NON-FINANCIAL.
            bool isFinancial = false;
            if (hasValidAmount)
            {
                if (hasExplicitCurrency || foundWithCurrency)
                {
                    isFinancial = true;
                }
                else if (hasExplicitCategory || categoryFromKeyword || foundWithFinancialVerb)
                {
                    isFinancial = true;
                }
            }

            if (!isFinancial)
            {
                _logger.LogInformation("[Analysis] Ignored non-financial: Amount={Amount}, HasCurr={HasCurr}, HasCat={HasCat}, Text={Text}",
                    amount, hasExplicitCurrency, hasExplicitCategory, textWithoutUrls.Length > 80 ? textWithoutUrls[..80] : textWithoutUrls);

                return NonFinancialResult(textWithoutUrls);
            }

            // If category wasn't explicit and not conflicted, infer from outgoing/incoming
            if (string.IsNullOrEmpty(category) && !isConflicted)
            {
                category = isOutgoing ? "تسليم" : "استلام";
            }

            // 4 pillars: Amount, Currency, Category, Party
            // Note: hasParty checks if ANY party is set (including fallback from contact name/phone).
            // But for "complete" status, only count the party if it was extracted from the message text itself,
            // NOT from the default party fallback (contact name / phone number).
            bool hasParty = !string.IsNullOrWhiteSpace(party);
            bool isComplete = !isConflicted && hasValidAmount && hasExplicitCurrency && hasExplicitCategory && partyExtractedFromText;
            string status = isComplete ? "مكتمل" : "مسودة";

            // ═══════════════════════════════════════════════════════════════
            // HYBRID AI FALLBACK: If regex returned "مسودة" (incomplete),
            // call Gemini AI to fill in the missing fields (unless conflicted by user keywords).
            // ═══════════════════════════════════════════════════════════════
            if (status == "مسودة" && isFinancial && !isConflicted)
            {
                try
                {
                    _logger.LogInformation("[Hybrid] Regex returned مسودة. Calling Gemini AI for fallback with user keywords...");
                    var aiResult = await _gemini.AnalyzeAsync(textWithoutUrls, isOutgoing, userKeywords);

                    if (aiResult != null)
                    {
                        if (!aiResult.IsFinancial)
                        {
                            _logger.LogInformation("[Hybrid] Gemini AI confirmed message is NON-FINANCIAL. Ignoring.");
                            return NonFinancialResult(textWithoutUrls);
                        }

                        // Fill ONLY missing fields from AI — never override what Regex already found confidently
                        if (!hasValidAmount && aiResult.Amount.HasValue && aiResult.Amount.Value > 0)
                        {
                            amount = aiResult.Amount;
                            hasValidAmount = true;
                            _logger.LogInformation("[Hybrid] AI filled Amount: {Amount}", amount);
                        }

                        if (!hasExplicitCurrency && !string.IsNullOrWhiteSpace(aiResult.Currency))
                        {
                            currency = aiResult.Currency.ToUpper();
                            hasExplicitCurrency = true;
                            _logger.LogInformation("[Hybrid] AI filled Currency: {Currency}", currency);
                        }

                        if (!hasExplicitCategory && !categoryFromKeyword && !string.IsNullOrWhiteSpace(aiResult.Category))
                        {
                            category = aiResult.Category;
                            hasExplicitCategory = true;
                            _logger.LogInformation("[Hybrid] AI filled Category: {Category}", category);
                        }

                        if (!partyExtractedFromText && !string.IsNullOrWhiteSpace(aiResult.Party))
                        {
                            party = aiResult.Party;
                            partyExtractedFromText = true;
                            _logger.LogInformation("[Hybrid] AI filled Party: {Party}", party);
                        }

                        // Re-evaluate completeness after AI fill
                        isComplete = hasValidAmount && hasExplicitCurrency && hasExplicitCategory && partyExtractedFromText;
                        status = isComplete ? "مكتمل" : "مسودة";
                        _logger.LogInformation("[Hybrid] After AI fill: Status={Status}", status);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Hybrid] AI fallback failed, using regex-only result");
                }
            }

            _logger.LogInformation("[Analysis Result] User={UserId}, IsOutgoing={IsOutgoing}, Amount={Amount}, Currency={Currency}, Category={Category}, Party={Party}, Status={Status}",
                userId, isOutgoing, amount, currency, category, party, status);

            string finalNotes = textWithoutUrls.Length > 300 ? textWithoutUrls.Substring(0, 300) : textWithoutUrls;
            if (isConflicted)
            {
                finalNotes = "⚠️ تعارض في الكلمات المفتاحية (الكلمة مضافة في الاستلام والتسليم معاً) — بانتظار تحديدك للتصنيف\n" + finalNotes;
                if (finalNotes.Length > 350) finalNotes = finalNotes.Substring(0, 350);
            }

            return new MessageAnalysisResult
            {
                Amount = amount,
                Currency = currency,
                Category = category ?? "",
                Party = party ?? "",
                Notes = finalNotes,
                Status = status,
                CategoryFromKeyword = categoryFromKeyword,
                IsConflicted = isConflicted
            };
        }

        private static MessageAnalysisResult NonFinancialResult(string notes)
        {
            return new MessageAnalysisResult
            {
                Amount = null,
                Currency = "",
                Category = "",
                Party = "",
                Notes = notes,
                Status = "غير مالي",
                CategoryFromKeyword = false
            };
        }

        private static string StripUrls(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            // Remove full URL strings (http, https, www, domain.tld/...)
            string cleaned = Regex.Replace(text, @"(?:https?:\/\/|www\.)[^\s]+|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\/[^\s]*", "", RegexOptions.IgnoreCase);
            // Remove trailing or standalone URL query artifacts
            cleaned = Regex.Replace(cleaned, @"[?&][a-zA-Z0-9_]+=[^\s]*", "", RegexOptions.IgnoreCase);
            // Clean repeated delimiters
            cleaned = Regex.Replace(cleaned, @"[=\-_*~`]{3,}", " ");
            return cleaned.Trim();
        }

        private static string NormalizeArabicNumerals(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (c >= '\u0660' && c <= '\u0669') sb.Append((char)('0' + (c - '\u0660')));
                else if (c >= '\u06F0' && c <= '\u06F9') sb.Append((char)('0' + (c - '\u06F0')));
                else if (c == '\u0640') continue; // Tatweel
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static decimal? ExtractSmartAmount(string text, List<DynamicKeyword>? userKeywords, out bool foundWithCurrency, out bool foundWithFinancialVerb)
        {
            foundWithCurrency = false;
            foundWithFinancialVerb = false;

            if (string.IsNullOrWhiteSpace(text)) return null;

            // Sanitize text by removing date formats, time formats, phone numbers, codes, tracking/shipment codes, and addresses
            string sanitized = Regex.Replace(text, @"\b\d{1,4}[/-]\d{1,2}[/-]\d{1,4}\b", " "); // Dates
            sanitized = Regex.Replace(sanitized, @"\b\d{1,2}:\d{2}(?::\d{2})?(?:\s*(?:ص|م|am|pm))?\b", " ", RegexOptions.IgnoreCase); // Times
            sanitized = Regex.Replace(sanitized, @"\b(?:\+?218|0)?[1-9]\d{7,9}\b", " "); // Phone numbers
            sanitized = Regex.Replace(sanitized, @"(?:\b|^)(?:رمز|كود|code|otp|pin|ref)\s*[:#-]?\s*[\w\d]+(?:\s+[\w\d]+)?\b", " ", RegexOptions.IgnoreCase); // Verification codes like كود 444 M
            sanitized = Regex.Replace(sanitized, @"\b[a-zA-Z]+[-_]?\d+\b", " "); // Code identifiers like U-539, REF-102 etc.
            sanitized = Regex.Replace(sanitized, @"(?:\b|^)(?:اشاري|إشاري|اشارة|إشارة|بوليصة|بوليصه|تتبع|tracking|awb|waybill)\s*[:#-]?\s*[\w\d]+", " ", RegexOptions.IgnoreCase); // Tracking codes
            sanitized = Regex.Replace(sanitized, @"(?:\b|^)(?:العنوان|عنوان|address)\s*[:=-]?\s*[^\r\n]+", " ", RegexOptions.IgnoreCase); // Explicit address line
            sanitized = Regex.Replace(sanitized, @"(?:\b|^).*?(?:No:\s*\d+|kat\s*\d+|Fatih|istanbul|Turkey|mah\.|sokak|cadde).*?$", " ", RegexOptions.Multiline | RegexOptions.IgnoreCase); // Foreign addresses / postal codes

            // CRITICAL FILTER: Educational batches, graduation years, birth years, academic years
            // e.g. "دفعة 2007", "دفعه 2007", "مواليد 2000", "سنة 2024", "عام 2023", "دورة 2019"
            sanitized = Regex.Replace(sanitized, @"(?:\b|^)(?:دفعة|دفعه|دفعتي|مواليد|سنة|سنه|عام|دورة|دوره|فصل|كلاس|تخرج|شهادة|تاريخ|بتاريخ)\s*(?:19\d{2}|20\d{2})\b", " ", RegexOptions.IgnoreCase);

            // Pattern 1: Amount directly followed or preceded by Currency (Highest Priority)
            // e.g. "500 دينار", "22.000$", "3540$$", "100$", "$100", "250 د.ل", "1000 دولار", "50 ألف", "5000 ين", "1000 byn"
            var currSuffixPattern = @"(?:\b|^)(\d{1,3}(?:[.,\s]\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)\s*(دينار|د\.ل|دل|د\.ل\.|دولار|دولارات|\$+|usd|يورو|€+|eur|ليرة|ليره|try|tl|ريال|sar|درهم|aed|جنيه|egp|باوند|gbp|ين|يوان|¥|cny|rmb|روبل|₽|rub|byn|ر\.ب|فرنك|chf|روبية|₹|inr|تيذر|usdt|ألف|الف|مليون)(?:\b|\s|$|[^\w])";
            var match1 = Regex.Match(sanitized, currSuffixPattern, RegexOptions.IgnoreCase);
            if (match1.Success)
            {
                decimal? val = ParseDecimalValue(match1.Groups[1].Value);
                if (val.HasValue && val.Value > 0)
                {
                    string unit = match1.Groups[2].Value.ToLower();
                    if (unit == "ألف" || unit == "الف") val *= 1000;
                    else if (unit == "مليون") val *= 1000000;

                    foundWithCurrency = true;
                    return val;
                }
            }

            var currPrefixPattern = @"(?:\$+|€+|¥+|£+|₽+|₹+|USD|EUR|LYD|TRY|SAR|AED|GBP|CNY|BYN|RUB|KWD|QAR|دينار|د\.ل|دولار|ين|يوان|روبل)\s*(\d{1,3}(?:[.,\s]\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)";
            var matchPrefix = Regex.Match(sanitized, currPrefixPattern, RegexOptions.IgnoreCase);
            if (matchPrefix.Success)
            {
                decimal? val = ParseDecimalValue(matchPrefix.Groups[1].Value);
                if (val.HasValue && val.Value > 0)
                {
                    foundWithCurrency = true;
                    return val;
                }
            }

            // Pattern 2: Amount directly preceded by a Financial Verb/Keyword (Very High Priority)
            // e.g. "تسليم 500", "القيمة: 22.000", "القيمه : $7660", "سلمت 200", "حولت 1000", "قبض 350", "ايداع 400", "سحب 700", "مبلغ 3000"
            // Note: "دفعة" excluded here so casual educational batches "دفعة 2007" are never treated as financial verbs!
            var verbPrefixPattern = @"(?:\b|^)(?:ال?تسليم|ال?استلام|سلم|سلمت|سلملي|سلموا|تسلم|تسلملي|يسلم|نسلم|تستلم|يستلم|نستلم|قبض|قبضت|صرف|صرفت|دفع|دفعت|ادفع|تحويل|حولت|حولنا|حول|إيداع|ايداع|أودعت|اودعت|حطيت|سحب|سحبت|اسحب|شحن|وصل|وصلني|وصلتني|وصلتنا|جاني|قسط|شيك|حوالة|حواله|ال?مبلغ|ال?قيمة|ال?قيمه|رصيد|باقي|واصل)\s*[:=-]?\s*(?:\$+|€+|USD|EUR|LYD|TRY|SAR|AED|دينار|د\.ل)?\s*(\d{1,3}(?:[.,\s]\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)";
            var match2 = Regex.Match(sanitized, verbPrefixPattern, RegexOptions.IgnoreCase);
            if (match2.Success)
            {
                decimal? val = ParseDecimalValue(match2.Groups[1].Value);
                if (val.HasValue && val.Value > 0)
                {
                    foundWithFinancialVerb = true;
                    return val;
                }
            }

            // Pattern 2.b: Custom User Category Keywords as Verbs Preceding Amount
            if (userKeywords != null && userKeywords.Any())
            {
                var customWords = userKeywords
                    .Where(k => !string.IsNullOrWhiteSpace(k.Word))
                    .Select(k => Regex.Escape(k.Word.Trim()))
                    .Distinct();

                if (customWords.Any())
                {
                    string customVerbPrefix = $@"(?:\b|^)(?:{string.Join("|", customWords)})\s*[:=-]?\s*(?:\$+|€+|USD|EUR|LYD|TRY|SAR|AED|دينار|د\.ل)?\s*(\d{{1,3}}(?:[.,\s]\d{{3}})+(?:\.\d+)?|\d+(?:\.\d+)?)";
                    var matchCustomPrefix = Regex.Match(sanitized, customVerbPrefix, RegexOptions.IgnoreCase);
                    if (matchCustomPrefix.Success)
                    {
                        decimal? val = ParseDecimalValue(matchCustomPrefix.Groups[1].Value);
                        if (val.HasValue && val.Value > 0)
                        {
                            foundWithFinancialVerb = true;
                            return val;
                        }
                    }
                }
            }

            // Pattern 3: Amount directly followed by a Financial Verb/Keyword
            // e.g. "500 تسليم", "1000 تحويل", "350 قبض", "400 ايداع"
            var verbSuffixPattern = @"(?:\b|^)(\d{1,3}(?:[.,\s]\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)\s*(?:ال?تسليم|ال?استلام|سلم|تسلم|تسلملي|يسلم|تستلم|يستلم|قبض|صرف|دفع|تحويل|إيداع|ايداع|سحب|شحن|واصل|قسط)(?:\b|$)";
            var match3 = Regex.Match(sanitized, verbSuffixPattern, RegexOptions.IgnoreCase);
            if (match3.Success)
            {
                decimal? val = ParseDecimalValue(match3.Groups[1].Value);
                if (val.HasValue && val.Value > 0)
                {
                    foundWithFinancialVerb = true;
                    return val;
                }
            }

            // Pattern 3.b: Amount followed by Custom User Category Keywords
            if (userKeywords != null && userKeywords.Any())
            {
                var customWords = userKeywords
                    .Where(k => !string.IsNullOrWhiteSpace(k.Word))
                    .Select(k => Regex.Escape(k.Word.Trim()))
                    .Distinct();

                if (customWords.Any())
                {
                    string customVerbSuffix = $@"(\d{{1,3}}(?:[.,\s]\d{{3}})+(?:\.\d+)?|\d+(?:\.\d+)?)\s*(?:{string.Join("|", customWords)})(?:\b|$)";
                    var matchCustomSuffix = Regex.Match(sanitized, customVerbSuffix, RegexOptions.IgnoreCase);
                    if (matchCustomSuffix.Success)
                    {
                        decimal? val = ParseDecimalValue(matchCustomSuffix.Groups[1].Value);
                        if (val.HasValue && val.Value > 0)
                        {
                            foundWithFinancialVerb = true;
                            return val;
                        }
                    }
                }
            }

            // Pattern 4: Standalone number search
            var generalMatch = Regex.Matches(sanitized, @"(?:\b|^)(\d{1,3}(?:[.,\s]\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)(?:\b|$)", RegexOptions.IgnoreCase);
            foreach (Match m in generalMatch)
            {
                decimal? val = ParseDecimalValue(m.Groups[1].Value);
                if (val.HasValue && val.Value > 0)
                {
                    if (val.Value < 50)
                    {
                        continue; // Skip small isolated digits!
                    }

                    // Skip isolated 4-digit years (1950 - 2099) without currency
                    if (val.Value >= 1950 && val.Value <= 2099)
                    {
                        continue;
                    }

                    return val;
                }
            }

            return null;
        }

        private static decimal? ParseDecimalValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string clean = raw.Replace(",", "").Replace(" ", "").Trim();
            // Thousand separator with dot (e.g. 22.000 or 150.000)
            if (Regex.IsMatch(clean, @"^\d{1,3}\.\d{3}$"))
            {
                clean = clean.Replace(".", "");
            }
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val))
            {
                if (val > 0 && val < 10000000000) return val;
            }
            return null;
        }

        private string? ExtractCategoryByUserKeywords(string text, List<DynamicKeyword> keywords, bool isOutgoing, out bool fromKeyword, out bool isConflicted)
        {
            fromKeyword = false;
            isConflicted = false;
            if (keywords == null || !keywords.Any()) return null;

            // Only keywords for financial transaction types can be categories.
            var validCategoryTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "استلام", "تسليم", "إيداع", "ايداع", "سحب", "صرف", "قبض", "دفع", "تحويل", "شحن"
            };

            bool matchesReceipt = false;
            bool matchesDelivery = false;
            string? firstMatch = null;

            foreach (var kw in keywords.Where(k => !string.IsNullOrWhiteSpace(k.Type) && validCategoryTypes.Contains(k.Type.Trim())))
            {
                string word = kw.Word.Trim();
                if (string.IsNullOrEmpty(word)) continue;

                var pattern = $@"(?:\b|[\s\p{{P}}]|^){Regex.Escape(word)}(?:[\s\p{{P}}]|\b|$)";
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                {
                    fromKeyword = true;
                    string type = kw.Type.Trim();
                    string resolved = type;

                    if (type == "صرف" || type == "دفع" || type == "تحويل" || type == "تسليم" || type == "إيداع" || type == "ايداع")
                    {
                        resolved = isOutgoing ? "تسليم" : "استلام";
                    }
                    else if (type == "قبض" || type == "شحن" || type == "استلام" || type == "سحب")
                    {
                        resolved = isOutgoing ? "استلام" : "تسليم";
                    }

                    if (resolved == "استلام") matchesReceipt = true;
                    if (resolved == "تسليم") matchesDelivery = true;

                    firstMatch ??= resolved;
                }
            }

            // إذا كانت الكلمة مضافة في الاستلام والتسليم معاً، يعتبر تعارض ولا يتم التخمين بل تحويل الرسالة إلى مسودة
            if (matchesReceipt && matchesDelivery)
            {
                isConflicted = true;
                _logger.LogInformation("[Keyword Conflict] Matched both Receipt and Delivery keywords in text: {Text}. Setting as Conflicted Draft.", text);
                return null;
            }

            return firstMatch;
        }

        private static string? ExtractExplicitCategory(string text, bool isOutgoing)
        {
            // 1. أفعال التسليم والصرف والدفع والتحويل:
            // في الرسالة الصادرة (أنا أرسلتها): تسليم
            // في الرسالة الواردة (الطرف الآخر أرسلها إليّ): استلام
            var expensePattern = @"(?:\b|^)(تسليم|تسليمات|سلم|سلمت|سلملي|سلموا|تسلم|تسلملي|يسلم|نسلم|صرف|صرفت|دفع|دفعت|ادفع|تحويل|حولت|حولتلك|حولنا|حول|اعطاء|اعطي|عطيت|عطيته)(?:\b|$)";
            if (Regex.IsMatch(text, expensePattern, RegexOptions.IgnoreCase))
            {
                return isOutgoing ? "تسليم" : "استلام";
            }

            // 2. أفعال الاستلام والقبض والشحن والوصول:
            // في الرسالة الصادرة (أنا أرسلتها): استلام
            // في الرسالة الواردة (الطرف الآخر أرسلها إليّ): تسليم
            var incomePattern = @"(?:\b|^)(استلام|استلامات|استلم|استلمت|استلمنا|تستلم|يستلم|نستلم|قبض|قبضت|قبضنا|وصل|وصلني|وصلتني|وصلتنا|جاني|شحن|استقبال)(?:\b|$)";
            if (Regex.IsMatch(text, incomePattern, RegexOptions.IgnoreCase))
            {
                return isOutgoing ? "استلام" : "تسليم";
            }

            // 3. قواعد الإيداع والسحب حسب اتجاه الرسالة:
            var depositPattern = @"(?:\b|^)(إيداع|ايداع|حطيت|مودع|اودعت|أودعت|إيداعات|ايداعات)(?:\b|$)";
            if (Regex.IsMatch(text, depositPattern, RegexOptions.IgnoreCase))
            {
                return isOutgoing ? "تسليم" : "استلام";
            }

            var withdrawPattern = @"(?:\b|^)(سحب|سحبت|سحبنا|مسحوب|سحوبات|اسحب)(?:\b|$)";
            if (Regex.IsMatch(text, withdrawPattern, RegexOptions.IgnoreCase))
            {
                return isOutgoing ? "استلام" : "تسليم";
            }

            return null;
        }

        private static string? ExtractExplicitCurrency(string text, List<DynamicKeyword> keywords)
        {
            if (keywords != null)
            {
                // Dynamic keywords for currencies: where Type is NOT "استلام" or "تسليم"
                // Sort by length descending so compound keywords (e.g. "ين صيني", "دينار كويتي") match before single words
                var currencyKeywords = keywords
                    .Where(k => !string.IsNullOrWhiteSpace(k.Word) && !string.IsNullOrWhiteSpace(k.Type) && k.Type != "\u0627\u0633\u062a\u0644\u0627\u0645" && k.Type != "\u062a\u0633\u0644\u064a\u0645")
                    .OrderByDescending(k => k.Word.Trim().Length);

                foreach (var kw in currencyKeywords)
                {
                    var pattern = $@"(?:\b|[\s\p{{P}}]|^){Regex.Escape(kw.Word.Trim())}(?:[\s\p{{P}}]|\b|$)";
                    if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    {
                        return kw.Type.Trim().ToUpper();
                    }
                }
            }

            // Fallback Currency Recognition (Specific/Compound phrases evaluated first)
            if (Regex.IsMatch(text, @"(?:\b|^)(روبل\s+بيلاروسي|ر\.ب|byn|byr)(?:\b|$)", RegexOptions.IgnoreCase))
                return "BYN";

            if (Regex.IsMatch(text, @"(?:\b|^)(روبل\s+روسي|روبل|₽+|rub)(?:\b|$)", RegexOptions.IgnoreCase))
                return "RUB";

            if (Regex.IsMatch(text, @"(?:\b|^)(ين\s+ياباني|jpy|yen)(?:\b|$)", RegexOptions.IgnoreCase))
                return "JPY";

            if (Regex.IsMatch(text, @"(?:\b|^)(يوان\s+صيني|ين\s+صيني|رنمينبي|رينمينبي|يوان|ين|¥+|cny|rmb)(?:\b|$)", RegexOptions.IgnoreCase))
                return "CNY";

            if (Regex.IsMatch(text, @"(?:\b|^)(دينار\s+كويتي|د\.ك|kwd)(?:\b|$)", RegexOptions.IgnoreCase))
                return "KWD";

            if (Regex.IsMatch(text, @"(?:\b|^)(دينار\s+بحريني|د\.ب|bhd)(?:\b|$)", RegexOptions.IgnoreCase))
                return "BHD";

            if (Regex.IsMatch(text, @"(?:\b|^)(ريال\s+عماني|ر\.ع|omr)(?:\b|$)", RegexOptions.IgnoreCase))
                return "OMR";

            if (Regex.IsMatch(text, @"(?:\b|^)(ريال\s+قطري|ر\.ق|qar)(?:\b|$)", RegexOptions.IgnoreCase))
                return "QAR";

            if (Regex.IsMatch(text, @"(?:\b|^)(دينار\s+أردني|دينار\s+اردني|د\.أ|د\.ا|jod)(?:\b|$)", RegexOptions.IgnoreCase))
                return "JOD";

            if (Regex.IsMatch(text, @"(?:\b|^)(دينار\s+عراقي|د\.ع|iqd)(?:\b|$)", RegexOptions.IgnoreCase))
                return "IQD";

            if (Regex.IsMatch(text, @"(?:\b|^)(دينار\s+تونسي|د\.ت|tnd)(?:\b|$)", RegexOptions.IgnoreCase))
                return "TND";

            if (Regex.IsMatch(text, @"(?:\b|^)(درهم\s+مغربي|د\.م|mad)(?:\b|$)", RegexOptions.IgnoreCase))
                return "MAD";

            if (Regex.IsMatch(text, @"(?:\b|^)(دينار\s+جزائري|د\.ج|dzd)(?:\b|$)", RegexOptions.IgnoreCase))
                return "DZD";

            if (Regex.IsMatch(text, @"(?:\b|^)(جنيه\s+سوداني|ج\.س|sdg)(?:\b|$)", RegexOptions.IgnoreCase))
                return "SDG";

            if (Regex.IsMatch(text, @"(?:\b|^)(ليرة\s+لبنانية|ليرة\s+لبنانيه|ل\.ل|lbp)(?:\b|$)", RegexOptions.IgnoreCase))
                return "LBP";

            if (Regex.IsMatch(text, @"(?:\b|^)(ليرة\s+سورية|ليرة\s+سوريه|ل\.س|syp)(?:\b|$)", RegexOptions.IgnoreCase))
                return "SYP";

            if (Regex.IsMatch(text, @"(?:\b|^)(ريال\s+يمني|ر\.ي|yer)(?:\b|$)", RegexOptions.IgnoreCase))
                return "YER";

            if (Regex.IsMatch(text, @"(?:\b|^)(دولار\s+كندي|c\$|cad)(?:\b|$)", RegexOptions.IgnoreCase))
                return "CAD";

            if (Regex.IsMatch(text, @"(?:\b|^)(دولار\s+أسترالي|دولار\s+استرالي|a\$|aud)(?:\b|$)", RegexOptions.IgnoreCase))
                return "AUD";

            if (Regex.IsMatch(text, @"(?:\b|^)(جنيه\s+إسترليني|جنيه\s+استرليني|باوند|استرليني|إسترليني|£+|gbp)(?:\b|$)", RegexOptions.IgnoreCase))
                return "GBP";

            if (Regex.IsMatch(text, @"(?:\b|^)(فرنك\s+سويسري|فرنك|chf|franc)(?:\b|$)", RegexOptions.IgnoreCase))
                return "CHF";

            if (Regex.IsMatch(text, @"(?:\b|^)(روبية\s+هندية|روبية\s+هنديه|روبية|روبيه|₹+|inr|rupee)(?:\b|$)", RegexOptions.IgnoreCase))
                return "INR";

            if (Regex.IsMatch(text, @"(?:\b|^)(تيذر|رقمي|usdt)(?:\b|$)", RegexOptions.IgnoreCase))
                return "USDT";

            if (Regex.IsMatch(text, @"(?:\b|^)(ليرة\s+تركية|ليرة\s+تركيه|ليرة|ليره|تركي|try|tl|turkish)(?:\b|$)", RegexOptions.IgnoreCase))
                return "TRY";

            if (Regex.IsMatch(text, @"(?:\b|^)(ريال\s+سعودي|س\.ر|ر\.س|ريال|sar|riyal|rs)(?:\b|$)", RegexOptions.IgnoreCase))
                return "SAR";

            if (Regex.IsMatch(text, @"(?:\b|^)(درهم\s+إماراتي|درهم\s+اماراتي|د\.إ|د\.ا|درهم|aed|dh|dirham)(?:\b|$)", RegexOptions.IgnoreCase))
                return "AED";

            if (Regex.IsMatch(text, @"(?:\b|^)(جنيه\s+مصري|ج\.م|جنيه|egp)(?:\b|$)", RegexOptions.IgnoreCase))
                return "EGP";

            if (Regex.IsMatch(text, @"(?:\b|^|[^\w])(دولار\s+أمريكي|دولار\s+امريكي|دولار|دولارات|\$+|usd|dollar|bucks|ورقة|أخضر|اخضر)(?:\b|$|[^\w])", RegexOptions.IgnoreCase))
                return "USD";

            if (Regex.IsMatch(text, @"(?:\b|^)(يورو|€+|eur|euro|أورو|اورو)(?:\b|$)", RegexOptions.IgnoreCase))
                return "EUR";

            if (Regex.IsMatch(text, @"(?:\b|^)(دينار\s+ليبي|د\.ل|دل|د\.ل\.|دينار|ليبي|كاش|lyd)(?:\b|$)", RegexOptions.IgnoreCase))
                return "LYD";

            return null;
        }

        private static string? ExtractParty(string text, List<DynamicKeyword> keywords)
        {
            // Priority 1: Explicit Commercial / Business Entity (e.g. شركة الواثقون, شركة التوكل, شركة الريادة, مكتب الصرافة)
            var companyMatch = Regex.Match(text, @"(?:\b|^)(شركة|مكتب|محلات|محل|صرافة|صرافه|وكالة|وكاله|مؤسسة|مؤسسه|معرض|مصنع|عيادة|عياده|فندق|مستشفى|صيدلية|صيدليه)\s+([^\d\r\n,.!?/\\:;#]+)", RegexOptions.IgnoreCase);
            if (companyMatch.Success)
            {
                string companyType = companyMatch.Groups[1].Value.Trim();
                string companyName = CleanPartyName(companyMatch.Groups[2].Value);
                if (!string.IsNullOrWhiteSpace(companyName) && IsValidPartyName(companyName))
                {
                    return $"{companyType} {companyName}".Trim();
                }
            }

            // Priority 2: Explicit Party Label (e.g. الطرف: فلان, المستلم: فلان, لصالح: فلان, المستفيد: فلان)
            var roleMatch = Regex.Match(text, @"(?:\b|^)(?:الطرف|المستلم|المستفيد|العميل|الزبون|باسم|بإسم|لصالح|حساب|طرف)\s*[:=-]?\s*([^\d\r\n,.!?/\\:;#]+)", RegexOptions.IgnoreCase);
            if (roleMatch.Success)
            {
                string party = CleanPartyName(roleMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(party) && IsValidPartyName(party)) return party;
            }

            // Priority 3: Prepositional / Verb phrases (e.g. أرجو تسليم فلان, استلم من فلان, تسليم لـ فلان)
            var prepMatch = Regex.Match(text, @"(?:أرجو\s+تسليم|ارجو\s+تسليم|أرجو\s+استلام|ارجو\s+استلام|الرجاء\s+تسليم|الرجاء\s+استلام|ياريت\s+تسلم|ياريت\s+تسلملي|ياريت\s+تسليم|ياريت\s+استلام|ممكن\s+تسلم|ممكن\s+تسلملي|لو\s*سمحت\s+تسلم|لو\s*سمحت\s+سلم|تسليم\s+لـ|تسليم\s+إلى|تسليم\s+الى|تسليم|استلام\s+من|استلام|استلم\s+من|استلم|إيداع\s+في\s+حساب|إيداع\s+لـ|إيداع|ايداع|سحب\s+من|سحب|سلم\s+لـ|سلم|تسلم|من|إلى|الى|لـ|عن\s+طريق)\s+([^\d\r\n,.!?/\\:;#]+)", RegexOptions.IgnoreCase);
            if (prepMatch.Success)
            {
                string party = CleanPartyName(prepMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(party) && IsValidPartyName(party)) return party;
            }

            // Priority 4: Name at start before amount: "محمد 400 دينار"
            var startNameMatch = Regex.Match(text, @"^([^\d\r\n,.!?_+=*/\\;:'""`~]{2,30})\s+(?:\d{1,3}(?:[,\s]\d{3})*(?:\.\d+)?|\d+)", RegexOptions.IgnoreCase);
            if (startNameMatch.Success)
            {
                string party = CleanPartyName(startNameMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(party) && IsValidPartyName(party)) return party;
            }

            // Priority 5: Suffix patterns: "400 دينار علي"
            var suffixMatch = Regex.Match(text, @"(?:\d+|دينار|دولار|يورو|ليرة|LYD|USD|EUR)\s+([\p{L}\s]{2,30})$", RegexOptions.IgnoreCase);
            if (suffixMatch.Success)
            {
                string party = CleanPartyName(suffixMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(party) && IsValidPartyName(party)) return party;
            }

            return null;
        }

        private static string CleanPartyName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            // Remove financial keywords
            string cleaned = Regex.Replace(raw, @"(?:\b|^)(الرجاء|تسليم|تسليمات|استلام|استلامات|استلم|دينار|دولار|يورو|ليرة|ريال|درهم|جنيه|باوند|ين|يوان|روبل|فرنك|روبية|تيذر|صرف|قبض|تحويل|إيداع|ايداع|سحب|LYD|USD|EUR|TRY|SAR|AED|EGP|GBP|CNY|BYN|RUB|KWD|QAR|BHD|OMR|JOD|IQD|TND|MAD|DZD|SDG|LBP|SYP|YER|CHF|INR|USDT|JPY)(?:\b|$)", "", RegexOptions.IgnoreCase);
            // Remove common Arabic stop words, address terms, and tracking terms
            cleaned = Regex.Replace(cleaned, @"(?:\b|^)(تم|تمت|تمه|يتم|عملية|مبلغ|قيمة|قيمه|رصيد|باقي|واصل|فلوس|مصاري|كاش|نقدي|نقد|شيك|حوالة|حواله|دفعة|دفعه|قسط|فاتورة|فاتوره|حساب|بنك|مصرف|فرع|رقم|بتاريخ|اليوم|أمس|امس|غدا|غدوة|الصبح|المسا|على|في|من|الى|إلى|عن|مع|بدون|بعد|قبل|كل|هذا|هذه|هذي|ذلك|هناك|هنا|لكن|أو|او|ان|أن|إن|لا|نعم|ايوا|لأ|كان|يكون|هو|هي|هم|أنا|انا|نحن|انت|أنت|فقط|بس|خلاص|اوكي|تمام|طيب|الله|يسلمك|شكرا|شكراً|مشكور|جزاك|ياريت|لوسمحت|ممكن|رجاءً|رجاء|أرجو|ارجو|عفوا|عفواً|بليز|please|plz|العنوان|عنوان|شارع|طريق|عمارة|شقة|طابق|اسطنبول|istanbul|fatih|تركيا|turkey|اشاري|إشاري|بوليصة|بوليصه|كود|code)(?:\b|$)", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"[^\p{L}\s]", " ").Trim();
            var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 4) cleaned = string.Join(" ", words.Take(4));
            cleaned = cleaned.Trim();
            return cleaned;
        }

        private static bool IsValidPartyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 2) return false;

            // Reject if contains 3 or more identical consecutive letters (e.g. ططط, هههه, aaaa, bbbb)
            if (Regex.IsMatch(name, @"(.)\1{2,}")) return false;

            // Reject keyboard mash / random two-letter oscillations (e.g. طؤط, طهط, طنط, طط)
            if (Regex.IsMatch(name, @"^(?:ط[ؤهنت][ط]?|هه|خخ|ءء)+$", RegexOptions.IgnoreCase)) return false;

            // Reject common casual chat words
            var chatWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "نشرحلهم", "صفوها", "كلهم", "الحمدلله", "دفعه", "دفعة", "دفعتي", "شباب", "مرحبا", "اهلين", "عادي", "وينك", "باهي", "صحه", "مبروك"
            };

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int validParts = 0;
            foreach (var p in parts)
            {
                if (chatWords.Contains(p)) continue;
                // A valid name part must have at least 2 characters and not be repeated letters
                if (p.Length >= 2 && !Regex.IsMatch(p, @"^(.)\1+$"))
                {
                    validParts++;
                }
            }

            return validParts > 0;
        }
    }
}
