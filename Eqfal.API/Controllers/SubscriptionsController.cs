using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Eqfal.API.Helpers;
using Eqfal.API.Models;
using Eqfal.API.Services;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class SubscriptionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ISubscriptionRenewalService _renewalService;

        public SubscriptionsController(AppDbContext context, ISubscriptionRenewalService renewalService)
        {
            _context = context;
            _renewalService = renewalService;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("nameid")?.Value
                     ?? User.FindFirst("sub")?.Value;

            return int.TryParse(claim, out int id) ? id : 0;
        }

        /// <summary>
        /// الحصول على حالة اشتراك التاجر الحالية
        /// </summary>
        [HttpGet("current")]
        [Authorize]
        public async Task<IActionResult> GetCurrentSubscription()
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var now = DateTime.UtcNow;

            var settings = await _context.SubscriptionSettings.AsNoTracking().FirstOrDefaultAsync();
            int graceDays = settings?.GracePeriodDays ?? 3;

            var sub = await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefaultAsync();

            if (sub == null)
            {
                return Ok(new
                {
                    success = true,
                    status = "None",
                    planId = (Guid?)null,
                    planPriceId = (Guid?)null,
                    billingCycle = (int?)null,
                    planType = "لا يوجد اشتراك",
                    isTrial = false,
                    startedAt = (DateTime?)null,
                    expiresAt = (DateTime?)null,
                    graceUntil = (DateTime?)null,
                    daysRemaining = 0
                });
            }

            string status = SubscriptionStateHelper.ComputeStatus(sub.ExpiresAt, sub.IsTrial, now, graceDays);
            var graceUntil = SubscriptionStateHelper.ComputeGraceUntil(sub.ExpiresAt, graceDays);
            int daysRemaining = SubscriptionStateHelper.ComputeDaysRemaining(sub.ExpiresAt, now);

            return Ok(new
            {
                success = true,
                status = status,
                planId = sub.PlanId,
                planPriceId = sub.PlanPriceId,
                billingCycle = (int?)sub.BillingCycle,
                planType = sub.PlanType,
                isTrial = sub.IsTrial,
                startedAt = sub.StartedAt,
                expiresAt = sub.ExpiresAt,
                graceUntil = graceUntil,
                daysRemaining = daysRemaining
            });
        }

        /// <summary>
        /// قائمة المشتركين في لوحة الإدارة
        /// </summary>
        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminSubscriptions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 500,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null)
        {
            var now = DateTime.UtcNow;
            var settings = await _context.SubscriptionSettings.AsNoTracking().FirstOrDefaultAsync();
            int graceDays = settings?.GracePeriodDays ?? 3;

            var usersQuery = _context.Users
                .AsNoTracking()
                .Include(u => u.Subscriptions)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                usersQuery = usersQuery.Where(u => u.FullName.Contains(s) || u.UsernameEmail.Contains(s) || (u.Phone != null && u.Phone.Contains(s)));
            }

            var allUsers = await usersQuery.ToListAsync();

            var mappedList = allUsers.Select(u =>
            {
                var latestSub = u.Subscriptions.OrderByDescending(s => s.ExpiresAt).FirstOrDefault();
                string userStatus = latestSub != null
                    ? SubscriptionStateHelper.ComputeStatus(latestSub.ExpiresAt, latestSub.IsTrial, now, graceDays)
                    : "None";

                return new
                {
                    userId = u.Id,
                    fullName = u.FullName,
                    phone = u.Phone,
                    email = u.UsernameEmail,
                    isActive = u.IsActive,
                    suspensionReason = u.SuspensionReason?.ToString(),
                    subscriptionId = latestSub?.Id,
                    planId = latestSub?.PlanId,
                    planPriceId = latestSub?.PlanPriceId,
                    billingCycle = (int?)latestSub?.BillingCycle,
                    planType = latestSub?.PlanType ?? "لا يوجد",
                    isTrial = latestSub?.IsTrial ?? false,
                    startedAt = latestSub?.StartedAt,
                    expiresAt = latestSub?.ExpiresAt,
                    daysRemaining = latestSub != null ? SubscriptionStateHelper.ComputeDaysRemaining(latestSub.ExpiresAt, now) : 0,
                    status = userStatus
                };
            })
            .OrderByDescending(m => m.status == "Active")
            .ThenByDescending(m => m.expiresAt)
            .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                mappedList = mappedList.Where(m => m.status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            int totalCount = mappedList.Count();
            var paged = mappedList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new
            {
                success = true,
                totalCount = totalCount,
                page = page,
                pageSize = pageSize,
                data = paged
            });
        }

        /// <summary>
        /// إحصائيات الاشتراكات في لوحة الإدارة
        /// </summary>
        [HttpGet("admin/stats")]
        public async Task<IActionResult> GetAdminStats()
        {
            var now = DateTime.UtcNow;
            var settings = await _context.SubscriptionSettings.AsNoTracking().FirstOrDefaultAsync();
            int graceDays = settings?.GracePeriodDays ?? 3;

            var users = await _context.Users
                .AsNoTracking()
                .Include(u => u.Subscriptions)
                .ToListAsync();

            int total = users.Count;
            int trial = 0, active = 0, grace = 0, expired = 0;

            foreach (var u in users)
            {
                var sub = u.Subscriptions.OrderByDescending(s => s.ExpiresAt).FirstOrDefault();
                if (sub == null)
                {
                    expired++;
                    continue;
                }

                string status = SubscriptionStateHelper.ComputeStatus(sub.ExpiresAt, sub.IsTrial, now, graceDays);
                switch (status)
                {
                    case "Trial": trial++; break;
                    case "Active": active++; break;
                    case "Grace": grace++; break;
                    case "Expired": expired++; break;
                }
            }

            return Ok(new
            {
                success = true,
                total = total,
                trial = trial,
                active = active,
                grace = grace,
                expired = expired
            });
        }

        /// <summary>
        /// تمديد يدوي أو تغيير خطة من لوحة الأدمن
        /// </summary>
        [HttpPut("{id}/extend")]
        public async Task<IActionResult> ExtendSubscription(int id, [FromBody] ExtendSubscriptionRequest request)
        {
            try
            {
                var sub = await _renewalService.ApplyManualExtensionAsync(id, request.Days, request.PlanPriceId, request.DiscountCode);
                return Ok(new { success = true, message = "تم تمديد وتحديث الاشتراك بنجاح", expiresAt = sub.ExpiresAt });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// إعدادات فترة السماح
        /// </summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _context.SubscriptionSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SubscriptionSettings { GracePeriodDays = 3 };
                _context.SubscriptionSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, gracePeriodDays = settings.GracePeriodDays });
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] SubscriptionSettingsDto dto)
        {
            if (dto.GracePeriodDays < 0)
                return BadRequest(new { success = false, message = "فترة السماح لا يمكن أن تكون بالسالب" });

            var settings = await _context.SubscriptionSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SubscriptionSettings { GracePeriodDays = dto.GracePeriodDays };
                _context.SubscriptionSettings.Add(settings);
            }
            else
            {
                settings.GracePeriodDays = dto.GracePeriodDays;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "تم حفظ إعدادات فترة السماح بنجاح", gracePeriodDays = settings.GracePeriodDays });
        }
    }

    public class ExtendSubscriptionRequest
    {
        public int Days { get; set; } = 30;
        public Guid? PlanPriceId { get; set; }
        public string? DiscountCode { get; set; }
    }

    public class SubscriptionSettingsDto
    {
        public int GracePeriodDays { get; set; }
    }
}
