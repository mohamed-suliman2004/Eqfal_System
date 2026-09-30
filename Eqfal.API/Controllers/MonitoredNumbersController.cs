using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eqfal.API.Data;
using Eqfal.API.Helpers;
using Eqfal.API.Hubs;
using Eqfal.API.Models;
using Eqfal.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    [Authorize]
    public class MonitoredNumbersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MonitoredNumbersController> _logger;
        private readonly IHubContext<OperationsHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public MonitoredNumbersController(
            AppDbContext context, 
            IConfiguration configuration, 
            ILogger<MonitoredNumbersController> logger,
            IHubContext<OperationsHub> hubContext,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                           || c.Type == "nameid" 
                                                           || c.Type == "sub" 
                                                           || c.Type.Contains("nameidentifier"))?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            return 1;
        }

        private string SanitizeContactName(string? name, string fallbackPhone)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallbackPhone;
            
            // Clean up corrupted Ge'ez/Amharic or ANSI mojibake characters
            if (Regex.IsMatch(name, @"[\u1200-\u137F]") || Regex.IsMatch(name, @"[][\u00A0-\u00FF]"))
            {
                return "جهة اتصال";
            }

            return name.Trim();
        }

        // GET /MonitoredNumbers
        [HttpGet]
        public async Task<IActionResult> GetMonitoredNumbers()
        {
            try
            {
                var userId = GetCurrentUserId();

                var rawNumbers = await _context.MonitoredNumbers
                    .Where(m => m.UserId == userId)
                    .OrderByDescending(m => m.Id)
                    .ToListAsync();

                bool dbUpdated = false;

                // 1. Check for any LIDs that can be upgraded from LidMappings table
                var lidNumbers = rawNumbers.Where(m => !PhoneHelper.IsRealPhone(m.PhoneNumber)).ToList();
                if (lidNumbers.Any())
                {
                    var lids = lidNumbers.Select(m => PhoneHelper.Normalize(m.PhoneNumber)).Distinct().ToList();
                    var mappings = await _context.LidMappings
                        .Where(l => lids.Contains(l.Lid))
                        .ToDictionaryAsync(l => l.Lid, l => l.RealPhone);

                    foreach (var m in lidNumbers)
                    {
                        string norm = PhoneHelper.Normalize(m.PhoneNumber);
                        if (mappings.TryGetValue(norm, out var realPhone) && PhoneHelper.IsRealPhone(realPhone))
                        {
                            m.PhoneNumber = realPhone;
                            m.NormalizedPhone = PhoneHelper.Normalize(realPhone);
                            dbUpdated = true;
                        }
                    }

                    // 2. If some LIDs are still unresolved, attempt live sync from Evolution API contacts store
                    var remainingLids = rawNumbers.Where(m => !PhoneHelper.IsRealPhone(m.PhoneNumber)).ToList();
                    if (remainingLids.Any())
                    {
                        try
                        {
                            await SyncContactsFromEvolutionAsync(userId);
                            
                            // Re-check LidMappings
                            var freshMappings = await _context.LidMappings.ToDictionaryAsync(l => l.Lid, l => l.RealPhone);
                            foreach (var m in remainingLids)
                            {
                                string norm = PhoneHelper.Normalize(m.PhoneNumber);
                                if (freshMappings.TryGetValue(norm, out var realPhone) && PhoneHelper.IsRealPhone(realPhone))
                                {
                                    m.PhoneNumber = realPhone;
                                    m.NormalizedPhone = PhoneHelper.Normalize(realPhone);
                                    dbUpdated = true;
                                }
                            }
                        }
                        catch { }
                    }
                }

                foreach (var m in rawNumbers)
                {
                    string cleanedName = SanitizeContactName(m.ContactName, m.PhoneNumber ?? "");
                    if (cleanedName != m.ContactName)
                    {
                        m.ContactName = cleanedName;
                        dbUpdated = true;
                    }
                }

                if (dbUpdated)
                {
                    try { await _context.SaveChangesAsync(); } catch { }
                }

                var result = rawNumbers.Select(m => new {
                    id = m.Id,
                    phoneNumber = m.PhoneNumber,
                    contactName = m.ContactName,
                    isActive = m.IsActive
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching monitored numbers");
                return StatusCode(500, new { message = "حدث خطأ أثناء جلب الأرقام المراقبة" });
            }
        }

        private async Task SyncContactsFromEvolutionAsync(int userId)
        {
            string evolutionUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
            string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
            string instanceName = $"user_{userId}";

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{evolutionUrl}/chat/findContacts/{instanceName}");
            req.Headers.Add("apikey", apiKey);
            req.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            var res = await client.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                // Fallback to GET
                using var reqGet = new HttpRequestMessage(HttpMethod.Get, $"{evolutionUrl}/chat/findContacts/{instanceName}");
                reqGet.Headers.Add("apikey", apiKey);
                res = await client.SendAsync(reqGet);
            }

            if (res.IsSuccessStatusCode)
            {
                var jsonStr = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    bool newMappingsAdded = false;
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        string idStr = elem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                        string lidStr = elem.TryGetProperty("lid", out var lidProp) ? lidProp.GetString() ?? "" : "";
                        string pushName = elem.TryGetProperty("pushName", out var pProp) ? pProp.GetString() ?? "" 
                                        : (elem.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "");

                        string cleanLid = PhoneHelper.Normalize(lidStr);
                        string cleanPhone = PhoneHelper.Normalize(idStr);

                        if (!string.IsNullOrEmpty(cleanLid) && PhoneHelper.IsRealPhone(cleanPhone))
                        {
                            var existingMap = await _context.LidMappings.FirstOrDefaultAsync(l => l.Lid == cleanLid);
                            if (existingMap == null)
                            {
                                _context.LidMappings.Add(new LidMapping
                                {
                                    Lid = cleanLid,
                                    RealPhone = cleanPhone,
                                    ContactName = pushName,
                                    CreatedAt = DateTime.UtcNow
                                });
                                newMappingsAdded = true;
                            }
                        }
                    }

                    if (newMappingsAdded)
                    {
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

        public class MonitoredNumberRequest
        {
            public string PhoneNumber { get; set; } = string.Empty;
            public string? ContactName { get; set; }
        }

        // POST /MonitoredNumbers
        [HttpPost]
        public async Task<IActionResult> AddMonitoredNumber([FromBody] MonitoredNumberRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new { message = "رقم الهاتف مطلوب" });

            try
            {
                var userId = GetCurrentUserId();
                var normalized = PhoneHelper.Normalize(request.PhoneNumber);

                var existing = await _context.MonitoredNumbers
                    .FirstOrDefaultAsync(m => m.UserId == userId && (m.NormalizedPhone == normalized || m.PhoneNumber == request.PhoneNumber.Trim()));

                string cleanName = SanitizeContactName(request.ContactName, request.PhoneNumber.Trim());

                if (existing != null)
                {
                    existing.IsActive = true;
                    if (!string.IsNullOrWhiteSpace(cleanName))
                        existing.ContactName = cleanName;
                    await _context.SaveChangesAsync();

                    try 
                    { 
                        await _hubContext.Clients.User(userId.ToString()).SendAsync("MonitoredNumbersChanged");
                        await _hubContext.Clients.Group($"user_{userId}").SendAsync("MonitoredNumbersChanged");
                    } 
                    catch { }

                    var clientIpE = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                    AuditLogger.Log(userId, "MONITORED_NUMBER_UPDATED", $"تفعيل مراقبة الرقم: {existing.PhoneNumber} ({existing.ContactName})", clientIpE);

                    return Ok(new {
                        id = existing.Id,
                        phoneNumber = existing.PhoneNumber,
                        contactName = existing.ContactName,
                        isActive = existing.IsActive
                    });
                }

                var newNumber = new MonitoredNumber
                {
                    PhoneNumber = request.PhoneNumber.Trim(),
                    NormalizedPhone = normalized,
                    ContactName = cleanName,
                    UserId = userId,
                    IsActive = true,
                    };

                _context.MonitoredNumbers.Add(newNumber);
                await _context.SaveChangesAsync();

                try 
                { 
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("MonitoredNumbersChanged");
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("MonitoredNumbersChanged");
                } 
                catch { }

                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                AuditLogger.Log(userId, "MONITORED_NUMBER_ADDED", $"إضافة رقم مراقب: {newNumber.PhoneNumber} ({newNumber.ContactName})", clientIp);

                return Ok(new {
                    id = newNumber.Id,
                    phoneNumber = newNumber.PhoneNumber,
                    contactName = newNumber.ContactName,
                    isActive = newNumber.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding monitored number");
                return StatusCode(500, new { message = "حدث خطأ أثناء إضافة الرقم: " + ex.Message });
            }
        }

        public class UpdateMonitoredNumberRequest
        {
            public string? ContactName { get; set; }
            public string? PhoneNumber { get; set; }
            public bool? IsActive { get; set; }
        }

        // POST /MonitoredNumbers/5/toggle
        [HttpPost("{id}/toggle")]
        public async Task<IActionResult> ToggleMonitoredNumber(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var number = await _context.MonitoredNumbers.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

                if (number == null)
                    return NotFound(new { message = "الرقم غير موجود" });

                number.IsActive = !number.IsActive;
                await _context.SaveChangesAsync();

                try 
                { 
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("MonitoredNumbersChanged");
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("MonitoredNumbersChanged");
                } 
                catch { }

                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                string actionText = number.IsActive ? "تفعيل" : "إيقاف";
                AuditLogger.Log(userId, "MONITORED_NUMBER_TOGGLED", $"{actionText} مراقبة الرقم: {number.PhoneNumber} ({number.ContactName})", clientIp);

                return Ok(new {
                    id = number.Id,
                    phoneNumber = number.PhoneNumber,
                    contactName = number.ContactName,
                    isActive = number.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling monitored number");
                return StatusCode(500, new { message = "حدث خطأ أثناء تغيير حالة الرقم: " + ex.Message });
            }
        }

        // PUT /MonitoredNumbers/5
        [HttpPut("{id}")]
        [HttpPost("{id}/update")]
        [HttpPost("{id}/edit")]
        public async Task<IActionResult> UpdateMonitoredNumber(int id, [FromBody] UpdateMonitoredNumberRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var number = await _context.MonitoredNumbers.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

                if (number == null)
                    return NotFound(new { message = "الرقم غير موجود" });

                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    number.PhoneNumber = request.PhoneNumber.Trim();
                    number.NormalizedPhone = PhoneHelper.Normalize(request.PhoneNumber);
                }

                if (request.ContactName != null)
                    number.ContactName = SanitizeContactName(request.ContactName, number.PhoneNumber);

                if (request.IsActive.HasValue)
                    number.IsActive = request.IsActive.Value;

                await _context.SaveChangesAsync();

                try 
                { 
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("MonitoredNumbersChanged");
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("MonitoredNumbersChanged");
                } 
                catch { }

                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                AuditLogger.Log(userId, "MONITORED_NUMBER_UPDATED", $"تعديل بيانات الرقم: {number.PhoneNumber} ({number.ContactName})", clientIp);

                return Ok(new {
                    id = number.Id,
                    phoneNumber = number.PhoneNumber,
                    contactName = number.ContactName,
                    isActive = number.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating monitored number");
                return StatusCode(500, new { message = "حدث خطأ أثناء تعديل الرقم: " + ex.Message });
            }
        }

        // DELETE /MonitoredNumbers/5
        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteMonitoredNumber(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var number = await _context.MonitoredNumbers.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

                if (number == null)
                    return NotFound(new { message = "الرقم غير موجود" });

                _context.MonitoredNumbers.Remove(number);
                await _context.SaveChangesAsync();

                try 
                { 
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("MonitoredNumbersChanged");
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("MonitoredNumbersChanged");
                } 
                catch { }

                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                AuditLogger.Log(userId, "MONITORED_NUMBER_DELETED", $"حذف الرقم المراقب: {number.PhoneNumber}", clientIp);

                return Ok(new { message = "تم حذف الرقم بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting monitored number");
                return StatusCode(500, new { message = "حدث خطأ أثناء حذف الرقم: " + ex.Message });
            }
        }
    }
}