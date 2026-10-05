using System;
using System.Collections.Generic;
using System.Linq;
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
    public class MarketersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MarketersController(AppDbContext context)
        {
            _context = context;
        }

        // ==================== تصنيفات المسوقين ====================

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.MarketerCategories
                .AsNoTracking()
                .Include(c => c.Marketers)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    description = c.Description,
                    defaultCommissionType = (int)c.DefaultCommissionType,
                    defaultCommissionValue = c.DefaultCommissionValue,
                    isActive = c.IsActive,
                    marketersCount = c.Marketers.Count,
                    createdAt = c.CreatedAt
                })
                .ToListAsync();

            return Ok(new { success = true, data = categories });
        }

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { success = false, message = "اسم التصنيف مطلوب" });

            string norm = dto.Name.Trim();
            if (await _context.MarketerCategories.AnyAsync(c => c.Name == norm))
                return BadRequest(new { success = false, message = "هذا التصنيف موجود بالفعل" });

            var cat = new MarketerCategory
            {
                Name = norm,
                Description = dto.Description?.Trim(),
                DefaultCommissionType = (DiscountType)dto.DefaultCommissionType,
                DefaultCommissionValue = dto.DefaultCommissionValue,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.MarketerCategories.Add(cat);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم إنشاء التصنيف بنجاح", data = cat.Id });
        }

        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryUpsertDto dto)
        {
            var cat = await _context.MarketerCategories.FindAsync(id);
            if (cat == null) return NotFound(new { success = false, message = "التصنيف غير موجود" });

            string norm = dto.Name.Trim();
            if (await _context.MarketerCategories.AnyAsync(c => c.Name == norm && c.Id != id))
                return BadRequest(new { success = false, message = "هذا الاسم مستخدم بالفعل لتصنيف آخر" });

            cat.Name = norm;
            cat.Description = dto.Description?.Trim();
            cat.DefaultCommissionType = (DiscountType)dto.DefaultCommissionType;
            cat.DefaultCommissionValue = dto.DefaultCommissionValue;
            cat.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم تعديل التصنيف بنجاح" });
        }

        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var cat = await _context.MarketerCategories.Include(c => c.Marketers).FirstOrDefaultAsync(c => c.Id == id);
            if (cat == null) return NotFound(new { success = false, message = "التصنيف غير موجود" });

            if (cat.Marketers.Any())
                return BadRequest(new { success = false, message = "لا يمكن حذف التصنيف لأنه مرتبط بمسوقين — يمكنك تعطيله بدلاً من ذلك" });

            _context.MarketerCategories.Remove(cat);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم حذف التصنيف بنجاح" });
        }

        // ==================== المسوقين ====================

        [HttpGet]
        public async Task<IActionResult> GetMarketers()
        {
            var marketers = await _context.Marketers
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.Transactions)
                .Include(m => m.DiscountCodes)
                .OrderBy(m => m.Name)
                .ToListAsync();

            var result = marketers.Select(m =>
            {
                decimal totalAccrued = m.Transactions.Where(t => t.Type == MarketerTransactionType.Accrual).Sum(t => t.Amount);
                decimal totalPaid = m.Transactions.Where(t => t.Type == MarketerTransactionType.Payout).Sum(t => t.Amount);
                decimal balance = totalAccrued - totalPaid;

                return new
                {
                    id = m.Id,
                    name = m.Name,
                    phone = m.Phone,
                    phoneNumber = m.Phone,
                    email = m.Email,
                    categoryId = m.CategoryId,
                    categoryName = m.Category.Name,
                    commissionType = (int)m.CommissionType,
                    commissionValue = m.CommissionValue,
                    discountCode = m.DiscountCodes.FirstOrDefault(c => c.IsActive)?.Code ?? m.DiscountCodes.FirstOrDefault()?.Code,
                    discountCodes = m.DiscountCodes.OrderByDescending(c => c.CreatedAt).Select(c => new
                    {
                        id = c.Id,
                        code = c.Code,
                        discountType = (int)c.DiscountType,
                        value = c.Value,
                        maxUses = c.MaxUses,
                        timesUsed = c.TimesUsed,
                        expiresAt = c.ExpiresAt,
                        isActive = c.IsActive,
                        isUsable = c.IsUsable,
                        createdAt = c.CreatedAt
                    }).ToList(),
                    isActive = m.IsActive,
                    notes = m.Notes,
                    createdAt = m.CreatedAt,
                    totalAccrued = totalAccrued,
                    totalEarned = totalAccrued,
                    totalPaid = totalPaid,
                    balance = balance,
                    currentBalance = balance,
                    activeCodesCount = m.DiscountCodes.Count(c => c.IsActive),
                    totalTimesUsed = m.DiscountCodes.Sum(c => c.TimesUsed)
                };
            }).ToList();

            return Ok(new { success = true, data = result });
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> GetLookup()
        {
            var active = await _context.Marketers
                .AsNoTracking()
                .Include(m => m.Category)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Name)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    phone = m.Phone,
                    categoryName = m.Category.Name,
                    commissionType = (int)m.CommissionType,
                    commissionValue = m.CommissionValue
                })
                .ToListAsync();

            return Ok(new { success = true, data = active });
        }

        [HttpPost]
        public async Task<IActionResult> CreateMarketer([FromBody] MarketerUpsertDto dto)
        {
            var phone = !string.IsNullOrWhiteSpace(dto.Phone) ? dto.Phone.Trim() : dto.PhoneNumber?.Trim();
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(phone))
                return BadRequest(new { success = false, message = "اسم المسوق ورقم الهاتف مطلوبان" });

            MarketerCategory? category = null;
            if (dto.CategoryId.HasValue && dto.CategoryId.Value != Guid.Empty)
            {
                category = await _context.MarketerCategories.FindAsync(dto.CategoryId.Value);
            }

            if (category == null)
            {
                category = await _context.MarketerCategories.FirstOrDefaultAsync();
                if (category == null)
                {
                    category = new MarketerCategory
                    {
                        Name = "مسوقين معتمدين",
                        Description = "التصنيف الافتراضي للمسوقين",
                        DefaultCommissionType = (DiscountType)dto.CommissionType,
                        DefaultCommissionValue = dto.CommissionValue,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.MarketerCategories.Add(category);
                    await _context.SaveChangesAsync();
                }
            }

            var marketer = new Marketer
            {
                Name = dto.Name.Trim(),
                Phone = phone,
                Email = dto.Email?.Trim(),
                CategoryId = category.Id,
                CommissionType = (DiscountType)dto.CommissionType,
                CommissionValue = dto.CommissionValue,
                IsActive = dto.IsActive,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Marketers.Add(marketer);
            await _context.SaveChangesAsync();

            // إنشاء كود الخصم وربطه بالمسوق مباشرة إذا تم إدخال كود
            if (!string.IsNullOrWhiteSpace(dto.DiscountCode))
            {
                string normCode = dto.DiscountCode.Trim().ToUpperInvariant();
                var existingCode = await _context.DiscountCodes.FirstOrDefaultAsync(d => d.Code == normCode);
                if (existingCode == null)
                {
                    var disc = new DiscountCode
                    {
                        Code = normCode,
                        MarketerId = marketer.Id,
                        DiscountType = dto.DiscountType.HasValue ? (DiscountType)dto.DiscountType.Value : DiscountType.Percentage,
                        Value = dto.DiscountValue ?? 10m,
                        CommissionType = (DiscountType)dto.CommissionType,
                        CommissionValue = dto.CommissionValue,
                        MaxUses = dto.MaxUses ?? dto.MaxUsages,
                        ExpiresAt = dto.ExpiresAt,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.DiscountCodes.Add(disc);
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { success = true, message = "تم إنشاء المسوق وكود الخصم بنجاح", data = marketer.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMarketer(Guid id, [FromBody] MarketerUpsertDto dto)
        {
            var marketer = await _context.Marketers
                .Include(m => m.DiscountCodes)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (marketer == null) return NotFound(new { success = false, message = "المسوق غير موجود" });

            if (!string.IsNullOrWhiteSpace(dto.Name)) marketer.Name = dto.Name.Trim();
            var phone = !string.IsNullOrWhiteSpace(dto.Phone) ? dto.Phone.Trim() : dto.PhoneNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(phone)) marketer.Phone = phone;
            marketer.Email = dto.Email?.Trim();
            if (dto.CategoryId.HasValue && dto.CategoryId.Value != Guid.Empty) marketer.CategoryId = dto.CategoryId.Value;
            marketer.CommissionType = (DiscountType)dto.CommissionType;
            marketer.CommissionValue = dto.CommissionValue;
            marketer.IsActive = dto.IsActive;
            marketer.Notes = dto.Notes?.Trim();

            // إذا تم تزويد كود خصم جديد أثناء التعديل
            if (!string.IsNullOrWhiteSpace(dto.DiscountCode))
            {
                string normCode = dto.DiscountCode.Trim().ToUpperInvariant();
                var existsOther = await _context.DiscountCodes.AnyAsync(d => d.Code == normCode && d.MarketerId != id);
                if (existsOther)
                {
                    return BadRequest(new { success = false, message = "كود الخصم مستخدم بالفعل لمسوق آخر" });
                }

                // خيار حذف الأكواد القديمة
                if (dto.DeleteOldCodes)
                {
                    foreach (var oldCode in marketer.DiscountCodes.ToList())
                    {
                        var txs = await _context.MarketerTransactions.Where(t => t.DiscountCodeId == oldCode.Id).ToListAsync();
                        foreach (var tx in txs) tx.DiscountCodeId = null;
                        _context.DiscountCodes.Remove(oldCode);
                    }
                }
                else if (dto.DeactivateOldCodes)
                {
                    foreach (var oldCode in marketer.DiscountCodes)
                    {
                        oldCode.IsActive = false;
                    }
                }

                // التحقق هل الكود موجود مسبقاً لنفس المسوق أم جديد
                var sameCode = marketer.DiscountCodes.FirstOrDefault(d => d.Code == normCode);
                if (sameCode != null)
                {
                    sameCode.IsActive = true;
                    if (dto.DiscountType.HasValue) sameCode.DiscountType = (DiscountType)dto.DiscountType.Value;
                    if (dto.DiscountValue.HasValue) sameCode.Value = dto.DiscountValue.Value;
                    sameCode.CommissionType = (DiscountType)dto.CommissionType;
                    sameCode.CommissionValue = dto.CommissionValue;
                    sameCode.MaxUses = dto.MaxUses ?? dto.MaxUsages;
                    sameCode.ExpiresAt = dto.ExpiresAt;
                }
                else
                {
                    var newCode = new DiscountCode
                    {
                        Code = normCode,
                        MarketerId = marketer.Id,
                        DiscountType = dto.DiscountType.HasValue ? (DiscountType)dto.DiscountType.Value : DiscountType.Percentage,
                        Value = dto.DiscountValue ?? 10m,
                        CommissionType = (DiscountType)dto.CommissionType,
                        CommissionValue = dto.CommissionValue,
                        MaxUses = dto.MaxUses ?? dto.MaxUsages,
                        ExpiresAt = dto.ExpiresAt,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.DiscountCodes.Add(newCode);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم تعديل بيانات المسوق بنجاح" });
        }

        /// <summary>
        /// إضافة كود خصم جديد لمسوق موجود مع إمكانية حذف الكود القديم أو الاحتفاظ به
        /// </summary>
        [HttpPost("{id}/codes")]
        public async Task<IActionResult> AddCodeToMarketer(Guid id, [FromBody] MarketerNewCodeDto dto)
        {
            var marketer = await _context.Marketers
                .Include(m => m.DiscountCodes)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marketer == null) return NotFound(new { success = false, message = "المسوق غير موجود" });

            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { success = false, message = "كود الخصم مطلوب" });

            string normCode = dto.Code.Trim().ToUpperInvariant();
            if (await _context.DiscountCodes.AnyAsync(d => d.Code == normCode))
                return BadRequest(new { success = false, message = "هذا الكود مستخدم بالفعل، يرجى اختيار كود آخر" });

            // خيار حذف الأكواد القديمة
            if (dto.DeleteOldCodes)
            {
                foreach (var oldCode in marketer.DiscountCodes.ToList())
                {
                    var txs = await _context.MarketerTransactions.Where(t => t.DiscountCodeId == oldCode.Id).ToListAsync();
                    foreach (var tx in txs) tx.DiscountCodeId = null;
                    _context.DiscountCodes.Remove(oldCode);
                }
            }
            else if (dto.DeactivateOldCodes)
            {
                foreach (var oldCode in marketer.DiscountCodes)
                {
                    oldCode.IsActive = false;
                }
            }

            var disc = new DiscountCode
            {
                Code = normCode,
                MarketerId = marketer.Id,
                DiscountType = (DiscountType)dto.DiscountType,
                Value = dto.DiscountValue,
                CommissionType = (DiscountType)(dto.CommissionType ?? (int)marketer.CommissionType),
                CommissionValue = dto.CommissionValue ?? marketer.CommissionValue,
                MaxUses = dto.MaxUses,
                ExpiresAt = dto.ExpiresAt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.DiscountCodes.Add(disc);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم إضافة كود الخصم للمسوق بنجاح", data = disc.Id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMarketer(Guid id)
        {
            var marketer = await _context.Marketers
                .Include(m => m.DiscountCodes)
                .Include(m => m.Transactions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marketer == null) return NotFound(new { success = false, message = "المسوق غير موجود" });

            // 1. فك ارتباط الكود بالحركات المسجلة لكي لا يعترض قيود الـ FK
            foreach (var tx in marketer.Transactions)
            {
                tx.DiscountCodeId = null;
            }

            var codeIds = marketer.DiscountCodes.Select(c => c.Id).ToList();
            if (codeIds.Any())
            {
                var otherTxs = await _context.MarketerTransactions
                    .Where(t => t.DiscountCodeId.HasValue && codeIds.Contains(t.DiscountCodeId.Value))
                    .ToListAsync();
                foreach (var tx in otherTxs)
                {
                    tx.DiscountCodeId = null;
                }
            }

            // 2. حذف الحركات وأكواد الخصم والمسوق
            _context.MarketerTransactions.RemoveRange(marketer.Transactions);
            _context.DiscountCodes.RemoveRange(marketer.DiscountCodes);
            _context.Marketers.Remove(marketer);

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم حذف المسوق وجميع بياناته بنجاح" });
        }

        /// <summary>
        /// تسجيل سداد مستحقات للمسوق (Payout)
        /// قرار معتمد: غير مقيد بالرصيد ويسمح بالرصيد السالب كدفعة مقدمة
        /// </summary>
        [HttpPost("{id}/payout")]
        public async Task<IActionResult> RecordPayout(Guid id, [FromBody] MarketerPayoutRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest(new { success = false, message = "مبلغ السداد يجب أن يكون أكبر من صفر" });

            var marketer = await _context.Marketers.FindAsync(id);
            if (marketer == null) return NotFound(new { success = false, message = "المسوق غير موجود" });

            var payout = new MarketerTransaction
            {
                MarketerId = id,
                Type = MarketerTransactionType.Payout,
                Amount = request.Amount,
                ReferenceNumber = request.ReferenceNumber?.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? $"سداد مستحقات للمسوق {marketer.Name}" : request.Notes.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.MarketerTransactions.Add(payout);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم تسجيل حركة السداد بنجاح" });
        }

        /// <summary>
        /// كشف حساب تفصيلي للمسوق (Statement of Account) مع الرصيد الجاري
        /// </summary>
        [HttpGet("{id}/statement")]
        public async Task<IActionResult> GetStatement(Guid id, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var marketer = await _context.Marketers
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (marketer == null) return NotFound(new { success = false, message = "المسوق غير موجود" });

            // 1. الرصيد الافتتاحي (قبل fromDate)
            decimal openingBalance = 0;
            if (fromDate.HasValue)
            {
                var pastTxs = await _context.MarketerTransactions
                    .Where(t => t.MarketerId == id && t.CreatedAt < fromDate.Value)
                    .ToListAsync();

                decimal pastAccruals = pastTxs.Where(t => t.Type == MarketerTransactionType.Accrual).Sum(t => t.Amount);
                decimal pastPayouts = pastTxs.Where(t => t.Type == MarketerTransactionType.Payout).Sum(t => t.Amount);
                openingBalance = pastAccruals - pastPayouts;
            }

            // 2. الحركات داخل الفترة
            var txQuery = _context.MarketerTransactions
                .Where(t => t.MarketerId == id);

            if (fromDate.HasValue) txQuery = txQuery.Where(t => t.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date.AddDays(1);
                txQuery = txQuery.Where(t => t.CreatedAt < endOfDay);
            }

            var periodTxs = await txQuery.OrderBy(t => t.CreatedAt).ToListAsync();

            decimal runningBalance = openingBalance;
            decimal totalAccrued = 0;
            decimal totalPaid = 0;

            var rows = new List<object>();
            foreach (var t in periodTxs)
            {
                decimal accrualAmount = t.Type == MarketerTransactionType.Accrual ? t.Amount : 0;
                decimal payoutAmount = t.Type == MarketerTransactionType.Payout ? t.Amount : 0;

                totalAccrued += accrualAmount;
                totalPaid += payoutAmount;
                runningBalance += (accrualAmount - payoutAmount);

                rows.Add(new
                {
                    transactionId = t.Id,
                    date = t.CreatedAt,
                    type = (int)t.Type,
                    typeLabel = t.Type == MarketerTransactionType.Accrual ? "استحقاق عمولة" : "سداد مستحقات",
                    userName = t.UserName,
                    discountCodeText = t.DiscountCodeText,
                    planName = t.PlanName,
                    subscriptionAmount = t.SubscriptionAmount,
                    accrualAmount = accrualAmount,
                    payoutAmount = payoutAmount,
                    runningBalance = runningBalance,
                    referenceNumber = t.ReferenceNumber,
                    notes = t.Notes
                });
            }

            return Ok(new
            {
                success = true,
                marketerId = marketer.Id,
                marketerName = marketer.Name,
                marketerPhone = marketer.Phone,
                categoryName = marketer.Category.Name,
                fromDate = fromDate,
                toDate = toDate,
                openingBalance = openingBalance,
                rows = rows,
                totalAccrued = totalAccrued,
                totalPaid = totalPaid,
                netPeriodChange = totalAccrued - totalPaid,
                closingBalance = runningBalance
            });
        }
    }

    public class CategoryUpsertDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DefaultCommissionType { get; set; } = 0;
        public decimal DefaultCommissionValue { get; set; } = 10;
        public bool IsActive { get; set; } = true;
    }

    public class MarketerUpsertDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public Guid? CategoryId { get; set; }
        public int CommissionType { get; set; } = 0;
        public decimal CommissionValue { get; set; } = 10;
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }

        public string? DiscountCode { get; set; }
        public int? DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public int? MaxUses { get; set; }
        public int? MaxUsages { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool DeleteOldCodes { get; set; } = false;
        public bool DeactivateOldCodes { get; set; } = false;
    }

    public class MarketerNewCodeDto
    {
        public string Code { get; set; } = string.Empty;
        public int DiscountType { get; set; } = 0;
        public decimal DiscountValue { get; set; } = 10;
        public int? CommissionType { get; set; }
        public decimal? CommissionValue { get; set; }
        public int? MaxUses { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool DeleteOldCodes { get; set; } = false;
        public bool DeactivateOldCodes { get; set; } = false;
    }

    public class MarketerPayoutRequest
    {
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
    }
}
