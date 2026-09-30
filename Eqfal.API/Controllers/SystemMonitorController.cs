using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Eqfal.API.Data;
using Eqfal.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace Eqfal.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class SystemMonitorController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SystemMonitorController> _logger;

        public SystemMonitorController(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<SystemMonitorController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private static string NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "غير متصل";
            var s = status.Trim();

            if (s == "متصل" || s.Equals("open", StringComparison.OrdinalIgnoreCase) || s.Equals("connected", StringComparison.OrdinalIgnoreCase))
                return "متصل";

            if (s == "في انتظار الربط" || s == "جارٍ الاتصال" || s == "جاري الاتصال" || s.Equals("connecting", StringComparison.OrdinalIgnoreCase))
                return "في انتظار الربط";

            if (s == "غير متصل" || s.Equals("close", StringComparison.OrdinalIgnoreCase) || s.Equals("disconnected", StringComparison.OrdinalIgnoreCase))
                return "غير متصل";

            if (s.Contains("متصل") && !s.Contains("غير"))
                return "متصل";

            if (s.Contains("ربط") || s.Contains("اتصال"))
                return "في انتظار الربط";

            return "غير متصل";
        }

        private static bool IsConnected(string? status)
        {
            return NormalizeStatus(status) == "متصل";
        }

        // GET /SystemMonitor/health
        [HttpGet("health")]
        public async Task<IActionResult> GetHealth()
        {
            bool dbStatus = false;
            try
            {
                dbStatus = await _context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection check failed");
            }

            var allSessions = await _context.WhatsAppSessions.AsNoTracking().ToListAsync();
            int connectedCount = allSessions.Count(s => IsConnected(s.Status));
            int totalSessions = allSessions.Count;

            var proc = Process.GetCurrentProcess();
            long heapUsed = GC.GetTotalMemory(false);
            long totalRss = proc.WorkingSet64;
            long uptimeSeconds = (long)(DateTime.UtcNow - proc.StartTime.ToUniversalTime()).TotalSeconds;
            if (uptimeSeconds < 0) uptimeSeconds = 0;

            var nodeHealth = new
            {
                status = "running",
                instancesCount = totalSessions,
                connectedCount = connectedCount,
                uptimeSeconds = uptimeSeconds,
                memoryUsage = new
                {
                    heapUsed = heapUsed,
                    rss = totalRss
                }
            };

            return Ok(new
            {
                apiStatus = true,
                dbStatus,
                nodeStatus = true,
                nodeHealth,
                timestamp = DateTime.UtcNow
            });
        }

        // GET /SystemMonitor/metrics
        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                int totalOps = await _context.Operations.AsNoTracking().CountAsync();
                int inOps = await _context.Operations.AsNoTracking().CountAsync(o => !o.IsOutgoing);
                int outOps = await _context.Operations.AsNoTracking().CountAsync(o => o.IsOutgoing);

                var allSessions = await _context.WhatsAppSessions.AsNoTracking().ToListAsync();
                int connectedSessions = allSessions.Count(s => IsConnected(s.Status));

                int totalUsers = await _context.Users.AsNoTracking().CountAsync(u => u.IsActive);
                if (totalUsers < 1) totalUsers = 1;

                return Ok(new
                {
                    activeSessions = connectedSessions,
                    activeSessionsCount = connectedSessions,
                    messagesReceived = inOps,
                    messagesSent = outOps,
                    webhookSuccess = totalOps,
                    webhookFailures = 0,
                    webhookDropped = 0,
                    webhookQueueLength = 0,
                    activeWebhookWorkers = connectedSessions > 0 ? connectedSessions : 1,
                    activePairingsCount = 0,
                    authStoresCount = totalUsers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET /SystemMonitor/sessions
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            try
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.WhatsAppSession)
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Id)
                    .ToListAsync();

                var resultList = users.Select(u => {
                    string cleanStatus = NormalizeStatus(u.WhatsAppSession?.Status);
                    bool connected = cleanStatus == "متصل";

                    string displayPhone = !string.IsNullOrEmpty(u.Phone) && u.Phone != "0000000000" 
                        ? u.Phone 
                        : (!string.IsNullOrEmpty(u.WhatsAppSession?.PairingCode) && u.WhatsAppSession.PairingCode.Length >= 8 
                            ? u.WhatsAppSession.PairingCode 
                            : (u.Phone ?? "غير محدد"));

                    return new
                    {
                        userId = u.Id,
                        fullName = u.FullName ?? "مستخدم",
                        username = u.UsernameEmail,
                        email = u.UsernameEmail,
                        phone = displayPhone,
                        status = cleanStatus,
                        isConnected = connected,
                        lastConnected = u.WhatsAppSession?.LastConnected
                    };
                }).ToList();

                return Ok(resultList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET /SystemMonitor/otp-setting
        [HttpGet("otp-setting")]
        public async Task<IActionResult> GetOtpSetting()
        {
            try
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "OtpSenderUserId");
                int userId = 1;
                if (setting != null && int.TryParse(setting.Value, out int parsed))
                {
                    userId = parsed;
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.WhatsAppSession)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                string cleanStatus = NormalizeStatus(user?.WhatsAppSession?.Status);
                bool connected = cleanStatus == "متصل";

                return Ok(new
                {
                    userId = userId,
                    fullName = user != null ? (user.FullName ?? "مستخدم") : "مستخدم غير محدد",
                    phone = user != null ? (user.Phone ?? "غير محدد") : "",
                    status = cleanStatus,
                    isConnected = connected,
                    updatedAt = setting?.UpdatedAt ?? DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class SetOtpSenderRequest
        {
            public int UserId { get; set; }
        }

        // POST /SystemMonitor/otp-setting
        [HttpPost("otp-setting")]
        public async Task<IActionResult> SetOtpSender([FromBody] SetOtpSenderRequest request)
        {
            if (request.UserId <= 0) return BadRequest("معرف المستخدم غير صالح");

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("المستخدم غير موجود");

            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "OtpSenderUserId");
            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = "OtpSenderUserId",
                    Value = request.UserId.ToString(),
                    UpdatedAt = DateTime.UtcNow
                };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = request.UserId.ToString();
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"تم تعيين المستخدم #{user.Id} ({user.FullName}) كالرقم الرسمي لإرسال OTP",
                userId = user.Id,
                fullName = user.FullName,
                phone = user.Phone ?? ""
            });
        }

        // GET /SystemMonitor/user-activity
        [HttpGet("user-activity")]
        public async Task<IActionResult> GetUserActivity()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Include(u => u.WhatsAppSession)
                .Where(u => u.IsActive)
                .OrderBy(u => u.Id)
                .ToListAsync();

            var opsCounts = await _context.Operations
                .AsNoTracking()
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserId, g => g.Count);

            var activityList = users.Select(u => {
                string cleanStatus = NormalizeStatus(u.WhatsAppSession?.Status);
                bool connected = cleanStatus == "متصل";

                return new
                {
                    userId = u.Id,
                    fullName = u.FullName ?? "مستخدم",
                    username = u.UsernameEmail,
                    email = u.UsernameEmail,
                    phone = u.Phone ?? "غير محدد",
                    status = cleanStatus,
                    isConnected = connected,
                    operationsProcessed = opsCounts.ContainsKey(u.Id) ? opsCounts[u.Id] : 0,
                    inboundMessages = opsCounts.ContainsKey(u.Id) ? opsCounts[u.Id] : 0,
                    outboundMessages = 0,
                    lastConnected = u.WhatsAppSession?.LastConnected
                };
            }).ToList();

            return Ok(activityList);
        }

        // GET /SystemMonitor/audit-logs
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int limit = 100,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null)
        {
            var query = _context.AuditLogs
                .AsNoTracking()
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(a => 
                    (a.User != null && a.User.FullName.Contains(s)) ||
                    a.Action.Contains(s) ||
                    a.Details.Contains(s) ||
                    a.IpAddress.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(type) && type != "all")
            {
                if (type == "error")
                {
                    query = query.Where(a => a.Action.Contains("ERROR") || a.Action.Contains("FAIL") || a.Details.Contains("فشل") || a.Details.Contains("خطأ"));
                }
                else if (type == "security")
                {
                    query = query.Where(a => a.Action.Contains("SECURITY") || a.Action.Contains("PASSWORD") || a.Action.Contains("OTP") || a.Action.Contains("AUTH"));
                }
                else if (type == "operations")
                {
                    query = query.Where(a => a.Action.Contains("OPERATION") || a.Action.Contains("EXTRACT") || a.Action.Contains("MSG"));
                }
            }

            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .Select(a => new
                {
                    id = a.Id,
                    userId = a.UserId,
                    userName = a.User != null ? a.User.FullName : "نظام",
                    action = a.Action,
                    details = a.Details,
                    ipAddress = a.IpAddress,
                    timestamp = a.Timestamp
                })
                .ToListAsync();

            return Ok(logs);
        }

        // POST /SystemMonitor/cleanup-audit-logs
        [HttpPost("cleanup-audit-logs")]
        public async Task<IActionResult> CleanupAuditLogs([FromQuery] int days = 30)
        {
            if (days < 1) days = 1;
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            int deletedCount = await _context.AuditLogs
                .Where(a => a.Timestamp < cutoffDate)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Manual cleanup of audit logs executed. Purged {Count} records older than {Days} days.", deletedCount, days);

            return Ok(new
            {
                success = true,
                deletedCount,
                cutoffDate,
                message = $"تم حذف {deletedCount} سجل قديم (أقدم من {days} يوم) بنجاح"
            });
        }

        // POST /SystemMonitor/cleanup-operations
        [HttpPost("cleanup-operations")]
        public async Task<IActionResult> CleanupOperations(
            [FromQuery] int draftDays = 30,
            [FromQuery] int confirmedDays = 180)
        {
            if (draftDays < 1) draftDays = 1;
            if (confirmedDays < 1) confirmedDays = 1;

            var draftCutoff = DateTime.UtcNow.AddDays(-draftDays);
            var confirmedCutoff = DateTime.UtcNow.AddDays(-confirmedDays);

            int deletedDrafts = await _context.Operations
                .Where(o => o.Status == "مسودة" && o.CreatedAt < draftCutoff)
                .ExecuteDeleteAsync();

            int deletedConfirmed = await _context.Operations
                .Where(o => o.Status != "مسودة" && o.CreatedAt < confirmedCutoff)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Manual cleanup of operations executed: {Drafts} drafts (> {DD}d), {Confirmed} confirmed (> {CD}d).", 
                deletedDrafts, draftDays, deletedConfirmed, confirmedDays);

            return Ok(new
            {
                success = true,
                deletedDrafts,
                deletedConfirmed,
                totalDeleted = deletedDrafts + deletedConfirmed,
                message = $"تم تنظيف {deletedDrafts} مسودة قديمة (> {draftDays} يوم) و {deletedConfirmed} عملية قديمة (> {confirmedDays} يوم) بنجاح"
            });
        }
    }
}