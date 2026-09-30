using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Eqfal.API.Models;

namespace Eqfal.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class KeywordsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public KeywordsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                        || c.Type == "nameid" 
                                                        || c.Type == "sub" 
                                                        || c.Type.Contains("nameidentifier"))?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return 1;
        }

        // GET: api/keywords?type=استلام
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DynamicKeyword>>> GetKeywords([FromQuery] string? type)
        {
            int userId = GetCurrentUserId();
            
            // Clean up any corrupted/empty records for this user
            var corrupted = await _context.DynamicKeywords
                .Where(k => k.UserId == userId && (string.IsNullOrWhiteSpace(k.Word) || k.Word.Contains("?") || string.IsNullOrWhiteSpace(k.Type) || k.Type.Contains("?")))
                .ToListAsync();
            if (corrupted.Any())
            {
                _context.DynamicKeywords.RemoveRange(corrupted);
                await _context.SaveChangesAsync();
            }

            // Check if user has classification keywords
            bool hasIncome = await _context.DynamicKeywords.AnyAsync(k => k.UserId == userId && k.Type == "\u0627\u0633\u062a\u0644\u0627\u0645");
            bool hasExpense = await _context.DynamicKeywords.AnyAsync(k => k.UserId == userId && k.Type == "\u062a\u0633\u0644\u064a\u0645");
            bool hasCurrencies = await _context.DynamicKeywords.AnyAsync(k => k.UserId == userId && k.Type != "\u0627\u0633\u062a\u0644\u0627\u0645" && k.Type != "\u062a\u0633\u0644\u064a\u0645");

            var newKeywords = new List<DynamicKeyword>();

            if (!hasIncome)
            {
                // استلام
                string[] incomeWords = { "\u0627\u0633\u062a\u0644\u0627\u0645", "\u0627\u0633\u062a\u0644\u0645\u062a", "\u0627\u0633\u0644\u0645", "\u0648\u0635\u0644", "\u0648\u0635\u0644\u0646\u064a", "\u0642\u0628\u0636\u062a", "\u0642\u0628\u0636", "\u0648\u0627\u0631\u062f", "\u0627\u064a\u062f\u0627\u0639", "\u062f\u062e\u0644", "\u0645\u0642\u0628\u0648\u0636\u0627\u062a" };
                foreach (var w in incomeWords)
                {
                    newKeywords.Add(new DynamicKeyword { UserId = userId, Word = w, Type = "\u0627\u0633\u062a\u0644\u0627\u0645", CreatedAt = DateTime.UtcNow });
                }
            }

            if (!hasExpense)
            {
                // تسليم
                string[] expenseWords = { "\u062a\u0633\u0644\u064a\u0645", "\u0633\u0644\u0645\u062a", "\u0633\u0644\u0645", "\u062d\u0648\u0644\u062a", "\u062d\u0648\u0644", "\u062a\u062d\u0648\u064a\u0644", "\u0635\u0631\u0641", "\u0635\u0631\u0641\u062a", "\u062f\u0641\u0639", "\u062f\u0641\u0639\u062a", "\u0635\u0627\u062f\u0631", "\u0645\u0635\u0631\u0648\u0641", "\u0645\u0635\u0627\u0631\u064a\u0641", "\u0633\u062d\u0628" };
                foreach (var w in expenseWords)
                {
                    newKeywords.Add(new DynamicKeyword { UserId = userId, Word = w, Type = "\u062a\u0633\u0644\u064a\u0645", CreatedAt = DateTime.UtcNow });
                }
            }

            if (!hasCurrencies)
            {
                foreach (var (word, code) in DefaultCurrencies)
                {
                    newKeywords.Add(new DynamicKeyword { UserId = userId, Word = word, Type = code, CreatedAt = DateTime.UtcNow });
                }
            }

            if (newKeywords.Any())
            {
                _context.DynamicKeywords.AddRange(newKeywords);
                await _context.SaveChangesAsync();
            }

            var query = _context.DynamicKeywords.AsQueryable().Where(k => k.UserId == userId);

            if (!string.IsNullOrEmpty(type))
            {
                string normType = type.Trim();
                query = query.Where(k => k.Type == normType);
            }

            return await query.OrderByDescending(k => k.CreatedAt).ToListAsync();
        }

        // POST: api/keywords
        [HttpPost]
        public async Task<ActionResult<DynamicKeyword>> PostKeyword(DynamicKeyword keyword)
        {
            keyword.UserId = GetCurrentUserId();
            keyword.CreatedAt = DateTime.UtcNow;
            
            if (string.IsNullOrWhiteSpace(keyword.Word) || string.IsNullOrWhiteSpace(keyword.Type))
            {
                return BadRequest(new { message = "يرجى إدخال الكلمة والتصنيف بشكل صحيح" });
            }

            keyword.Word = keyword.Word.Trim();
            keyword.Type = keyword.Type.Trim();

            // Check if it already exists
            bool exists = await _context.DynamicKeywords
                .AnyAsync(k => k.UserId == keyword.UserId && k.Type == keyword.Type && k.Word == keyword.Word);
                
            if (exists)
            {
                return Conflict(new { message = "الكلمة المفتاحية موجودة مسبقاً في هذا القاموس" });
            }

            _context.DynamicKeywords.Add(keyword);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetKeywords), new { id = keyword.Id }, keyword);
        }

        // PUT: api/keywords/5 or POST: api/keywords/5/update
        [HttpPut("{id}")]
        [HttpPost("{id}/update")]
        [HttpPost("update/{id}")]
        public async Task<IActionResult> UpdateKeyword(int id, [FromBody] DynamicKeyword updated)
        {
            int userId = GetCurrentUserId();
            var keyword = await _context.DynamicKeywords.FirstOrDefaultAsync(k => k.Id == id && k.UserId == userId);
            if (keyword == null)
            {
                return NotFound(new { message = "الرمز أو الكلمة المفتاحية غير موجودة" });
            }

            if (string.IsNullOrWhiteSpace(updated.Word))
            {
                return BadRequest(new { message = "يرجى إدخال الكلمة أو الرمز" });
            }

            keyword.Word = updated.Word.Trim();
            if (!string.IsNullOrWhiteSpace(updated.Type))
            {
                keyword.Type = updated.Type.Trim();
            }

            await _context.SaveChangesAsync();
            return Ok(keyword);
        }

        // POST: api/keywords/rename-type
        [HttpPost("rename-type")]
        public async Task<IActionResult> RenameType([FromBody] RenameTypeDto dto)
        {
            int userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(dto.OldType) || string.IsNullOrWhiteSpace(dto.NewType))
            {
                return BadRequest(new { message = "يرجى تحديد الرمز القديم والجديد للعملة" });
            }

            string oldType = dto.OldType.Trim();
            string newType = dto.NewType.Trim();

            var keywords = await _context.DynamicKeywords
                .Where(k => k.UserId == userId && k.Type == oldType)
                .ToListAsync();

            if (!keywords.Any())
            {
                return NotFound(new { message = "لم يتم العثور على رموز تابعة لهذه العملة" });
            }

            foreach (var kw in keywords)
            {
                kw.Type = newType;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, updatedCount = keywords.Count, newType = newType });
        }

        // DELETE: api/keywords/type/{type} or POST: api/keywords/delete-type
        [HttpDelete("type/{type}")]
        [HttpPost("delete-type")]
        public async Task<IActionResult> DeleteType([FromRoute] string? type, [FromBody] DeleteTypeDto? dto)
        {
            int userId = GetCurrentUserId();
            string? targetType = !string.IsNullOrWhiteSpace(type) ? type : dto?.Type;
            if (string.IsNullOrWhiteSpace(targetType))
            {
                return BadRequest(new { message = "يرجى تحديد نوع العملة المطلوب حذفها" });
            }

            targetType = targetType.Trim();
            var keywords = await _context.DynamicKeywords
                .Where(k => k.UserId == userId && k.Type == targetType)
                .ToListAsync();

            if (keywords.Any())
            {
                _context.DynamicKeywords.RemoveRange(keywords);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, deletedCount = keywords.Count });
        }

        // DELETE: api/keywords/5 or POST: api/keywords/5/delete
        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        [HttpPost("delete/{id}")]
        public async Task<IActionResult> DeleteKeyword(int id)
        {
            var keyword = await _context.DynamicKeywords.FindAsync(id);
            if (keyword == null)
            {
                return NotFound(new { message = "الكلمة المفتاحية غير موجودة" });
            }

            _context.DynamicKeywords.Remove(keyword);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = id });
        }

        // POST: api/keywords/sync-currencies
        [HttpPost("sync-currencies")]
        public async Task<IActionResult> SyncDefaultCurrencies()
        {
            int userId = GetCurrentUserId();
            var existingKeywords = await _context.DynamicKeywords
                .Where(k => k.UserId == userId)
                .Select(k => new { k.Word, k.Type })
                .ToListAsync();

            var existingSet = new HashSet<string>(existingKeywords.Select(k => $"{k.Type.ToUpper()}:{k.Word.Trim().ToLower()}"));
            var toAdd = new List<DynamicKeyword>();

            foreach (var (word, code) in DefaultCurrencies)
            {
                string key = $"{code.ToUpper()}:{word.Trim().ToLower()}";
                if (!existingSet.Contains(key))
                {
                    toAdd.Add(new DynamicKeyword
                    {
                        UserId = userId,
                        Word = word.Trim(),
                        Type = code.Trim().ToUpper(),
                        CreatedAt = DateTime.UtcNow
                    });
                    existingSet.Add(key);
                }
            }

            if (toAdd.Any())
            {
                _context.DynamicKeywords.AddRange(toAdd);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, addedCount = toAdd.Count });
        }

        private static readonly (string Word, string Code)[] DefaultCurrencies = new (string Word, string Code)[]
        {
            // LYD - دينار ليبي
            ("دينار ليبي", "LYD"),
            ("دينار", "LYD"),
            ("د.ل", "LYD"),
            ("دل", "LYD"),
            ("lyd", "LYD"),

            // SAR - ريال سعودي
            ("ريال سعودي", "SAR"),
            ("ريال", "SAR"),
            ("س.ر", "SAR"),
            ("ر.س", "SAR"),
            ("sar", "SAR"),

            // AED - درهم إماراتي
            ("درهم إماراتي", "AED"),
            ("درهم", "AED"),
            ("د.إ", "AED"),
            ("aed", "AED"),

            // QAR - ريال قطري
            ("ريال قطري", "QAR"),
            ("ر.ق", "QAR"),
            ("qar", "QAR"),

            // KWD - دينار كويتي
            ("دينار كويتي", "KWD"),
            ("د.ك", "KWD"),
            ("kwd", "KWD"),

            // BHD - دينار بحريني
            ("دينار بحريني", "BHD"),
            ("د.ب", "BHD"),
            ("bhd", "BHD"),

            // OMR - ريال عماني
            ("ريال عماني", "OMR"),
            ("ر.ع", "OMR"),
            ("omr", "OMR"),

            // JOD - دينار أردني
            ("دينار أردني", "JOD"),
            ("د.أ", "JOD"),
            ("jod", "JOD"),

            // IQD - دينار عراقي
            ("دينار عراقي", "IQD"),
            ("د.ع", "IQD"),
            ("iqd", "IQD"),

            // TND - دينار تونسي
            ("دينار تونسي", "TND"),
            ("د.ت", "TND"),
            ("tnd", "TND"),
            ("تونس", "TND"),

            // MAD - درهم مغربي
            ("درهم مغربي", "MAD"),
            ("د.م", "MAD"),
            ("mad", "MAD"),
            ("مغرب", "MAD"),

            // DZD - دينار جزائري
            ("دينار جزائري", "DZD"),
            ("د.ج", "DZD"),
            ("dzd", "DZD"),

            // EGP - جنيه مصري
            ("جنيه مصري", "EGP"),
            ("جنيه", "EGP"),
            ("ج.م", "EGP"),
            ("egp", "EGP"),

            // SDG - جنيه سوداني
            ("جنيه سوداني", "SDG"),
            ("ج.س", "SDG"),
            ("sdg", "SDG"),

            // LBP - ليرة لبنانية
            ("ليرة لبنانية", "LBP"),
            ("ل.ل", "LBP"),
            ("lbp", "LBP"),

            // SYP - ليرة سورية
            ("ليرة سورية", "SYP"),
            ("ل.س", "SYP"),
            ("syp", "SYP"),

            // TRY - ليرة تركية
            ("ليرة تركية", "TRY"),
            ("ليرة", "TRY"),
            ("ليره", "TRY"),
            ("تركي", "TRY"),
            ("try", "TRY"),
            ("tl", "TRY"),

            // YER - ريال يمني
            ("ريال يمني", "YER"),
            ("ر.ي", "YER"),
            ("yer", "YER"),

            // USD - دولار أمريكي
            ("دولار أمريكي", "USD"),
            ("دولار", "USD"),
            ("$", "USD"),
            ("usd", "USD"),
            ("dollar", "USD"),

            // EUR - يورو
            ("يورو", "EUR"),
            ("€", "EUR"),
            ("eur", "EUR"),

            // CNY - يوان صيني
            ("يوان صيني", "CNY"),
            ("ين صيني", "CNY"),
            ("يوان", "CNY"),
            ("ين", "CNY"),
            ("¥", "CNY"),
            ("cny", "CNY"),
            ("rmb", "CNY"),

            // JPY - ين ياباني
            ("ين ياباني", "JPY"),
            ("jpy", "JPY"),
            ("yen", "JPY"),

            // GBP - جنيه إسترليني
            ("جنيه إسترليني", "GBP"),
            ("باوند", "GBP"),
            ("استرليني", "GBP"),
            ("£", "GBP"),
            ("gbp", "GBP"),

            // CHF - فرنك سويسري
            ("فرنك سويسري", "CHF"),
            ("فرنك", "CHF"),
            ("chf", "CHF"),

            // CAD - دولار كندي
            ("دولار كندي", "CAD"),
            ("cad", "CAD"),
            ("c$", "CAD"),

            // AUD - دولار أسترالي
            ("دولار أسترالي", "AUD"),
            ("aud", "AUD"),
            ("a$", "AUD"),

            // RUB - روبل روسي
            ("روبل روسي", "RUB"),
            ("روبل", "RUB"),
            ("₽", "RUB"),
            ("rub", "RUB"),

            // INR - روبية هندية
            ("روبية هندية", "INR"),
            ("روبية", "INR"),
            ("₹", "INR"),
            ("inr", "INR"),

            // USDT - تيذر رقمي
            ("تيذر رقمي", "USDT"),
            ("تيذر", "USDT"),
            ("usdt", "USDT"),

            // BYN - روبل بيلاروسي
            ("روبل بيلاروسي", "BYN"),
            ("ر.ب", "BYN"),
            ("byn", "BYN"),
            ("byr", "BYN")
        };
    }

    public class RenameTypeDto
    {
        public string OldType { get; set; } = string.Empty;
        public string NewType { get; set; } = string.Empty;
    }

    public class DeleteTypeDto
    {
        public string Type { get; set; } = string.Empty;
    }
}