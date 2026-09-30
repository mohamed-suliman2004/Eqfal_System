using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Eqfal.API.Models;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class DiscountCodesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DiscountCodesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDiscountCodes()
        {
            var codes = await _context.DiscountCodes
                .AsNoTracking()
                .Include(d => d.Marketer)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            var result = codes.Select(d => new
            {
                id = d.Id,
                code = d.Code,
                discountType = (int)d.DiscountType,
                value = d.Value,
                expiresAt = d.ExpiresAt,
                maxUses = d.MaxUses,
                timesUsed = d.TimesUsed,
                isActive = d.IsActive,
                isUsable = d.IsUsable,
                marketerId = d.MarketerId,
                marketerName = d.Marketer.Name,
                marketerPhone = d.Marketer.Phone,
                commissionType = (int)d.CommissionType,
                commissionValue = d.CommissionValue,
                createdAt = d.CreatedAt
            }).ToList();

            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// اقتراح كود خصم تلقائي للمسوق
        /// </summary>
        [HttpGet("suggest")]
        public async Task<IActionResult> SuggestCode([FromQuery] Guid? marketerId = null)
        {
            string prefix = "EQF";

            if (marketerId.HasValue)
            {
                var marketer = await _context.Marketers.FindAsync(marketerId.Value);
                if (marketer != null)
                {
                    var clean = Regex.Replace(marketer.Name, @"[^\w]", "");
                    if (clean.Length >= 2)
                    {
                        prefix = clean.Substring(0, Math.Min(4, clean.Length)).ToUpperInvariant();
                    }
                }
            }

            string suggested = "";
            for (int i = 0; i < 10; i++)
            {
                string randHex = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
                suggested = $"{prefix}-{randHex}";

                if (!await _context.DiscountCodes.AnyAsync(d => d.Code == suggested))
                    break;
            }

            return Ok(new { success = true, suggestedCode = suggested });
        }

        [HttpPost]
        public async Task<IActionResult> CreateDiscountCode([FromBody] DiscountCodeUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { success = false, message = "كود الخصم مطلوب" });

            string normCode = dto.Code.Trim().ToUpperInvariant();
            if (await _context.DiscountCodes.AnyAsync(d => d.Code == normCode))
                return BadRequest(new { success = false, message = "هذا الكود مستخدم بالفعل" });

            decimal finalValue = dto.DiscountValue ?? dto.Value;
            if (finalValue <= 0)
                return BadRequest(new { success = false, message = "قيمة الخصم يجب أن تكون أكبر من صفر" });

            if (dto.DiscountType == 0 && finalValue > 100)
                return BadRequest(new { success = false, message = "نسبة الخصم لا يمكن أن تتجاوز 100%" });

            int? finalMaxUses = dto.MaxUsages ?? dto.MaxUses;
            if (finalMaxUses.HasValue && finalMaxUses.Value <= 0)
                return BadRequest(new { success = false, message = "الحد الأقصى للاستخدام يجب أن يكون أكبر من صفر إن حُدد" });

            if (dto.CommissionValue < 0)
                return BadRequest(new { success = false, message = "قيمة عمولة المسوق لا يمكن أن تكون بالسالب" });

            if (dto.CommissionType == 0 && dto.CommissionValue > 100)
                return BadRequest(new { success = false, message = "نسبة عمولة المسوق لا يمكن أن تتجاوز 100%" });

            // ربط الكود بالمسوق المحدد، أو ربطه بمسوق نظام افتراضي إن كان كوداً عاماً
            Marketer? marketer = null;
            if (dto.MarketerId.HasValue && dto.MarketerId.Value != Guid.Empty)
            {
                marketer = await _context.Marketers.FindAsync(dto.MarketerId.Value);
            }
            if (marketer == null)
            {
                marketer = await _context.Marketers.FirstOrDefaultAsync();
                if (marketer == null)
                {
                    var cat = await _context.MarketerCategories.FirstOrDefaultAsync();
                    if (cat == null)
                    {
                        cat = new MarketerCategory { Name = "عام", Description = "تصنيف عام" };
                        _context.MarketerCategories.Add(cat);
                        await _context.SaveChangesAsync();
                    }
                    marketer = new Marketer
                    {
                        Name = "إدارة النظام",
                        Phone = "0000000000",
                        CategoryId = cat.Id,
                        IsActive = true
                    };
                    _context.Marketers.Add(marketer);
                    await _context.SaveChangesAsync();
                }
            }

            var code = new DiscountCode
            {
                Code = normCode,
                DiscountType = (DiscountType)dto.DiscountType,
                Value = finalValue,
                ExpiresAt = dto.ExpiresAt,
                MaxUses = finalMaxUses,
                IsActive = dto.IsActive,
                MarketerId = marketer.Id,
                CommissionType = (DiscountType)dto.CommissionType,
                CommissionValue = dto.CommissionValue,
                CreatedAt = DateTime.UtcNow
            };

            _context.DiscountCodes.Add(code);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم إنشاء كود الخصم بنجاح", data = code.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDiscountCode(Guid id, [FromBody] DiscountCodeUpsertDto dto)
        {
            var code = await _context.DiscountCodes.FindAsync(id);
            if (code == null) return NotFound(new { success = false, message = "كود الخصم غير موجود" });

            string normCode = dto.Code.Trim().ToUpperInvariant();
            if (await _context.DiscountCodes.AnyAsync(d => d.Code == normCode && d.Id != id))
                return BadRequest(new { success = false, message = "هذا الكود مستخدم بالفعل لكود آخر" });

            var marketer = await _context.Marketers.FindAsync(dto.MarketerId);
            if (marketer == null)
                return BadRequest(new { success = false, message = "المسوق المحدد غير موجود" });

            if (dto.Value <= 0)
                return BadRequest(new { success = false, message = "قيمة الخصم يجب أن تكون أكبر من صفر" });

            if (dto.DiscountType == 0 && dto.Value > 100)
                return BadRequest(new { success = false, message = "نسبة الخصم لا يمكن أن تتجاوز 100%" });

            if (dto.MaxUses.HasValue && dto.MaxUses.Value <= 0)
                return BadRequest(new { success = false, message = "الحد الأقصى للاستخدام يجب أن يكون أكبر من صفر إن حُدد" });

            code.Code = normCode;
            code.DiscountType = (DiscountType)dto.DiscountType;
            code.Value = dto.Value;
            code.ExpiresAt = dto.ExpiresAt;
            code.MaxUses = dto.MaxUses;
            code.IsActive = dto.IsActive;
            if (dto.MarketerId.HasValue && dto.MarketerId.Value != Guid.Empty) code.MarketerId = dto.MarketerId.Value;
            code.CommissionType = (DiscountType)dto.CommissionType;
            code.CommissionValue = dto.CommissionValue;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم تعديل كود الخصم بنجاح" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDiscountCode(Guid id)
        {
            var code = await _context.DiscountCodes.FindAsync(id);
            if (code == null) return NotFound(new { success = false, message = "كود الخصم غير موجود" });

            if (code.TimesUsed > 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "لا يمكن حذف كود سبق استخدامه — يمكنك تعطيله بدلاً من ذلك"
                });
            }

            _context.DiscountCodes.Remove(code);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم حذف كود الخصم بنجاح" });
        }
    }

    public class DiscountCodeUpsertDto
    {
        public string Code { get; set; } = string.Empty;
        public int DiscountType { get; set; } = 0;
        public decimal Value { get; set; }
        public decimal? DiscountValue { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? MaxUses { get; set; }
        public int? MaxUsages { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? MarketerId { get; set; }
        public int CommissionType { get; set; } = 0;
        public decimal CommissionValue { get; set; } = 0;
    }
}
