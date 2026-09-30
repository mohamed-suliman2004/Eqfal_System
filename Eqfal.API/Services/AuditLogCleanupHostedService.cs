using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Eqfal.API.Data;

namespace Eqfal.API.Services
{
    /// <summary>
    /// Background service that periodically cleans up:
    /// 1. Old AuditLogs (default: 30 days)
    /// 2. Abandoned Draft Operations (Status == "مسودة", default: 30 days)
    /// 3. Old Confirmed Operations (Status != "مسودة", default: 180 days / 6 months)
    /// </summary>
    public class AuditLogCleanupHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditLogCleanupHostedService> _logger;

        public AuditLogCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<AuditLogCleanupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay of 1 minute after server boot to avoid startup congestion
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                    // 1. Audit Logs Policy (default: 30 days)
                    int auditRetentionDays = 30;
                    if (int.TryParse(config["DataRetentionSettings:AuditLogRetentionDays"] ?? config["AuditLogSettings:RetentionDays"], out int configuredAudit) && configuredAudit > 0)
                    {
                        auditRetentionDays = configuredAudit;
                    }
                    var auditCutoff = DateTime.UtcNow.AddDays(-auditRetentionDays);
                    int deletedAuditLogs = await db.AuditLogs
                        .Where(a => a.Timestamp < auditCutoff)
                        .ExecuteDeleteAsync(stoppingToken);

                    if (deletedAuditLogs > 0)
                    {
                        _logger.LogInformation(
                            "Data Retention Policy: Purged {Count} audit logs older than {Days} days.",
                            deletedAuditLogs, auditRetentionDays);
                    }

                    // 2. Draft Operations Policy (default: 30 days)
                    int draftRetentionDays = 30;
                    if (int.TryParse(config["DataRetentionSettings:DraftOperationsRetentionDays"], out int configuredDraft) && configuredDraft > 0)
                    {
                        draftRetentionDays = configuredDraft;
                    }
                    var draftCutoff = DateTime.UtcNow.AddDays(-draftRetentionDays);
                    int deletedDrafts = await db.Operations
                        .Where(o => o.Status == "مسودة" && o.CreatedAt < draftCutoff)
                        .ExecuteDeleteAsync(stoppingToken);

                    if (deletedDrafts > 0)
                    {
                        _logger.LogInformation(
                            "Data Retention Policy: Purged {Count} abandoned draft operations older than {Days} days.",
                            deletedDrafts, draftRetentionDays);
                    }

                    // 3. Confirmed Operations Policy (default: 180 days / 6 months)
                    int confirmedRetentionDays = 180;
                    if (int.TryParse(config["DataRetentionSettings:ConfirmedOperationsRetentionDays"], out int configuredConfirmed) && configuredConfirmed > 0)
                    {
                        confirmedRetentionDays = configuredConfirmed;
                    }
                    var confirmedCutoff = DateTime.UtcNow.AddDays(-confirmedRetentionDays);
                    int deletedConfirmed = await db.Operations
                        .Where(o => o.Status != "مسودة" && o.CreatedAt < confirmedCutoff)
                        .ExecuteDeleteAsync(stoppingToken);

                    if (deletedConfirmed > 0)
                    {
                        _logger.LogInformation(
                            "Data Retention Policy: Purged {Count} old confirmed operations older than {Days} days.",
                            deletedConfirmed, confirmedRetentionDays);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error occurred while executing periodic data retention cleanup.");
                }

                // Run once every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
