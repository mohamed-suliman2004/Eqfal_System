using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Eqfal.API.Data;
using Eqfal.API.Helpers;
using Eqfal.API.Models;
using Eqfal.API.Services;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    [Route("subscription-payments")]
    [Route("api/subscription-payments")]
    public class SubscriptionPaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPaymentGatewayService _paymentGateway;
        private readonly ISubscriptionRenewalService _renewalService;
        private readonly ILogger<SubscriptionPaymentsController> _logger;

        public SubscriptionPaymentsController(
            AppDbContext context,
            IPaymentGatewayService paymentGateway,
            ISubscriptionRenewalService renewalService,
            ILogger<SubscriptionPaymentsController> logger)
        {
            _context = context;
            _paymentGateway = paymentGateway;
            _renewalService = renewalService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("nameid")?.Value
                     ?? User.FindFirst("sub")?.Value;

            return int.TryParse(claim, out int id) ? id : 0;
        }

        /// <summary>
        /// معاينة أثر تغيير الخطة (قاعدة D6) قبل إتمام الدفع
        /// </summary>
        [HttpGet("plan-change-preview")]
        [Authorize]
        public async Task<IActionResult> PlanChangePreview([FromQuery] Guid planPriceId)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var newPrice = await _context.SubscriptionPlanPrices
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.Id == planPriceId && p.IsActive && p.Plan.IsActive);

            if (newPrice == null)
            {
                return NotFound(new { success = false, message = "السعر غير موجود أو غير متاح", errorCode = "PLAN_PRICE_UNAVAILABLE" });
            }

            var now = DateTime.UtcNow;
            var currentSub = await _context.Subscriptions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefaultAsync();

            var kind = SubscriptionStateHelper.DetermineKind(currentSub, newPrice.Id, now);
            bool requiresConfirmation = kind == PlanChangeKind.Change;
            var baseDate = SubscriptionStateHelper.ComputeBaseDate(currentSub, kind, now);
            var newExpiresAt = baseDate.AddDays(newPrice.DurationDays);

            return Ok(new
            {
                success = true,
                kind = (int)kind,
                requiresConfirmation = requiresConfirmation,
                currentPlanNameAr = currentSub?.PlanType,
                currentBillingCycle = (int?)currentSub?.BillingCycle,
                currentExpiresAt = currentSub?.ExpiresAt,
                remainingDays = currentSub != null ? SubscriptionStateHelper.ComputeDaysRemaining(currentSub.ExpiresAt, now) : 0,
                newPlanNameAr = newPrice.Plan.NameAr,
                newBillingCycle = (int)newPrice.BillingCycle,
                newStartsAt = baseDate,
                newExpiresAt = newExpiresAt
            });
        }

        /// <summary>
        /// التحقق من كود الخصم وحساب الصافي
        /// </summary>
        [HttpPost("validate-discount")]
        [Authorize]
        public async Task<IActionResult> ValidateDiscount([FromBody] ValidateDiscountRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { success = false, message = "أدخل كود الخصم أولاً" });

            var price = await _context.SubscriptionPlanPrices
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.Id == request.PlanPriceId && p.IsActive && p.Plan.IsActive);

            if (price == null)
                return NotFound(new { success = false, message = "السعر غير موجود أو غير متاح" });

            if (price.Plan.IsTrialPlan)
                return BadRequest(new { success = false, message = "لا يمكن استخدام كود خصم مع خطة تجربة" });

            string normCode = request.Code.Trim().ToUpperInvariant();
            var code = await _context.DiscountCodes
                .Include(d => d.Marketer)
                .FirstOrDefaultAsync(d => d.Code == normCode);

            var now = DateTime.UtcNow;

            if (code == null) return BadRequest(new { success = false, message = "كود الخصم غير موجود" });
            if (!code.IsActive) return BadRequest(new { success = false, message = "كود الخصم غير مفعّل" });
            if (code.ExpiresAt.HasValue && code.ExpiresAt.Value < now) return BadRequest(new { success = false, message = "كود الخصم منتهي الصلاحية" });
            if (code.MaxUses.HasValue && code.TimesUsed >= code.MaxUses.Value) return BadRequest(new { success = false, message = "كود الخصم استُنفد الحد الأقصى لاستخدامه" });

            decimal originalPrice = price.Price;
            decimal discountAmount = code.DiscountType == DiscountType.Percentage
                ? Math.Round(originalPrice * code.Value / 100m, 2, MidpointRounding.AwayFromZero)
                : code.Value;

            decimal finalPrice = Math.Max(0, originalPrice - discountAmount);

            return Ok(new
            {
                success = true,
                code = code.Code,
                originalPrice = originalPrice,
                discountAmount = discountAmount,
                finalPrice = finalPrice,
                discountType = (int)code.DiscountType,
                value = code.Value
            });
        }

        /// <summary>
        /// طلب إنشاء فاتورة دفع أونلاين عبر EzonePay
        /// </summary>
        [HttpPost("checkout")]
        [Authorize]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            if (!_paymentGateway.IsConfigured)
            {
                return StatusCode(503, new { success = false, message = "بوابة الدفع غير مُفعّلة حالياً — تواصل مع الدعم" });
            }

            var price = await _context.SubscriptionPlanPrices
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.Id == request.PlanPriceId && p.IsActive && p.Plan.IsActive);

            if (price == null)
            {
                return NotFound(new { success = false, message = "السعر غير موجود أو غير متاح", errorCode = "PLAN_PRICE_UNAVAILABLE" });
            }

            if (price.Plan.IsTrialPlan)
            {
                return BadRequest(new { success = false, message = "لا يمكن الدفع لخطة تجربة" });
            }

            var now = DateTime.UtcNow;
            var currentSub = await _context.Subscriptions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.ExpiresAt)
                .FirstOrDefaultAsync();

            var kind = SubscriptionStateHelper.DetermineKind(currentSub, price.Id, now);

            if (kind == PlanChangeKind.Change && !request.ConfirmReplaceActivePlan)
            {
                return Conflict(new
                {
                    success = false,
                    message = "اشتراكك الحالي سارٍ — يلزم تأكيد إنهائه فوراً قبل بدء الخطة الجديدة",
                    errorCode = "PLAN_CHANGE_CONFIRMATION_REQUIRED"
                });
            }

            decimal finalAmount = price.Price;
            string? normalizedCode = null;

            if (!string.IsNullOrWhiteSpace(request.DiscountCode))
            {
                normalizedCode = request.DiscountCode.Trim().ToUpperInvariant();
                var code = await _context.DiscountCodes.FirstOrDefaultAsync(d => d.Code == normalizedCode);
                if (code == null) return BadRequest(new { success = false, message = "كود الخصم غير موجود" });
                if (!code.IsActive) return BadRequest(new { success = false, message = "كود الخصم غير مفعّل" });
                if (code.ExpiresAt.HasValue && code.ExpiresAt.Value < now) return BadRequest(new { success = false, message = "كود الخصم منتهي الصلاحية" });
                if (code.MaxUses.HasValue && code.TimesUsed >= code.MaxUses.Value) return BadRequest(new { success = false, message = "كود الخصم استُنفد الحد الأقصى لاستخدامه" });

                decimal discountAmt = code.DiscountType == DiscountType.Percentage
                    ? Math.Round(price.Price * code.Value / 100m, 2, MidpointRounding.AwayFromZero)
                    : code.Value;

                finalAmount = Math.Max(0, price.Price - discountAmt);
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            var payment = new SubscriptionPayment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PlanId = price.PlanId,
                PlanPriceId = price.Id,
                BillingCycle = price.BillingCycle,
                ReplacesActivePlan = kind == PlanChangeKind.Change,
                DiscountCode = normalizedCode,
                Amount = finalAmount,
                Currency = "LYD",
                GatewayPaymentId = string.Empty,
                Status = "Pending",
                CreatedAt = now
            };

            // لو كان المبلغ 0 بسبب خصم 100%، نعتمده فوراً دون المرور بالبوابة (قرار البند 12.8)
            if (finalAmount <= 0)
            {
                payment.GatewayPaymentId = $"FREE-{payment.Id:N}";
                _context.SubscriptionPayments.Add(payment);
                await _context.SaveChangesAsync();

                await _renewalService.ProcessSettledPaymentAsync(payment.Id, 0);

                return Ok(new
                {
                    success = true,
                    id = payment.Id,
                    isFree = true,
                    paymentUrl = (string?)null,
                    message = "تم تفعيل الاشتراك بنجاح بخصم 100%"
                });
            }

            // الاتصال بـ EzonePay لإنشاء رابط الدفع
            var invoiceParams = new Dictionary<string, string>
            {
                { "subscriptionPaymentId", payment.Id.ToString() },
                { "customerName", user.FullName },
                { "customerPhone", user.Phone ?? "0910000000" }
            };

            string cycleName = price.BillingCycle switch
            {
                BillingCycle.Monthly => "شهري",
                BillingCycle.Quarterly => "3 شهور",
                BillingCycle.SemiAnnual => "6 شهور",
                BillingCycle.Yearly => "سنوي",
                _ => ""
            };

            string description = $"اشتراك إقفال — {price.Plan.NameAr} ({cycleName})";

            var invoiceResult = await _paymentGateway.CreateInvoiceAsync(finalAmount, "LYD", description, invoiceParams);
            if (invoiceResult == null)
            {
                return StatusCode(502, new { success = false, message = "تعذّر إنشاء رابط الدفع لدى بوابة EzonePay، حاول مرة أخرى" });
            }

            payment.GatewayPaymentId = invoiceResult.PaymentId;
            _context.SubscriptionPayments.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                id = payment.Id,
                paymentUrl = invoiceResult.PaymentUrl
            });
        }

        /// <summary>
        /// استعلام حالة الدفعة من التطبيق (Polling) والمطابقة اللحظية
        /// </summary>
        [HttpGet("{id}/status")]
        [Authorize]
        public async Task<IActionResult> GetPaymentStatus(Guid id)
        {
            int userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var payment = await _context.SubscriptionPayments.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (payment == null) return NotFound(new { success = false, message = "العملية غير موجودة" });

            // لو ما زالت معلقة، نسأل EzonePay احتياطياً للمطابقة
            if (payment.Status == "Pending" && !string.IsNullOrEmpty(payment.GatewayPaymentId))
            {
                var gwStatus = await _paymentGateway.GetPaymentAsync(payment.GatewayPaymentId);
                if (gwStatus != null)
                {
                    if (gwStatus.Status == "Settled")
                    {
                        await _renewalService.ProcessSettledPaymentAsync(payment.Id);
                    }
                    else if (gwStatus.Status == "Cancelled")
                    {
                        payment.Status = "Cancelled";
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return Ok(new
            {
                success = true,
                id = payment.Id,
                status = payment.Status,
                amount = payment.Amount,
                currency = payment.Currency,
                createdAt = payment.CreatedAt,
                completedAt = payment.CompletedAt
            });
        }

        /// <summary>
        /// استقبال إشعارات Webhook من EzonePay
        /// </summary>
        [HttpPost("ezonepay/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> EzonePayWebhook()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            string? signature = Request.Headers["X-Signature"].FirstOrDefault();
            if (!_paymentGateway.VerifyWebhookSignature(rawBody, signature))
            {
                _logger.LogWarning("[EzonePay Webhook] Invalid signature received");
                return Unauthorized();
            }

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                // استخراج orderReference من الإشعار
                string? orderRef = null;
                if (root.TryGetProperty("orderReference", out var orProp)) orderRef = orProp.GetString();
                else if (root.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("orderReference", out var dorProp)) orderRef = dorProp.GetString();

                if (!Guid.TryParse(orderRef, out Guid paymentId))
                {
                    return Ok(new { received = true, note = "Ignored non-guid orderReference" });
                }

                var payment = await _context.SubscriptionPayments.FindAsync(paymentId);
                if (payment == null)
                {
                    return Ok(new { received = true, note = "Unknown payment id" });
                }

                if (payment.Status != "Pending")
                {
                    return Ok(new { received = true, note = "Payment already processed" });
                }

                // الاستعلام من البوابة مباشرة كمصدر الحقيقة الوحيد
                var gwStatus = await _paymentGateway.GetPaymentAsync(payment.GatewayPaymentId);
                if (gwStatus == null)
                {
                    return StatusCode(502); // لتعيد البوابة المحاولة
                }

                if (gwStatus.Status == "Settled")
                {
                    await _renewalService.ProcessSettledPaymentAsync(payment.Id);
                }
                else if (gwStatus.Status == "Cancelled")
                {
                    payment.Status = "Cancelled";
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EzonePay Webhook] Error parsing webhook payload");
                return Ok(new { received = true, error = ex.Message });
            }
        }

        /// <summary>
        /// تقرير سجل الدفعات للإدارة
        /// </summary>
        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminPayments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 500,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] Guid? planId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var query = _context.SubscriptionPayments
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Plan)
                .Include(p => p.PlanPrice)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(p => p.User.FullName.Contains(s) || (p.User.Phone != null && p.User.Phone.Contains(s)) || p.GatewayPaymentId.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status == status.Trim());
            }

            if (planId.HasValue)
            {
                query = query.Where(p => p.PlanId == planId.Value);
            }

            if (from.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                var endDay = to.Value.Date.AddDays(1);
                query = query.Where(p => p.CreatedAt < endDay);
            }

            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    id = p.Id,
                    userId = p.UserId,
                    fullName = p.User.FullName,
                    username = p.User.UsernameEmail,
                    userEmail = p.User.UsernameEmail,
                    userPhone = p.User.Phone,
                    planId = p.PlanId,
                    planNameAr = p.Plan.NameAr,
                    billingCycle = (int?)p.BillingCycle,
                    amount = p.Amount,
                    currency = p.Currency,
                    status = p.Status,
                    discountCode = p.DiscountCode,
                    gatewayReferenceId = p.GatewayPaymentId,
                    createdAt = p.CreatedAt,
                    completedAt = p.CompletedAt
                })
                .ToListAsync();

            return Ok(new { success = true, totalCount, page, pageSize, data = items });
        }

        /// <summary>
        /// ملخص الإيرادات المالية (Settled فقط)
        /// </summary>
        [HttpGet("admin/summary")]
        public async Task<IActionResult> GetAdminSummary([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var query = _context.SubscriptionPayments
                .AsNoTracking()
                .Include(p => p.Plan)
                .Where(p => p.Status == "Settled" && p.CompletedAt.HasValue);

            if (from.HasValue)
            {
                query = query.Where(p => p.CompletedAt >= from.Value);
            }

            if (to.HasValue)
            {
                var endDay = to.Value.Date.AddDays(1);
                query = query.Where(p => p.CompletedAt < endDay);
            }

            var settledPayments = await query.ToListAsync();

            decimal totalRevenue = settledPayments.Sum(p => p.Amount);
            int paymentsCount = settledPayments.Count;
            decimal averageAmount = paymentsCount > 0 ? Math.Round(totalRevenue / paymentsCount, 2) : 0;

            var byPlan = settledPayments
                .GroupBy(p => new { p.PlanId, p.Plan.NameAr })
                .Select(g => new
                {
                    planId = g.Key.PlanId,
                    planNameAr = g.Key.NameAr,
                    amount = g.Sum(x => x.Amount),
                    count = g.Count()
                })
                .OrderByDescending(x => x.amount)
                .ToList();

            return Ok(new
            {
                success = true,
                totalRevenue,
                paymentsCount,
                averageAmount,
                byPlan
            });
        }
    }

    public class ValidateDiscountRequest
    {
        public Guid PlanPriceId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class CheckoutRequest
    {
        public Guid PlanPriceId { get; set; }
        public string? DiscountCode { get; set; }
        public bool ConfirmReplaceActivePlan { get; set; } = false;
    }
}
