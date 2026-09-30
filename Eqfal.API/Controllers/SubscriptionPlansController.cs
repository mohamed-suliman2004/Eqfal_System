using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Eqfal.API.Helpers;
using Eqfal.API.Models;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    [Route("subscription-plans")]
    [Route("api/subscription-plans")]
    public class SubscriptionPlansController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubscriptionPlansController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// للزوار وموقع الهبوط (Anonymous)
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicCatalog()
        {
            var trialPlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IsTrialPlan);

            int trialDays = trialPlan?.TrialDurationDays ?? 7;

            var activePlans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Include(p => p.Prices.Where(pr => pr.IsActive))
                .Include(p => p.Features.OrderBy(f => f.SortOrder))
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.IsTrialPlan)
                .ThenBy(p => p.SortOrder)
                .ThenBy(p => p.CreatedAt)
                .ToListAsync();

            var plansDto = activePlans.Select(p =>
            {
                var monthlyPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.Monthly)?.Price;
                var quarterlyPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.Quarterly)?.Price;
                var semiAnnualPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.SemiAnnual)?.Price;
                var yearlyPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.Yearly)?.Price;

                return new
                {
                    id = p.Id,
                    name = p.Name,
                    nameAr = p.NameAr,
                    descriptionAr = p.DescriptionAr,
                    themeKey = p.ThemeKey,
                    isPopular = p.IsPopular,
                    isTrial = p.IsTrialPlan,
                    trialDurationDays = p.TrialDurationDays,
                    sortOrder = p.SortOrder,
                    features = p.Features.OrderBy(f => f.SortOrder).Select(f => f.Text).ToList(),
                    prices = new
                    {
                        monthly = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.Monthly).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault(),
                        quarterly = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.Quarterly).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault(),
                        semiAnnual = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.SemiAnnual).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault(),
                        yearly = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.Yearly).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault()
                    },
                    savings = new
                    {
                        quarterlySavingPercent = SubscriptionStateHelper.CalculateSavingPercent(monthlyPrice, quarterlyPrice, BillingCycle.Quarterly),
                        semiAnnualSavingPercent = SubscriptionStateHelper.CalculateSavingPercent(monthlyPrice, semiAnnualPrice, BillingCycle.SemiAnnual),
                        yearlySavingPercent = SubscriptionStateHelper.CalculateSavingPercent(monthlyPrice, yearlyPrice, BillingCycle.Yearly)
                    }
                };
            }).ToList();

            int? maxYearlySaving = plansDto
                .Select(p => p.savings.yearlySavingPercent)
                .Where(s => s.HasValue)
                .Max();

            return Ok(new
            {
                success = true,
                trialDays = trialDays,
                maxYearlySavingPercent = maxYearlySaving,
                plans = plansDto
            });
        }

        /// <summary>
        /// للتطبيق للمشتركين المسجلين (Active Non-Trial Plans)
        /// </summary>
        [HttpGet("active")]
        [Authorize]
        public async Task<IActionResult> GetActiveCatalog()
        {
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Include(p => p.Prices.Where(pr => pr.IsActive))
                .Include(p => p.Features.OrderBy(f => f.SortOrder))
                .Where(p => p.IsActive && !p.IsTrialPlan)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.CreatedAt)
                .ToListAsync();

            var plansDto = plans.Select(p =>
            {
                var monthlyPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.Monthly)?.Price;
                var quarterlyPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.Quarterly)?.Price;
                var semiAnnualPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.SemiAnnual)?.Price;
                var yearlyPrice = p.Prices.FirstOrDefault(pr => pr.BillingCycle == BillingCycle.Yearly)?.Price;

                return new
                {
                    id = p.Id,
                    name = p.Name,
                    nameAr = p.NameAr,
                    descriptionAr = p.DescriptionAr,
                    themeKey = p.ThemeKey,
                    isPopular = p.IsPopular,
                    sortOrder = p.SortOrder,
                    features = p.Features.OrderBy(f => f.SortOrder).Select(f => f.Text).ToList(),
                    prices = new
                    {
                        monthly = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.Monthly).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault(),
                        quarterly = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.Quarterly).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault(),
                        semiAnnual = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.SemiAnnual).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault(),
                        yearly = p.Prices.Where(pr => pr.BillingCycle == BillingCycle.Yearly).Select(pr => new { id = pr.Id, price = pr.Price, durationDays = pr.DurationDays }).FirstOrDefault()
                    },
                    savings = new
                    {
                        quarterlySavingPercent = SubscriptionStateHelper.CalculateSavingPercent(monthlyPrice, quarterlyPrice, BillingCycle.Quarterly),
                        semiAnnualSavingPercent = SubscriptionStateHelper.CalculateSavingPercent(monthlyPrice, semiAnnualPrice, BillingCycle.SemiAnnual),
                        yearlySavingPercent = SubscriptionStateHelper.CalculateSavingPercent(monthlyPrice, yearlyPrice, BillingCycle.Yearly)
                    }
                };
            }).ToList();

            int? maxYearlySaving = plansDto
                .Select(p => p.savings.yearlySavingPercent)
                .Where(s => s.HasValue)
                .Max();

            return Ok(new
            {
                success = true,
                maxYearlySavingPercent = maxYearlySaving,
                plans = plansDto
            });
        }

        /// <summary>
        /// للوحة الإدارة (جميع الخطط بالتفصيل)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _context.SubscriptionPlans
                .AsNoTracking()
                .Include(p => p.Prices)
                .Include(p => p.Features)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.CreatedAt)
                .ToListAsync();

            var now = DateTime.UtcNow;

            var result = new List<object>();
            foreach (var p in plans)
            {
                int subCount = await _context.Subscriptions.CountAsync(s => s.PlanId == p.Id);
                int activeSubCount = await _context.Subscriptions.CountAsync(s => s.PlanId == p.Id && s.ExpiresAt >= now);

                result.Add(new
                {
                    id = p.Id,
                    name = p.Name,
                    nameAr = p.NameAr,
                    descriptionAr = p.DescriptionAr,
                    isTrialPlan = p.IsTrialPlan,
                    trialDurationDays = p.TrialDurationDays,
                    isPopular = p.IsPopular,
                    isActive = p.IsActive,
                    sortOrder = p.SortOrder,
                    themeKey = p.ThemeKey,
                    createdAt = p.CreatedAt,
                    subscriptionsCount = subCount,
                    affectedActiveSubscribers = activeSubCount,
                    features = p.Features.OrderBy(f => f.SortOrder).Select(f => f.Text).ToList(),
                    prices = p.Prices.Select(pr => new
                    {
                        id = pr.Id,
                        billingCycle = (int)pr.BillingCycle,
                        billingCycleName = pr.BillingCycle.ToString(),
                        price = pr.Price,
                        durationDays = pr.DurationDays,
                        isActive = pr.IsActive
                    }).ToList()
                });
            }

            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// إنشاء خطة جديدة (Admin)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePlan([FromBody] PlanUpsertRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.NameAr))
                return BadRequest(new { success = false, message = "اسم الخطة بالعربي والإنجليزي مطلوب" });

            if (request.IsTrialPlan)
            {
                if (!request.TrialDurationDays.HasValue || request.TrialDurationDays.Value < 1)
                    return BadRequest(new { success = false, message = "مدة التجربة يجب أن تكون يوماً واحداً على الأقل" });

                // إلغاء صفة التجربة عن باقي الخطط في نفس المعاملة
                var existingTrials = await _context.SubscriptionPlans.Where(p => p.IsTrialPlan).ToListAsync();
                foreach (var t in existingTrials) t.IsTrialPlan = false;
            }

            if (request.IsPopular)
            {
                var existingPopular = await _context.SubscriptionPlans.Where(p => p.IsPopular).ToListAsync();
                foreach (var pop in existingPopular) pop.IsPopular = false;
            }

            var plan = new SubscriptionPlan
            {
                Name = request.Name.Trim(),
                NameAr = request.NameAr.Trim(),
                DescriptionAr = request.DescriptionAr?.Trim(),
                IsTrialPlan = request.IsTrialPlan,
                TrialDurationDays = request.IsTrialPlan ? request.TrialDurationDays : null,
                IsPopular = request.IsPopular,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                ThemeKey = string.IsNullOrWhiteSpace(request.ThemeKey) ? "blue" : request.ThemeKey.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            if (request.Features != null)
            {
                int order = 1;
                foreach (var feat in request.Features.Where(f => !string.IsNullOrWhiteSpace(f)))
                {
                    plan.Features.Add(new SubscriptionPlanFeature { Text = feat.Trim(), SortOrder = order++ });
                }
            }

            if (!request.IsTrialPlan && request.Prices != null)
            {
                foreach (var pr in request.Prices)
                {
                    plan.Prices.Add(new SubscriptionPlanPrice
                    {
                        BillingCycle = (BillingCycle)pr.BillingCycle,
                        Price = pr.Price,
                        DurationDays = pr.DurationDays,
                        IsActive = pr.IsActive
                    });
                }
            }

            _context.SubscriptionPlans.Add(plan);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم إنشاء الخطة بنجاح", data = plan.Id });
        }

        /// <summary>
        /// تعديل الخطة والأسعار (Admin)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] PlanUpsertRequest request)
        {
            var plan = await _context.SubscriptionPlans
                .Include(p => p.Prices)
                .Include(p => p.Features)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (plan == null) return NotFound(new { success = false, message = "الخطة غير موجودة" });

            if (request.IsTrialPlan && !plan.IsTrialPlan)
            {
                var existingTrials = await _context.SubscriptionPlans.Where(p => p.IsTrialPlan && p.Id != id).ToListAsync();
                foreach (var t in existingTrials) t.IsTrialPlan = false;
            }

            if (request.IsPopular && !plan.IsPopular)
            {
                var existingPopular = await _context.SubscriptionPlans.Where(p => p.IsPopular && p.Id != id).ToListAsync();
                foreach (var pop in existingPopular) pop.IsPopular = false;
            }

            plan.Name = request.Name.Trim();
            plan.NameAr = request.NameAr.Trim();
            plan.DescriptionAr = request.DescriptionAr?.Trim();
            plan.IsTrialPlan = request.IsTrialPlan;
            plan.TrialDurationDays = request.IsTrialPlan ? request.TrialDurationDays : null;
            plan.IsPopular = request.IsPopular;
            plan.IsActive = request.IsActive;
            plan.SortOrder = request.SortOrder;
            plan.ThemeKey = string.IsNullOrWhiteSpace(request.ThemeKey) ? "blue" : request.ThemeKey.Trim();
            plan.UpdatedAt = DateTime.UtcNow;

            // تحديث المزايا
            if (plan.Features.Any())
            {
                _context.SubscriptionPlanFeatures.RemoveRange(plan.Features);
                plan.Features.Clear();
            }

            if (request.Features != null)
            {
                int order = 1;
                foreach (var feat in request.Features.Where(f => !string.IsNullOrWhiteSpace(f)))
                {
                    var newFeat = new SubscriptionPlanFeature
                    {
                        Id = Guid.NewGuid(),
                        PlanId = plan.Id,
                        Text = feat.Trim(),
                        SortOrder = order++
                    };
                    _context.SubscriptionPlanFeatures.Add(newFeat);
                }
            }

            // تحديث الأسعار بطريقة Upsert للدورات
            if (!request.IsTrialPlan && request.Prices != null)
            {
                var incomingCycles = request.Prices.Select(pr => (BillingCycle)pr.BillingCycle).ToHashSet();

                // تعطيل الأسعار الموجودة التي لم تأتِ في الطلب
                foreach (var existingPrice in plan.Prices)
                {
                    if (incomingCycles.Contains(existingPrice.BillingCycle))
                    {
                        var incoming = request.Prices.First(pr => (BillingCycle)pr.BillingCycle == existingPrice.BillingCycle);
                        existingPrice.Price = incoming.Price;
                        existingPrice.DurationDays = incoming.DurationDays;
                        existingPrice.IsActive = incoming.IsActive;
                    }
                    else
                    {
                        existingPrice.IsActive = false;
                    }
                }

                // إضافة دورات جديدة إن وجدت
                var existingCycles = plan.Prices.Select(p => p.BillingCycle).ToHashSet();
                foreach (var incoming in request.Prices)
                {
                    var cycle = (BillingCycle)incoming.BillingCycle;
                    if (!existingCycles.Contains(cycle))
                    {
                        var newPrice = new SubscriptionPlanPrice
                        {
                            Id = Guid.NewGuid(),
                            PlanId = plan.Id,
                            BillingCycle = cycle,
                            Price = incoming.Price,
                            DurationDays = incoming.DurationDays,
                            IsActive = incoming.IsActive
                        };
                        _context.SubscriptionPlanPrices.Add(newPrice);
                    }
                }
            }
            else if (request.IsTrialPlan)
            {
                foreach (var price in plan.Prices)
                {
                    price.IsActive = false;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم تعديل الخطة بنجاح" });
        }

        /// <summary>
        /// حذف الخطة (ممنوع لو مرتبطة باشتراكات أو دفعات)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(Guid id)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(id);
            if (plan == null) return NotFound(new { success = false, message = "الخطة غير موجودة" });

            bool hasSubs = await _context.Subscriptions.AnyAsync(s => s.PlanId == id);
            bool hasPayments = await _context.SubscriptionPayments.AnyAsync(p => p.PlanId == id);

            if (hasSubs || hasPayments)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "لا يمكن حذف خطة مرتبطة باشتراكات أو دفعات — يمكنك تعطيلها بدلاً من ذلك."
                });
            }

            _context.SubscriptionPlans.Remove(plan);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم حذف الخطة بنجاح" });
        }
    }

    public class PlanUpsertRequest
    {
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? DescriptionAr { get; set; }
        public bool IsTrialPlan { get; set; }
        public int? TrialDurationDays { get; set; }
        public bool IsPopular { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public string ThemeKey { get; set; } = "blue";
        public List<string>? Features { get; set; }
        public List<PlanPriceDto>? Prices { get; set; }
    }

    public class PlanPriceDto
    {
        public int BillingCycle { get; set; }
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
