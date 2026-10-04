using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Eqfal.API.Data;
using Eqfal.API.Helpers;
using Eqfal.API.Hubs;
using Eqfal.API.Models;

namespace Eqfal.API.Services
{
    public interface ISubscriptionRenewalService
    {
        Task<bool> ProcessSettledPaymentAsync(Guid paymentId, decimal? finalAmount = null);
        Task<Subscription> ApplyManualExtensionAsync(int userId, int days, Guid? planPriceId = null, string? discountCodeText = null);
    }

    public class SubscriptionRenewalService : ISubscriptionRenewalService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OperationsHub> _hubContext;
        private readonly ILogger<SubscriptionRenewalService> _logger;

        public SubscriptionRenewalService(
            AppDbContext context,
            IHubContext<OperationsHub> hubContext,
            ILogger<SubscriptionRenewalService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// معالجة سداد الفاتورة الذري المانع للتكرار (ProcessSettled)
        /// </summary>
        public async Task<bool> ProcessSettledPaymentAsync(Guid paymentId, decimal? finalAmount = null)
        {
            // 1. انتقال ذري للحالة لمنع تكرار العملية والسباق بين الـ Webhook والـ Polling
            // نبحث عن الدفعة بحالة Pending فقط
            var payment = await _context.SubscriptionPayments
                .Include(p => p.Plan)
                .Include(p => p.PlanPrice)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                _logger.LogWarning("[RenewalService] Payment {Id} not found", paymentId);
                return false;
            }

            if (payment.Status == "Settled")
            {
                _logger.LogInformation("[RenewalService] Payment {Id} is already Settled, skipping duplicate execution", paymentId);
                return true;
            }

            if (payment.Status != "Pending" && payment.Status != "Processing")
            {
                _logger.LogWarning("[RenewalService] Payment {Id} cannot be settled from status {Status}", paymentId, payment.Status);
                return false;
            }

            // علّم الدفعة أنها قيد المعالجة
            payment.Status = "Processing";
            await _context.SaveChangesAsync();

            var now = DateTime.UtcNow;

            // جلب أحدث اشتراك للمستخدم
            var latestSub = await _context.Subscriptions
                .Where(s => s.UserId == payment.UserId)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefaultAsync();

            var price = payment.PlanPrice ?? await _context.SubscriptionPlanPrices
                .Include(pr => pr.Plan)
                .FirstOrDefaultAsync(pr => pr.Id == payment.PlanPriceId);

            if (price == null)
            {
                _logger.LogError("[RenewalService] PlanPrice not found for payment {PaymentId}", paymentId);
                payment.Status = "Settled";
                payment.CompletedAt = now;
                await _context.SaveChangesAsync();
                return true;
            }

            // تحديد قاعدة D6 للتمديد أو التغيير
            bool isOngoing = latestSub != null && now <= latestSub.ExpiresAt && !latestSub.IsTrial;
            bool isSamePrice = latestSub != null && latestSub.PlanPriceId == price.Id;

            bool startsNow = payment.ReplacesActivePlan || !isOngoing || !isSamePrice;
            DateTime baseDate = startsNow ? now : (latestSub?.ExpiresAt ?? now);

            if (latestSub == null)
            {
                latestSub = new Subscription
                {
                    UserId = payment.UserId,
                    PlanId = price.PlanId,
                    PlanType = price.Plan.NameAr,
                    PlanPriceId = price.Id,
                    BillingCycle = price.BillingCycle,
                    PriceSnapshot = finalAmount ?? payment.Amount,
                    IsTrial = false,
                    StartedAt = now,
                    ExpiresAt = baseDate.AddDays(price.DurationDays),
                    CreatedAt = now
                };
                _context.Subscriptions.Add(latestSub);
            }
            else
            {
                latestSub.PlanId = price.PlanId;
                latestSub.PlanType = price.Plan.NameAr;
                latestSub.PlanPriceId = price.Id;
                latestSub.BillingCycle = price.BillingCycle;
                latestSub.PriceSnapshot = finalAmount ?? payment.Amount;
                latestSub.IsTrial = false;
                latestSub.ReminderSentAt = null;
                latestSub.ExpiresAt = baseDate.AddDays(price.DurationDays);

                if (startsNow)
                {
                    latestSub.StartedAt = now;
                }
            }

            // استهلاك كود الخصم وتسجيل استحقاق المسوق
            if (!string.IsNullOrWhiteSpace(payment.DiscountCode))
            {
                var normCode = payment.DiscountCode.Trim().ToUpperInvariant();
                var discountCode = await _context.DiscountCodes
                    .Include(d => d.Marketer)
                    .FirstOrDefaultAsync(d => d.Code == normCode);

                if (discountCode != null)
                {
                    discountCode.TimesUsed++;

                    // حساب عمولة المسوق
                    decimal baseAmount = payment.Amount;
                    decimal commission = discountCode.CommissionType == DiscountType.Percentage
                        ? Math.Round(baseAmount * discountCode.CommissionValue / 100m, 2, MidpointRounding.AwayFromZero)
                        : discountCode.CommissionValue;

                    if (commission > 0)
                    {
                        var transaction = new MarketerTransaction
                        {
                            MarketerId = discountCode.MarketerId,
                            Type = MarketerTransactionType.Accrual,
                            Amount = commission,
                            UserId = payment.UserId,
                            UserName = payment.User?.FullName,
                            SubscriptionPaymentId = payment.Id,
                            DiscountCodeId = discountCode.Id,
                            DiscountCodeText = discountCode.Code,
                            PlanName = price.Plan.NameAr,
                            SubscriptionAmount = baseAmount,
                            Notes = $"استحقاق عمولة اشتراك التاجر {payment.User?.FullName ?? ""} بخطة {price.Plan.NameAr}",
                            CreatedAt = now
                        };
                        _context.MarketerTransactions.Add(transaction);
                    }
                }
            }

            payment.Status = "Settled";
            payment.CompletedAt = now;

            // إعادة تفعيل الحساب التلقائي (R-REACT-1) إذا كان موقوفاً لانتهاء الاشتراك
            var user = payment.User ?? await _context.Users.FindAsync(payment.UserId);
            bool accountReactivated = false;
            if (user != null && (!user.IsActive || user.SuspensionReason == SuspensionReason.SubscriptionExpired))
            {
                user.IsActive = true;
                user.SuspensionReason = null;
                user.SuspensionNote = null;
                user.SuspendedAt = null;
                accountReactivated = true;
            }

            await _context.SaveChangesAsync();
            AuditLogger.Log(payment.UserId, "PAYMENT_SUCCESS", $"سداد اشتراك ناجح: {price.Plan.NameAr} ({price.BillingCycle}) بقيمة {payment.Amount} د.ل", "EzonePay");
            _logger.LogInformation("[RenewalService] Successfully settled payment {Id} and renewed subscription for user {UserId}", paymentId, payment.UserId);

            // بث الأحداث اللحظية عبر SignalR
            try
            {
                await _hubContext.Clients.Group($"user_{payment.UserId}").SendAsync("SubscriptionStatusChanged", new
                {
                    status = "Active",
                    planType = latestSub.PlanType,
                    isTrial = false,
                    startedAt = latestSub.StartedAt,
                    expiresAt = latestSub.ExpiresAt,
                    daysRemaining = SubscriptionStateHelper.ComputeDaysRemaining(latestSub.ExpiresAt, now)
                });

                if (accountReactivated)
                {
                    await _hubContext.Clients.Group($"user_{payment.UserId}").SendAsync("AccountStatusChanged", new
                    {
                        isActive = true,
                        reason = (string?)null,
                        note = (string?)null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RenewalService] SignalR notification warning for user {UserId}", payment.UserId);
            }

            return true;
        }

        /// <summary>
        /// التمديد اليدوي من لوحة التحكم (الأدمن)
        /// </summary>
        public async Task<Subscription> ApplyManualExtensionAsync(int userId, int days, Guid? planPriceId = null, string? discountCodeText = null)
        {
            var now = DateTime.UtcNow;
            var sub = await _context.Subscriptions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefaultAsync();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new InvalidOperationException("المستخدم غير موجود");

            if (planPriceId.HasValue)
            {
                var price = await _context.SubscriptionPlanPrices
                    .Include(p => p.Plan)
                    .FirstOrDefaultAsync(p => p.Id == planPriceId.Value);

                if (price == null) throw new InvalidOperationException("صف السعر غير موجود");

                var kind = SubscriptionStateHelper.DetermineKind(sub, price.Id, now);
                var baseDate = SubscriptionStateHelper.ComputeBaseDate(sub, kind, now);

                decimal finalPrice = price.Price;
                DiscountCode? usedCode = null;

                if (!string.IsNullOrWhiteSpace(discountCodeText))
                {
                    var norm = discountCodeText.Trim().ToUpperInvariant();
                    usedCode = await _context.DiscountCodes
                        .Include(d => d.Marketer)
                        .FirstOrDefaultAsync(d => d.Code == norm);

                    if (usedCode != null && usedCode.IsUsable)
                    {
                        usedCode.TimesUsed++;
                        finalPrice = usedCode.DiscountType == DiscountType.Percentage
                            ? Math.Max(0, price.Price - (price.Price * usedCode.Value / 100m))
                            : Math.Max(0, price.Price - usedCode.Value);
                    }
                }

                if (sub == null)
                {
                    sub = new Subscription
                    {
                        UserId = userId,
                        PlanId = price.PlanId,
                        PlanType = price.Plan.NameAr,
                        PlanPriceId = price.Id,
                        BillingCycle = price.BillingCycle,
                        PriceSnapshot = finalPrice,
                        IsTrial = false,
                        StartedAt = now,
                        ExpiresAt = baseDate.AddDays(price.DurationDays),
                        CreatedAt = now
                    };
                    _context.Subscriptions.Add(sub);
                }
                else
                {
                    sub.PlanId = price.PlanId;
                    sub.PlanType = price.Plan.NameAr;
                    sub.PlanPriceId = price.Id;
                    sub.BillingCycle = price.BillingCycle;
                    sub.PriceSnapshot = finalPrice;
                    sub.IsTrial = false;
                    sub.ReminderSentAt = null;
                    sub.ExpiresAt = baseDate.AddDays(price.DurationDays);

                    if (kind != PlanChangeKind.Renewal)
                    {
                        sub.StartedAt = now;
                    }
                }

                // تسجيل استحقاق عمولة للمسوق إن وجد كود
                if (usedCode != null)
                {
                    decimal comm = usedCode.CommissionType == DiscountType.Percentage
                        ? Math.Round(finalPrice * usedCode.CommissionValue / 100m, 2, MidpointRounding.AwayFromZero)
                        : usedCode.CommissionValue;

                    if (comm > 0)
                    {
                        _context.MarketerTransactions.Add(new MarketerTransaction
                        {
                            MarketerId = usedCode.MarketerId,
                            Type = MarketerTransactionType.Accrual,
                            Amount = comm,
                            UserId = userId,
                            UserName = user.FullName,
                            SubscriptionPaymentId = null,
                            DiscountCodeId = usedCode.Id,
                            DiscountCodeText = usedCode.Code,
                            PlanName = price.Plan.NameAr,
                            SubscriptionAmount = finalPrice,
                            Notes = $"استحقاق عمولة تمديد اشتراك التاجر {user.FullName} بخطة {price.Plan.NameAr}",
                            CreatedAt = now
                        });
                    }
                }
            }
            else
            {
                // تمديد بعدد أيام حر
                if (days <= 0) throw new InvalidOperationException("عدد الأيام يجب أن يكون أكبر من صفر");

                DateTime baseDate = sub != null ? (sub.ExpiresAt > now ? sub.ExpiresAt : now) : now;

                if (sub == null)
                {
                    sub = new Subscription
                    {
                        UserId = userId,
                        PlanType = "يدوي",
                        PriceSnapshot = 0,
                        IsTrial = false,
                        StartedAt = now,
                        ExpiresAt = baseDate.AddDays(days),
                        CreatedAt = now
                    };
                    _context.Subscriptions.Add(sub);
                }
                else
                {
                    sub.ExpiresAt = baseDate.AddDays(days);
                    sub.IsTrial = false;
                    sub.ReminderSentAt = null;
                }
            }

            // إعادة التفعيل التلقائي
            if (!user.IsActive || user.SuspensionReason == SuspensionReason.SubscriptionExpired)
            {
                user.IsActive = true;
                user.SuspensionReason = null;
                user.SuspensionNote = null;
                user.SuspendedAt = null;
            }

            await _context.SaveChangesAsync();
            AuditLogger.Log(userId, "SUBSCRIPTION_EXTENDED", $"تمديد يدوي لاشتراك المستخدم: {user.FullName} بمقدار {days} يوم", "Admin");
            return sub;
        }
    }
}
