using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Eqfal.API.Data;
using Eqfal.API.Models;

namespace Eqfal.API.Helpers
{
    public class SubscriptionAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;

        public SubscriptionAccessMiddleware(RequestDelegate next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // إذا لم يكن المستخدم مسجلاً دخوله، يمر الطلب للمصادقة العادية
            if (context.User?.Identity == null || !context.User.Identity.IsAuthenticated)
            {
                await _next(context);
                return;
            }

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.User.FindFirst("nameid")?.Value
                           ?? context.User.FindFirst("sub")?.Value;

            if (!int.TryParse(userIdClaim, out int userId) || userId <= 0)
            {
                await _next(context);
                return;
            }

            // فحص كاش حالة الحساب لتجنب استعلام قاعدة البيانات في كل نقرة (كاش 30 ثانية)
            string cacheKey = $"user-active-{userId}";
            if (!_cache.TryGetValue(cacheKey, out UserStatusCache? userStatus))
            {
                using var scope = context.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var user = await dbContext.Users.FindAsync(userId);
                if (user != null)
                {
                    userStatus = new UserStatusCache
                    {
                        IsActive = user.IsActive,
                        SuspensionReason = user.SuspensionReason
                    };
                    _cache.Set(cacheKey, userStatus, TimeSpan.FromSeconds(30));
                }
            }

            // لو الحساب موقوف يدوياً (Manual) => يرفض فوراً بـ 401
            if (userStatus != null && !userStatus.IsActive && userStatus.SuspensionReason == SuspensionReason.Manual)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                var errorResponse = new
                {
                    success = false,
                    message = "تم إيقاف هذا الحساب. تواصل مع الدعم الفني لمزيد من التفاصيل.",
                    data = (object?)null,
                    errors = new[] { "تم إيقاف هذا الحساب. تواصل مع الدعم الفني لمزيد من التفاصيل." }
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }

            // لو الحساب موقوف لانتهاء الاشتراك (SubscriptionExpired) => الوضع المقيد (Read-Only)
            if (userStatus != null && (!userStatus.IsActive && userStatus.SuspensionReason == SuspensionReason.SubscriptionExpired))
            {
                string method = context.Request.Method.ToUpperInvariant();
                string path = context.Request.Path.Value ?? "";

                // السماح بطلبات القراءة والتصدير
                bool isReadMethod = method == "GET" || method == "HEAD" || method == "OPTIONS";

                // السماح بمسارات الدفع وتجديد الاشتراك والمصادقة والـ Hubs
                bool isAllowedPath = path.Contains("/subscription-payments", StringComparison.OrdinalIgnoreCase) ||
                                     path.Contains("/subscriptions", StringComparison.OrdinalIgnoreCase) ||
                                     path.Contains("/Auth", StringComparison.OrdinalIgnoreCase) ||
                                     path.Contains("/hubs", StringComparison.OrdinalIgnoreCase) ||
                                     path.Contains("/SystemMonitor", StringComparison.OrdinalIgnoreCase);

                if (!isReadMethod && !isAllowedPath)
                {
                    // رفض بـ 403 وليس 401 حتى لا يقوم التطبيق بتسجيل خروج التاجر
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    var errorObj = new
                    {
                        success = false,
                        message = "انتهى اشتراكك. جدّد الاشتراك للمتابعة.",
                        data = (object?)null,
                        errors = new[] { "انتهى اشتراكك. جدّد الاشتراك للمتابعة." },
                        errorCode = "SUBSCRIPTION_EXPIRED"
                    };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorObj));
                    return;
                }
            }

            await _next(context);
        }

        private class UserStatusCache
        {
            public bool IsActive { get; set; }
            public SuspensionReason? SuspensionReason { get; set; }
        }
    }
}
