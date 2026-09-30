using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Eqfal.API.Data;
using Eqfal.API.Models;
using Microsoft.AspNetCore.SignalR;
using Eqfal.API.Hubs;

namespace Eqfal.API.Services
{
    public static class AuditLogger
    {
        private static IServiceProvider? _serviceProvider;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static void Log(int? userId, string action, string details, string ipAddress = "")
        {
            if (_serviceProvider == null) return;

            // Non-blocking background task to ensure zero impact on request latency
            Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var hubContext = scope.ServiceProvider.GetService<IHubContext<OperationsHub>>();

                    var log = new AuditLog
                    {
                        UserId = userId,
                        Action = action,
                        Details = details,
                        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
                        Timestamp = DateTime.UtcNow
                    };

                    db.AuditLogs.Add(log);
                    await db.SaveChangesAsync();

                    // Real-time broadcast to Company Dashboard via SignalR
                    if (hubContext != null)
                    {
                        string userName = "مجهول / زائر";
                        if (userId.HasValue && userId.Value > 0)
                        {
                            var u = await db.Users.FindAsync(userId.Value);
                            if (u != null) userName = u.FullName;
                        }

                        var livePayload = new
                        {
                            id = log.Id,
                            userId = log.UserId,
                            userName = userName,
                            action = log.Action,
                            details = log.Details,
                            ipAddress = log.IpAddress,
                            timestamp = log.Timestamp
                        };

                        await hubContext.Clients.All.SendAsync("ReceiveAuditLog", livePayload);
                        await hubContext.Clients.All.SendAsync("ReceiveSystemStreamLog", new
                        {
                            time = log.Timestamp.ToString("HH:mm:ss"),
                            type = action.Contains("ERROR") || action.Contains("FAIL") ? "error" : (action.Contains("SECURITY") || action.Contains("PASSWORD") || action.Contains("OTP") ? "warn" : "info"),
                            source = "Audit",
                            message = $"[{action}] {userName}: {details}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AuditLog error: {ex.Message}");
                }
            });
        }
    }
}