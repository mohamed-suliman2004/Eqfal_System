using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eqfal.API.Data;
using Eqfal.API.Models;

namespace Eqfal.API.Services
{
    public class KeywordSeederService : IKeywordSeederService
    {
        private readonly AppDbContext _context;

        public KeywordSeederService(AppDbContext context)
        {
            _context = context;
        }

        public async Task SeedDefaultKeywordsAsync(int userId)
        {
            var defaultKeywords = new List<DynamicKeyword>
            {
                // استلام
                new DynamicKeyword { UserId = userId, Word = "\u0627\u0633\u062a\u0644\u0627\u0645", Type = "\u0627\u0633\u062a\u0644\u0627\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0627\u0633\u062a\u0644\u0645\u062a", Type = "\u0627\u0633\u062a\u0644\u0627\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0648\u0635\u0644", Type = "\u0627\u0633\u062a\u0644\u0627\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0648\u0635\u0644\u0646\u064a", Type = "\u0627\u0633\u062a\u0644\u0627\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0627\u064a\u062f\u0627\u0639", Type = "\u0627\u0633\u062a\u0644\u0627\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0642\u0628\u0636\u062a", Type = "\u0627\u0633\u062a\u0644\u0627\u0645", CreatedAt = DateTime.UtcNow },

                // تسليم
                new DynamicKeyword { UserId = userId, Word = "\u062a\u0633\u0644\u064a\u0645", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0633\u0644\u0645\u062a", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u062a\u062d\u0648\u064a\u0644", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u062d\u0648\u0644\u062a", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u062d\u0648\u0644\u062a\u0644\u0643", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0635\u0631\u0641", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u0633\u062d\u0628", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "\u062f\u0641\u0639", Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow },

                // Currencies (30 Arab, Gulf, and Global Currencies)
                // LYD - دينار ليبي
                new DynamicKeyword { UserId = userId, Word = "دينار ليبي", Type = "LYD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "دينار", Type = "LYD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.ل", Type = "LYD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "دل", Type = "LYD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "lyd", Type = "LYD", CreatedAt = DateTime.UtcNow },

                // SAR - ريال سعودي
                new DynamicKeyword { UserId = userId, Word = "ريال سعودي", Type = "SAR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ريال", Type = "SAR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "س.ر", Type = "SAR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ر.س", Type = "SAR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "sar", Type = "SAR", CreatedAt = DateTime.UtcNow },

                // AED - درهم إماراتي
                new DynamicKeyword { UserId = userId, Word = "درهم إماراتي", Type = "AED", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "درهم", Type = "AED", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.إ", Type = "AED", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "aed", Type = "AED", CreatedAt = DateTime.UtcNow },

                // QAR - ريال قطري
                new DynamicKeyword { UserId = userId, Word = "ريال قطري", Type = "QAR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ر.ق", Type = "QAR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "qar", Type = "QAR", CreatedAt = DateTime.UtcNow },

                // KWD - دينار كويتي
                new DynamicKeyword { UserId = userId, Word = "دينار كويتي", Type = "KWD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.ك", Type = "KWD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "kwd", Type = "KWD", CreatedAt = DateTime.UtcNow },

                // BHD - دينار بحريني
                new DynamicKeyword { UserId = userId, Word = "دينار بحريني", Type = "BHD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.ب", Type = "BHD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "bhd", Type = "BHD", CreatedAt = DateTime.UtcNow },

                // OMR - ريال عماني
                new DynamicKeyword { UserId = userId, Word = "ريال عماني", Type = "OMR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ر.ع", Type = "OMR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "omr", Type = "OMR", CreatedAt = DateTime.UtcNow },

                // JOD - دينار أردني
                new DynamicKeyword { UserId = userId, Word = "دينار أردني", Type = "JOD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.أ", Type = "JOD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "jod", Type = "JOD", CreatedAt = DateTime.UtcNow },

                // IQD - دينار عراقي
                new DynamicKeyword { UserId = userId, Word = "دينار عراقي", Type = "IQD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.ع", Type = "IQD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "iqd", Type = "IQD", CreatedAt = DateTime.UtcNow },

                // TND - دينار تونسي
                new DynamicKeyword { UserId = userId, Word = "دينار تونسي", Type = "TND", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.ت", Type = "TND", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "tnd", Type = "TND", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "تونس", Type = "TND", CreatedAt = DateTime.UtcNow },

                // MAD - درهم مغربي
                new DynamicKeyword { UserId = userId, Word = "درهم مغربي", Type = "MAD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.م", Type = "MAD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "mad", Type = "MAD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "مغرب", Type = "MAD", CreatedAt = DateTime.UtcNow },

                // DZD - دينار جزائري
                new DynamicKeyword { UserId = userId, Word = "دينار جزائري", Type = "DZD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "د.ج", Type = "DZD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "dzd", Type = "DZD", CreatedAt = DateTime.UtcNow },

                // EGP - جنيه مصري
                new DynamicKeyword { UserId = userId, Word = "جنيه مصري", Type = "EGP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "جنيه", Type = "EGP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ج.م", Type = "EGP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "egp", Type = "EGP", CreatedAt = DateTime.UtcNow },

                // SDG - جنيه سوداني
                new DynamicKeyword { UserId = userId, Word = "جنيه سوداني", Type = "SDG", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ج.س", Type = "SDG", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "sdg", Type = "SDG", CreatedAt = DateTime.UtcNow },

                // LBP - ليرة لبنانية
                new DynamicKeyword { UserId = userId, Word = "ليرة لبنانية", Type = "LBP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ل.ل", Type = "LBP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "lbp", Type = "LBP", CreatedAt = DateTime.UtcNow },

                // SYP - ليرة سورية
                new DynamicKeyword { UserId = userId, Word = "ليرة سورية", Type = "SYP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ل.س", Type = "SYP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "syp", Type = "SYP", CreatedAt = DateTime.UtcNow },

                // TRY - ليرة تركية
                new DynamicKeyword { UserId = userId, Word = "ليرة تركية", Type = "TRY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ليرة", Type = "TRY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ليره", Type = "TRY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "تركي", Type = "TRY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "try", Type = "TRY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "tl", Type = "TRY", CreatedAt = DateTime.UtcNow },

                // YER - ريال يمني
                new DynamicKeyword { UserId = userId, Word = "ريال يمني", Type = "YER", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ر.ي", Type = "YER", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "yer", Type = "YER", CreatedAt = DateTime.UtcNow },

                // USD - دولار أمريكي
                new DynamicKeyword { UserId = userId, Word = "دولار أمريكي", Type = "USD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "دولار", Type = "USD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "$", Type = "USD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "usd", Type = "USD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "dollar", Type = "USD", CreatedAt = DateTime.UtcNow },

                // EUR - يورو
                new DynamicKeyword { UserId = userId, Word = "يورو", Type = "EUR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "€", Type = "EUR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "eur", Type = "EUR", CreatedAt = DateTime.UtcNow },

                // CNY - يوان صيني
                new DynamicKeyword { UserId = userId, Word = "يوان صيني", Type = "CNY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ين صيني", Type = "CNY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "يوان", Type = "CNY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ين", Type = "CNY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "¥", Type = "CNY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "cny", Type = "CNY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "rmb", Type = "CNY", CreatedAt = DateTime.UtcNow },

                // JPY - ين ياباني
                new DynamicKeyword { UserId = userId, Word = "ين ياباني", Type = "JPY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "jpy", Type = "JPY", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "yen", Type = "JPY", CreatedAt = DateTime.UtcNow },

                // GBP - جنيه إسترليني
                new DynamicKeyword { UserId = userId, Word = "جنيه إسترليني", Type = "GBP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "باوند", Type = "GBP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "استرليني", Type = "GBP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "£", Type = "GBP", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "gbp", Type = "GBP", CreatedAt = DateTime.UtcNow },

                // CHF - فرنك سويسري
                new DynamicKeyword { UserId = userId, Word = "فرنك سويسري", Type = "CHF", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "فرنك", Type = "CHF", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "chf", Type = "CHF", CreatedAt = DateTime.UtcNow },

                // CAD - دولار كندي
                new DynamicKeyword { UserId = userId, Word = "دولار كندي", Type = "CAD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "cad", Type = "CAD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "c$", Type = "CAD", CreatedAt = DateTime.UtcNow },

                // AUD - دولار أسترالي
                new DynamicKeyword { UserId = userId, Word = "دولار أسترالي", Type = "AUD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "aud", Type = "AUD", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "a$", Type = "AUD", CreatedAt = DateTime.UtcNow },

                // RUB - روبل روسي
                new DynamicKeyword { UserId = userId, Word = "روبل روسي", Type = "RUB", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "روبل", Type = "RUB", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "₽", Type = "RUB", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "rub", Type = "RUB", CreatedAt = DateTime.UtcNow },

                // INR - روبية هندية
                new DynamicKeyword { UserId = userId, Word = "روبية هندية", Type = "INR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "روبية", Type = "INR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "₹", Type = "INR", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "inr", Type = "INR", CreatedAt = DateTime.UtcNow },

                // USDT - تيذر رقمي
                new DynamicKeyword { UserId = userId, Word = "تيذر رقمي", Type = "USDT", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "تيذر", Type = "USDT", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "usdt", Type = "USDT", CreatedAt = DateTime.UtcNow },

                // BYN - روبل بيلاروسي
                new DynamicKeyword { UserId = userId, Word = "روبل بيلاروسي", Type = "BYN", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "ر.ب", Type = "BYN", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "byn", Type = "BYN", CreatedAt = DateTime.UtcNow },
                new DynamicKeyword { UserId = userId, Word = "byr", Type = "BYN", CreatedAt = DateTime.UtcNow }
            };

            _context.DynamicKeywords.AddRange(defaultKeywords);
            await _context.SaveChangesAsync();
        }
    }
}
