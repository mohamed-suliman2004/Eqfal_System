using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class WhatsAppController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<OperationsHub> _hubContext;
        private readonly ILogger<WhatsAppController> _logger;
        private readonly string _nodeUrl;
        private readonly string _apiKey;

        public WhatsAppController(
            IHttpClientFactory httpClientFactory,
            AppDbContext context,
            IConfiguration configuration,
            IHubContext<OperationsHub> hubContext,
            ILogger<WhatsAppController> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
            _logger = logger;
            _nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
            _apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var claims = User.Claims;
            var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                      || c.Type == "nameid" 
                                                      || c.Type == "sub" 
                                                      || c.Type.Contains("nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated properly" });
            }

            string instanceName = $"user_{userId}";
            var session = await _context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == userId);

            bool isConnected = false;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{_nodeUrl}/instance/connectionState/{instanceName}");
                req.Headers.Add("apikey", _apiKey);
                var res = await _httpClient.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var jsonStr = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonStr);
                    if (doc.RootElement.TryGetProperty("instance", out var instProp) &&
                        instProp.TryGetProperty("state", out var stateProp))
                    {
                        var state = stateProp.GetString();
                        isConnected = state != null && state.Equals("open", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to fetch Evolution API connectionState: {Msg}", ex.Message);
            }

            if (isConnected)
            {
                _ = SetInstanceWebhookAsync(instanceName);

                if (session == null)
                {
                    session = new WhatsAppSession { UserId = userId, Status = "متصل", LastConnected = DateTime.UtcNow };
                    _context.WhatsAppSessions.Add(session);
                }
                else
                {
                    session.Status = "متصل";
                    session.PairingCode = null;
                    session.LastConnected = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                return Ok(new { connected = true, status = "متصل" });
            }

            if (session != null && session.Status == "متصل")
            {
                session.Status = "غير متصل";
                await _context.SaveChangesAsync();
            }

            return Ok(new { connected = false, status = session?.Status ?? "غير متصل" });
        }

        public class PairRequest 
        { 
            public int? UserId { get; set; } 
            public string Phone { get; set; } = string.Empty; 
            public bool ForceReset { get; set; } = false;
        }

        [HttpPost("pair")]
        public async Task<IActionResult> GetPairingCode([FromBody] PairRequest req)
        {
            var claims = User.Claims;
            var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                      || c.Type == "nameid" 
                                                      || c.Type == "sub" 
                                                      || c.Type.Contains("nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated properly" });
            }

            int targetUserId = (req.UserId.HasValue && req.UserId.Value > 0) ? req.UserId.Value : userId;
            string instanceName = $"user_{targetUserId}";
            string cleanPhone = PhoneHelper.Normalize(req.Phone).Replace("+", "");

            if (string.IsNullOrWhiteSpace(cleanPhone))
            {
                return BadRequest(new { success = false, message = "رقم الهاتف غير صالح" });
            }

            try
            {
                _logger.LogInformation("[WhatsApp Pair] Requesting pairing code for {Instance}, Phone: {Phone}, ForceReset: {ForceReset}", instanceName, cleanPhone, req.ForceReset);

                // 0. Check if user is already connected (unless force reset is requested)
                if (!req.ForceReset)
                {
                    try
                    {
                        using var checkReq = new HttpRequestMessage(HttpMethod.Get, $"{_nodeUrl}/instance/connectionState/{instanceName}");
                        checkReq.Headers.Add("apikey", _apiKey);
                        var checkRes = await _httpClient.SendAsync(checkReq);
                        if (checkRes.IsSuccessStatusCode)
                        {
                            var jsonStr = await checkRes.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(jsonStr);
                            if (doc.RootElement.TryGetProperty("instance", out var instProp) &&
                                instProp.TryGetProperty("state", out var stateProp) &&
                                stateProp.GetString()?.Equals("open", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var sess = await _context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == targetUserId);
                                if (sess != null)
                                {
                                    sess.Status = "متصل";
                                    sess.PairingCode = null;
                                    sess.LastConnected = DateTime.UtcNow;
                                    await _context.SaveChangesAsync();
                                }
                                return Ok(new { success = true, alreadyConnected = true, message = "حساب واتساب متصل بالفعل." });
                            }
                        }
                    }
                    catch { }
                }

                // 1. Only do a hard reset if explicitly requested via req.ForceReset
                if (req.ForceReset)
                {
                    try
                    {
                        using var logoutReq = new HttpRequestMessage(HttpMethod.Delete, $"{_nodeUrl}/instance/logout/{instanceName}");
                        logoutReq.Headers.Add("apikey", _apiKey);
                        await _httpClient.SendAsync(logoutReq);
                    }
                    catch { }

                    await Task.Delay(400);

                    try
                    {
                        using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"{_nodeUrl}/instance/delete/{instanceName}");
                        delReq.Headers.Add("apikey", _apiKey);
                        await _httpClient.SendAsync(delReq);
                    }
                    catch { }

                    await Task.Delay(1000);
                }

                // 2. Ensure instance exists in Evolution API
                bool instanceReady = false;
                try
                {
                    using var checkStateReq = new HttpRequestMessage(HttpMethod.Get, $"{_nodeUrl}/instance/connectionState/{instanceName}");
                    checkStateReq.Headers.Add("apikey", _apiKey);
                    var checkStateRes = await _httpClient.SendAsync(checkStateReq);
                    if (checkStateRes.IsSuccessStatusCode)
                    {
                        instanceReady = true;
                    }
                }
                catch { }

                if (!instanceReady)
                {
                    try
                    {
                        using var createReq = new HttpRequestMessage(HttpMethod.Post, $"{_nodeUrl}/instance/create");
                        createReq.Headers.Add("apikey", _apiKey);
                        createReq.Content = JsonContent.Create(new
                        {
                            instanceName = instanceName,
                            qrcode = false,
                            integration = "WHATSAPP-BAILEYS"
                        });
                        var createResp = await _httpClient.SendAsync(createReq);
                        var createContent = await createResp.Content.ReadAsStringAsync();
                        _logger.LogInformation("[WhatsApp Pair] Create Instance Response ({Status}): {Content}", createResp.StatusCode, createContent);

                        if (createResp.IsSuccessStatusCode || createContent.Contains("already in use") || createContent.Contains("already exists"))
                        {
                            instanceReady = true;
                        }
                        else
                        {
                            // Minimal create fallback
                            using var minCreateReq = new HttpRequestMessage(HttpMethod.Post, $"{_nodeUrl}/instance/create");
                            minCreateReq.Headers.Add("apikey", _apiKey);
                            minCreateReq.Content = JsonContent.Create(new
                            {
                                instanceName = instanceName,
                                qrcode = false
                            });
                            var minResp = await _httpClient.SendAsync(minCreateReq);
                            var minContent = await minResp.Content.ReadAsStringAsync();
                            _logger.LogInformation("[WhatsApp Pair] Minimal Create Response ({Status}): {Content}", minResp.StatusCode, minContent);
                            if (minResp.IsSuccessStatusCode || minContent.Contains("already in use") || minContent.Contains("already exists"))
                            {
                                instanceReady = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[WhatsApp Pair] Error creating instance {Instance}", instanceName);
                    }

                    await Task.Delay(2500);
                }

                // 3. Set Webhook for instance
                await SetInstanceWebhookAsync(instanceName);

                // 4. Configure instance settings
                try
                {
                    using var setReq = new HttpRequestMessage(HttpMethod.Post, $"{_nodeUrl}/settings/set/{instanceName}");
                    setReq.Headers.Add("apikey", _apiKey);
                    setReq.Content = JsonContent.Create(new
                    {
                        rejectCall = false,
                        groupsIgnore = false,
                        alwaysOnline = true,
                        readMessages = false,
                        syncFullHistory = true
                    });
                    await _httpClient.SendAsync(setReq);
                }
                catch { }

                await Task.Delay(1000);

                // 5. Connect instance with Pairing Code (up to 4 retries)
                string code = "";
                string lastContent = "";
                for (int attempt = 1; attempt <= 4; attempt++)
                {
                    _logger.LogInformation("[WhatsApp Pair] Attempt {Attempt}/4 to connect with pairing code for {Instance}...", attempt, instanceName);
                    var response = await TryConnectWithPairingCodeAsync(instanceName, cleanPhone);
                    lastContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("[WhatsApp Pair] Attempt {Attempt} Response ({Status}): {Content}", attempt, response.StatusCode, lastContent);

                    if (response.IsSuccessStatusCode)
                    {
                        code = ExtractPairingCodeFromJson(lastContent);
                        if (!string.IsNullOrEmpty(code) && IsValidCode(code))
                        {
                            break;
                        }
                    }
                    else if (lastContent.Contains("does not exist"))
                    {
                        // Instance was not yet ready on server, recreate and delay
                        try
                        {
                            using var recReq = new HttpRequestMessage(HttpMethod.Post, $"{_nodeUrl}/instance/create");
                            recReq.Headers.Add("apikey", _apiKey);
                            recReq.Content = JsonContent.Create(new { instanceName = instanceName, qrcode = false });
                            await _httpClient.SendAsync(recReq);
                        }
                        catch { }
                    }

                    if (attempt < 4)
                    {
                        await Task.Delay(2500); // Wait 2.5s before retry
                    }
                }

                if (string.IsNullOrEmpty(code) || !IsValidCode(code))
                {
                    _logger.LogError("[WhatsApp Pair] Failed to get valid pairing code after 4 attempts. Last response: {Content}", lastContent);
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "تعذر توليد رمز الربط من خادم واتساب حالياً. يرجى الانتظار 15 ثانية وإعادة المحاولة.",
                        details = lastContent
                    });
                }

                // 8. Save in SQL
                var userExists = await _context.Users.AnyAsync(u => u.Id == targetUserId);
                if (userExists)
                {
                    var session = await _context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == targetUserId);
                    if (session == null)
                    {
                        session = new WhatsAppSession
                        {
                            UserId = targetUserId,
                            PairingCode = code,
                            Status = "في انتظار الربط",
                            LastConnected = null
                        };
                        _context.WhatsAppSessions.Add(session);
                    }
                    else
                    {
                        session.PairingCode = code;
                        session.Status = "في انتظار الربط";
                    }
                    await _context.SaveChangesAsync();
                }

                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                AuditLogger.Log(targetUserId, "WHATSAPP_PAIR_REQUEST", $"طلب رمز ربط واتساب للرقم: {cleanPhone} - الرمز: {code}", clientIp);

                return Ok(new { success = true, code = code, expiresInSeconds = 120 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WhatsApp Pair] Exception during pairing code request");
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء الاتصال بخادم واتساب: " + ex.Message });
            }
        }

        private static string ExtractPairingCodeFromJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "";

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // 1. Check root level
                if (root.TryGetProperty("pairingCode", out var pc) && pc.ValueKind == JsonValueKind.String)
                {
                    var val = pc.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(val) && IsValidCode(val)) return val;
                }
                if (root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    var val = c.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(val) && IsValidCode(val)) return val;
                }

                // 2. Check qrcode object
                if (root.TryGetProperty("qrcode", out var qr) && qr.ValueKind == JsonValueKind.Object)
                {
                    if (qr.TryGetProperty("pairingCode", out var qpc) && qpc.ValueKind == JsonValueKind.String)
                    {
                        var val = qpc.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(val) && IsValidCode(val)) return val;
                    }
                    if (qr.TryGetProperty("code", out var qc) && qc.ValueKind == JsonValueKind.String)
                    {
                        var val = qc.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(val) && IsValidCode(val)) return val;
                    }
                }

                // 3. Check nested pairingCode object: { "pairingCode": { "code": "..." } }
                if (root.TryGetProperty("pairingCode", out var pco) && pco.ValueKind == JsonValueKind.Object)
                {
                    if (pco.TryGetProperty("code", out var pcoc) && pcoc.ValueKind == JsonValueKind.String)
                    {
                        var val = pcoc.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(val) && IsValidCode(val)) return val;
                    }
                }
            }
            catch { }

            // 4. Fallback: Search for real pairing code (8 alphanumeric characters, MUST contain at least one digit, NOT English dictionary words)
            var matches = Regex.Matches(content, @"\b([A-Z0-9]{4}-?[A-Z0-9]{4})\b", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                string found = m.Groups[1].Value.ToUpper().Trim();
                if (IsValidCode(found))
                {
                    return found;
                }
            }

            return "";
        }

        private static bool IsValidCode(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return false;
            val = val.Trim().ToUpper().Replace("-", "");
            if (val.Length != 8) return false;

            // Blacklist common HTTP/JSON words that might have 8 letters
            string[] blacklist = {
                "RESPONSE", "REQUESTS", "BADREQST", "INTERNAL", "DATABASE",
                "MESSAGES", "INSTANCE", "STATUSES", "SETTINGS", "WEBHOOKS",
                "UNAUTHOR", "CONFLICT", "NOTFOUND", "FORBIDDN", "TIMEOUTS",
                "SUCCESSF", "CONNECTD", "ERRORMSG", "PASSWORD", "REGISTER"
            };
            if (blacklist.Contains(val)) return false;

            // A WhatsApp pairing code ALWAYS contains at least one digit!
            if (!val.Any(char.IsDigit)) return false;

            // Must only contain alphanumeric chars
            if (!val.All(char.IsLetterOrDigit)) return false;

            return true;
        }

        private async Task<HttpResponseMessage> TryConnectWithPairingCodeAsync(string instanceName, string cleanPhone)
        {
            using var connectReq = new HttpRequestMessage(HttpMethod.Get, $"{_nodeUrl}/instance/connect/{instanceName}?number={cleanPhone}");
            connectReq.Headers.Add("apikey", _apiKey);
            return await _httpClient.SendAsync(connectReq);
        }

        private async Task<(bool success, string response)> SetInstanceWebhookAsync(string instanceName)
        {
            string webhookUrl = (_configuration["WhatsAppService:WebhookUrl"] 
                ?? "https://eqfall.mostanad.ly/api/WhatsAppWebhook/receive").TrimEnd('/');

            string lastResponse = "";

            try
            {
                using var setWebhookReq = new HttpRequestMessage(HttpMethod.Post, $"{_nodeUrl}/webhook/set/{instanceName}");
                setWebhookReq.Headers.Add("apikey", _apiKey);
                setWebhookReq.Content = JsonContent.Create(new
                {
                    enabled = true,
                    url = webhookUrl,
                    byEvents = false,
                    base64 = false,
                    events = new[]
                    {
                        "CONNECTION_UPDATE",
                        "MESSAGES_UPSERT",
                        "MESSAGES_UPDATE",
                        "MESSAGES_DELETE",
                        "SEND_MESSAGE"
                    }
                });

                var response = await _httpClient.SendAsync(setWebhookReq);
                lastResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Webhook Set] Success with format 1 for {Instance}", instanceName);
                    return (true, lastResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Webhook Set] Format 1 failed for {Instance}: {Msg}", instanceName, ex.Message);
            }

            try
            {
                using var setWebhookReq2 = new HttpRequestMessage(HttpMethod.Post, $"{_nodeUrl}/webhook/set/{instanceName}");
                setWebhookReq2.Headers.Add("apikey", _apiKey);
                setWebhookReq2.Content = JsonContent.Create(new
                {
                    webhook = new
                    {
                        enabled = true,
                        url = webhookUrl,
                        byEvents = false,
                        base64 = false,
                        events = new[]
                        {
                            "CONNECTION_UPDATE",
                            "MESSAGES_UPSERT",
                            "MESSAGES_UPDATE",
                            "MESSAGES_DELETE",
                            "SEND_MESSAGE"
                        }
                    }
                });

                var response2 = await _httpClient.SendAsync(setWebhookReq2);
                lastResponse = await response2.Content.ReadAsStringAsync();

                if (response2.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Webhook Set] Success with format 2 for {Instance}", instanceName);
                    return (true, lastResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Webhook Set] Format 2 failed for {Instance}: {Msg}", instanceName, ex.Message);
            }

            return (false, lastResponse);
        }

        
        [Authorize]
        [HttpGet("sync-webhook")]
        [HttpPost("sync-webhook")]
        public async Task<IActionResult> SyncWebhook([FromQuery] int? targetUserIdParam)
        {
            var claims = User.Claims;
            var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                      || c.Type == "nameid" 
                                                      || c.Type == "sub" 
                                                      || c.Type.Contains("nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated properly" });
            }

            if (targetUserIdParam.HasValue && targetUserIdParam.Value > 0)
            {
                userId = targetUserIdParam.Value;
            }

            string instanceName = $"user_{userId}";
            var (success, response) = await SetInstanceWebhookAsync(instanceName);
            return Ok(new { success, instance = instanceName, response });
        }
[HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect([FromQuery] int? targetUserIdParam)
        {
            var claims = User.Claims;
            var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                      || c.Type == "nameid" 
                                                      || c.Type == "sub" 
                                                      || c.Type.Contains("nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated properly" });
            }

            if (targetUserIdParam.HasValue && targetUserIdParam.Value > 0)
            {
                userId = targetUserIdParam.Value;
            }

            string instanceName = $"user_{userId}";

            // 1. Logout and Delete instance on Evolution API
            try
            {
                using var logoutReq = new HttpRequestMessage(HttpMethod.Delete, $"{_nodeUrl}/instance/logout/{instanceName}");
                logoutReq.Headers.Add("apikey", _apiKey);
                await _httpClient.SendAsync(logoutReq);
            }
            catch { }

            try
            {
                using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"{_nodeUrl}/instance/delete/{instanceName}");
                delReq.Headers.Add("apikey", _apiKey);
                await _httpClient.SendAsync(delReq);
            }
            catch { }

            // 2. Update DB Session
            var session = await _context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (session != null)
            {
                session.Status = "غير متصل";
                session.PairingCode = null;
                await _context.SaveChangesAsync();
            }

            // 3. Notify real-time clients
            try { await _hubContext.Clients.User(userId.ToString()).SendAsync("WhatsAppStatusChanged", "غير متصل"); } catch { }
            try { await _hubContext.Clients.Group($"user_{userId}").SendAsync("WhatsAppStatusChanged", "غير متصل"); } catch { }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            AuditLogger.Log(userId, "WHATSAPP_DISCONNECT", $"تم قطع اتصال الواتساب للمستخدم: {userId}", clientIp);

            return Ok(new { success = true, status = "غير متصل" });
        }
    }
}