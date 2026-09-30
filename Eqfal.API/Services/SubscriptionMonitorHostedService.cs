using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Eqfal.API.Data;
using Eqfal.API.Helpers;
using Eqfal.API.Hubs;
using Eqfal.API.Models;

namespace Eqfal.API.Services
{
    public class SubscriptionMonitorHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SubscriptionMonitorHostedService> _logger;

        public SubscriptionMonitorHostedService(
            IServiceProvider serviceProvider,
            ILogger<SubscriptionMonitorHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[SubscriptionMonitor] Background Service starting. Initial delay 30s...");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DoWorkAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SubscriptionMonitor] Error during hourly subscription check cycle");
                }

                // تكرار كل ساعة
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task DoWorkAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<OperationsHub>>();

            var now = DateTime.UtcNow;

            // جلب إعدادات فترة السماح
            var settings = await context.SubscriptionSettings.FirstOrDefaultAsync(stoppingToken);
            int graceDays = settings?.GracePeriodDays ?? 3;

            // جلب أحدث اشتراك لكل مستخدم
            var activeUsers = await context.Users
                .Include(u => u.Subscriptions)
                .ToListAsync(stoppingToken);

            foreach (var user in activeUsers)
            {
                var latestSub = user.Subscriptions
                    .OrderByDescending(s => s.ExpiresAt)
                    .FirstOrDefault();

                if (latestSub == null) continue;

                string status = SubscriptionStateHelper.ComputeStatus(latestSub.ExpiresAt, latestSub.IsTrial, now, graceDays);
                int daysLeft = SubscriptionStateHelper.ComputeDaysRemaining(latestSub.ExpiresAt, now);

                // أ) تذكير قبل الانتهاء بـ 3 أيام أو أقل لمرة واحدة
                if (latestSub.ReminderSentAt == null && daysLeft <= 3 && status != "Expired")
                {
                    latestSub.ReminderSentAt = now;
                    _logger.LogInformation("[SubscriptionMonitor] Sending expiration reminder to User {UserId} ({Days} days left)", user.Id, daysLeft);

                    try
                    {
                        await hubContext.Clients.Group($"user_{user.Id}").SendAsync("SubscriptionStatusChanged", new
                        {
                            status = status,
                            planType = latestSub.PlanType,
                            isTrial = latestSub.IsTrial,
                            startedAt = latestSub.StartedAt,
                            expiresAt = latestSub.ExpiresAt,
                            daysRemaining = daysLeft,
                            isWarning = true,
                            warningMessage = $"اشتراكك سينتهي بتاريخ {latestSub.ExpiresAt:yyyy/MM/dd}. يرجى التجديد لتفادي انقطاع الخدمة."
                        }, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[SubscriptionMonitor] SignalR warning for user {UserId}", user.Id);
                    }
                }

                // ب) إيقاف الحساب التلقائي وفصل جلسة الواتساب بعد انتهاء فترة السماح
                if (status == "Expired")
                {
                    bool wasActive = user.IsActive;
                    if (wasActive)
                    {
                        user.IsActive = false;
                        user.SuspensionReason = SuspensionReason.SubscriptionExpired;
                        user.SuspensionNote = "تم إيقاف الحساب تلقائياً لانتهاء الاشتراك وفترة السماح";
                        user.SuspendedAt = now;

                        _logger.LogInformation("[SubscriptionMonitor] User {UserId} subscription expired. Moving to Read-Only mode.", user.Id);

                        try
                        {
                            await hubContext.Clients.Group($"user_{user.Id}").SendAsync("AccountStatusChanged", new
                            {
                                isActive = false,
                                reason = "subscription_expired",
                                note = "تم إيقاف الحساب مؤقتاً لانتهاء الاشتراك. يمكنك استعراض بياناتك وتجديد الاشتراك لاستعادة الوصول الكامل."
                            }, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[SubscriptionMonitor] SignalR warning on suspension for user {UserId}", user.Id);
                        }
                    }

                    // فصل جلسة الواتساب تلقائياً من محرك Evolution API إذا كانت لا تزال متصلة أو تم تعليق الحساب للتو
                    var wsSession = await context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == user.Id, stoppingToken);
                    if (wasActive || (wsSession != null && wsSession.Status == "متصل"))
                    {
                        await DisconnectUserWhatsAppSessionAsync(context, hubContext, user.Id, stoppingToken);
                    }
                }
            }

            // ج) تنظيف الدفعات المعلقة (Pending) الأقدم من 24 ساعة
            var cutoff = now.AddHours(-24);
            var orphanPayments = await context.SubscriptionPayments
                .Where(p => p.Status == "Pending" && p.CreatedAt < cutoff)
                .ToListAsync(stoppingToken);

            foreach (var op in orphanPayments)
            {
                op.Status = "Cancelled";
            }

            if (orphanPayments.Any())
            {
                _logger.LogInformation("[SubscriptionMonitor] Cleaned up {Count} orphan pending payments.", orphanPayments.Count);
            }

            await context.SaveChangesAsync(stoppingToken);
        }

        private async Task DisconnectUserWhatsAppSessionAsync(
            AppDbContext context,
            IHubContext<OperationsHub> hubContext,
            int userId,
            CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                var httpClientFactory = scope.ServiceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>();

                string nodeUrl = (config["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                string apiKey = config["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
                string instanceName = $"user_{userId}";

                using var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                // 1. طلب تسجيل خروج الجلسة من محرك Evolution API
                try
                {
                    using var logoutReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Delete, $"{nodeUrl}/instance/logout/{instanceName}");
                    logoutReq.Headers.Add("apikey", apiKey);
                    await client.SendAsync(logoutReq, stoppingToken);
                    _logger.LogInformation("[SubscriptionMonitor] Evolution instance logged out for expired user {UserId} ({Instance})", userId, instanceName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SubscriptionMonitor] Could not logout Evolution instance {Instance}", instanceName);
                }

                // 2. حذف الجلسة لمنع إعادة الاتصال التلقائي
                try
                {
                    using var delReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Delete, $"{nodeUrl}/instance/delete/{instanceName}");
                    delReq.Headers.Add("apikey", apiKey);
                    await client.SendAsync(delReq, stoppingToken);
                }
                catch { }

                // 3. تحديث حالة الجلسة في قاعدة البيانات إلى "غير متصل"
                var session = await context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == userId, stoppingToken);
                if (session != null)
                {
                    session.Status = "غير متصل";
                    session.PairingCode = null;
                }

                // 4. إشعار تطبيق التاجر ولوحة العمليات لحظياً بتغير حالة الواتساب
                try
                {
                    await hubContext.Clients.User(userId.ToString()).SendAsync("WhatsAppStatusChanged", "غير متصل", stoppingToken);
                    await hubContext.Clients.Group($"user_{userId}").SendAsync("WhatsAppStatusChanged", "غير متصل", stoppingToken);
                }
                catch { }

                AuditLogger.Log(userId, "WHATSAPP_AUTO_DISCONNECT", "تم فصل جلسة الواتساب تلقائياً لانتهاء الاشتراك وفترة السماح", "System");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SubscriptionMonitor] Error disconnecting WhatsApp session for expired user {UserId}", userId);
            }
        }
    }
}
