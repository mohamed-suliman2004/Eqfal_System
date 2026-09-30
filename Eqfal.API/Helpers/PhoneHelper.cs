using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Eqfal.API.Helpers
{
    public static class PhoneHelper
    {
        public static string Normalize(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            string digits = Regex.Replace(phone, @"(@s\.whatsapp\.net|@lid|@c\.us|@g\.us).*", "", RegexOptions.IgnoreCase);
            digits = Regex.Replace(digits, @"[^\d]", "");

            if (string.IsNullOrEmpty(digits))
                return string.Empty;

            // Strip leading international 00
            if (digits.StartsWith("00"))
            {
                digits = digits.Substring(2);
            }

            // Libyan local format normalization (if 9 or 10 digits starting with 0 or 9)
            if (digits.StartsWith("09") && digits.Length == 10)
            {
                digits = "218" + digits.Substring(1); // 091... -> 21891...
            }
            else if ((digits.StartsWith("91") || digits.StartsWith("92") || digits.StartsWith("93") ||
                      digits.StartsWith("94") || digits.StartsWith("95") || digits.StartsWith("96")) && digits.Length == 9)
            {
                digits = "218" + digits; // 91... -> 21891...
            }
            // Egyptian local format normalization (010..., 011..., 012..., 015...)
            else if ((digits.StartsWith("010") || digits.StartsWith("011") || digits.StartsWith("012") || digits.StartsWith("015")) && digits.Length == 11)
            {
                digits = "20" + digits.Substring(1); // 010... -> 2010...
            }
            // Turkish local format normalization (05...)
            else if (digits.StartsWith("05") && digits.Length == 11)
            {
                digits = "90" + digits.Substring(1); // 05... -> 905...
            }
            // Tunisian local format normalization (8 digits starting with 9, 2, 5, 4)
            else if ((digits.StartsWith("9") || digits.StartsWith("2") || digits.StartsWith("5") || digits.StartsWith("4")) && digits.Length == 8)
            {
                digits = "216" + digits;
            }

            return digits;
        }

        public static bool IsRealPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            string digits = Regex.Replace(phone, @"[^\d]", "");
            if (digits.Length >= 15) return false; // 15+ digits is a WhatsApp LID
            return digits.Length >= 8 && digits.Length <= 14;
        }

        public static bool IsLid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string digits = Regex.Replace(value, @"[^\d]", "");
            return digits.Length >= 15;
        }

        public static string ResolveLidToRealPhone(string? lidOrPhone, string? customBasePath = null)
        {
            if (string.IsNullOrWhiteSpace(lidOrPhone)) return "";
            string digits = Regex.Replace(lidOrPhone, @"[^\d]", "");
            
            if (IsRealPhone(digits))
                return Normalize(digits);

            if (digits.Length < 10) return digits;

            try
            {
                string basePath = !string.IsNullOrEmpty(customBasePath) ? customBasePath : AppContext.BaseDirectory;
                if (Directory.Exists(basePath))
                {
                    var authDirs = Directory.GetDirectories(basePath, "auth_info_baileys*");
                    foreach (var dir in authDirs)
                    {
                        string mapFile = Path.Combine(dir, $"lid-mapping-{digits}_reverse.json");
                        if (File.Exists(mapFile))
                        {
                            string content = File.ReadAllText(mapFile).Trim().Trim('"').Trim();
                            string realDigits = Regex.Replace(content, @"[^\d]", "");
                            if (IsRealPhone(realDigits))
                            {
                                return Normalize(realDigits);
                            }
                        }
                    }
                }
            }
            catch { }

            return digits;
        }

        public static async Task<string> ResolveLidViaEvolutionApiAsync(
            string? lid, 
            string evolutionBaseUrl, 
            string instanceName = "user_1", 
            string apiKey = "")
        {
            if (string.IsNullOrWhiteSpace(lid)) return "";
            string digits = Regex.Replace(lid, @"[^\d]", "");
            if (IsRealPhone(digits)) return Normalize(digits);
            if (!IsLid(digits)) return digits;

            string baseUrl = evolutionBaseUrl.TrimEnd('/');
            string inst = !string.IsNullOrWhiteSpace(instanceName) ? instanceName : "user_1";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(2500);
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("apikey", apiKey);
            }

            try
            {
                var url = $"{baseUrl}/chat/whatsappNumbers/{inst}";
                var body = new { numbers = new[] { $"{digits}@lid", digits } };
                var res = await httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("jid", out var jidProp))
                            {
                                var jidStr = jidProp.GetString() ?? "";
                                if (jidStr.EndsWith("@s.whatsapp.net"))
                                {
                                    var pn = Normalize(jidStr);
                                    if (IsRealPhone(pn)) return pn;
                                }
                            }
                            if (item.TryGetProperty("pnJid", out var pnProp))
                            {
                                var pn = Normalize(pnProp.GetString());
                                if (IsRealPhone(pn)) return pn;
                            }
                        }
                    }
                }
            }
            catch { }

            return digits;
        }
    }
}