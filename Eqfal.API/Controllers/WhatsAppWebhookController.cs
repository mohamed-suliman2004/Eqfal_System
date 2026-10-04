using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Eqfal.API.Models;
using Eqfal.API.Hubs;
using Eqfal.API.Services;
using Microsoft.AspNetCore.SignalR;
using Eqfal.API.Helpers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace Eqfal.API.Controllers
{
    public class SecretEncryptedEditInfo
    {
        public string TargetMessageId { get; set; } = "";
        public string RemoteJid { get; set; } = "";
        public string EncPayload { get; set; } = "";
        public string EncIv { get; set; } = "";
        public bool FromMe { get; set; }
        public string Participant { get; set; } = "";
        public List<string> CandidateJids { get; set; } = new();
    }

    public class WebhookPayload
    {
        public int? UserId { get; set; }
        public string? From { get; set; }
        public string? ChatJid { get; set; }
        public string? Sender { get; set; }
        public string? SenderNumber { get; set; }
        public string? SenderPn { get; set; }
        public string? SenderLid { get; set; }
        public string? RemoteJidAlt { get; set; }
        public string? ParticipantAlt { get; set; }
        public string? Receiver { get; set; }
        public string? RawChatJid { get; set; }
        public string? RawSenderJid { get; set; }
        public string? Text { get; set; }
        public string? MessageText { get; set; }
        public bool FromMe { get; set; }
        public bool IsFromMe { get; set; }
        public bool IsGroup { get; set; }
        public bool IsLidChat { get; set; }
        public string? GroupName { get; set; }
        public string? PushName { get; set; }
        public string? InstanceName { get; set; }
        public string? Timestamp { get; set; }
        public string? MessageId { get; set; }
    }

    public class WhatsAppStatusRequest
    {
        public int UserId { get; set; }
        public string Status { get; set; } = "\u063a\u064a\u0631\u0020\u0645\u062a\u0635\u0644";
    }

    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class WhatsAppWebhookController : ControllerBase
    {
        private static readonly System.Collections.Concurrent.ConcurrentQueue<string> _recentRawWebhooks = new();
        private static readonly ConcurrentDictionary<string, string> _messageSecretCache = new();
        private static readonly ConcurrentDictionary<int, (bool isActive, DateTime expiresAt)> _userSubscriptionCache = new();
        private readonly AppDbContext _context;
        private readonly IHubContext<OperationsHub> _hubContext;
        private readonly IMessageAnalysisService _analysisService;
        private readonly ILogger<WhatsAppWebhookController> _logger;
        private readonly IConfiguration _configuration;

        public WhatsAppWebhookController(
            AppDbContext context,
            IHubContext<OperationsHub> hubContext,
            IMessageAnalysisService analysisService,
            ILogger<WhatsAppWebhookController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _hubContext = hubContext;
            _analysisService = analysisService;
            _logger = logger;
            _configuration = configuration;
        }

        private async Task<(string realPhone, string contactName)> ResolveContactInfoAsync(
            string? jid, string? senderPn, string? senderLid, string? rawSender, string? fallbackName, string? instanceName = null, int userId = 1, bool isFromMe = false)
        {
            string name = fallbackName ?? "";
            
            // 1. senderPn from Evolution API (most reliable source)
            if (!string.IsNullOrWhiteSpace(senderPn))
            {
                string pnDigits = PhoneHelper.Normalize(senderPn);
                if (PhoneHelper.IsRealPhone(pnDigits)) return (pnDigits, name);
            }

            // 2. jid is a standard phone (@s.whatsapp.net)
            if (!string.IsNullOrEmpty(jid) && !jid.Contains("@lid"))
            {
                string digits = PhoneHelper.Normalize(jid);
                if (PhoneHelper.IsRealPhone(digits)) return (digits, name);
            }

            // 3. rawSender might be a real phone
            if (!string.IsNullOrWhiteSpace(rawSender))
            {
                string sDigits = PhoneHelper.Normalize(rawSender);
                if (PhoneHelper.IsRealPhone(sDigits)) return (sDigits, name);
            }

            // 4. Evolution API single number check
            string evolutionUrl = _configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080";
            string evolutionApiKey = _configuration["WhatsAppService:ApiKey"] ?? "";
            string instanceToUse = !string.IsNullOrWhiteSpace(instanceName) ? instanceName : $"user_{userId}";

            var bestLidCandidate = new[] { senderLid, jid, rawSender }
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) && PhoneHelper.IsLid(s));

            if (bestLidCandidate != null)
            {
                string resolved = await PhoneHelper.ResolveLidViaEvolutionApiAsync(bestLidCandidate, evolutionUrl, instanceToUse, evolutionApiKey);
                if (PhoneHelper.IsRealPhone(resolved)) return (resolved, name);
            }

            // 5. Check existing MonitoredNumbers for this LID
            string jidDigits = PhoneHelper.Normalize(jid);
            if (!string.IsNullOrEmpty(jidDigits))
            {
                var existing = await _context.MonitoredNumbers
                    .FirstOrDefaultAsync(m => m.UserId == userId && 
                        (m.NormalizedPhone == jidDigits || m.PhoneNumber == jidDigits));
                if (existing != null && PhoneHelper.IsRealPhone(existing.PhoneNumber))
                {
                    return (existing.PhoneNumber, !string.IsNullOrWhiteSpace(existing.ContactName) ? existing.ContactName : name);
                }
            }

            return ("", name);
        }

        [HttpPost("receive")]
        [HttpPost]
        public async Task<IActionResult> HandleIncomingWebhook([FromBody] JsonElement root)
        {
            try
            {
                _logger.LogInformation("[Webhook] Raw payload received: {Payload}", root.GetRawText().Length > 500 ? root.GetRawText()[..500] : root.GetRawText());

                _recentRawWebhooks.Enqueue($"[{DateTime.UtcNow:HH:mm:ss}] {root.GetRawText()}");
                while (_recentRawWebhooks.Count > 40) _recentRawWebhooks.TryDequeue(out _);

                if (root.TryGetProperty("event", out _) || 
                    (root.TryGetProperty("data", out _) && root.TryGetProperty("instance", out _)))
                {
                    return await ProcessEvolutionWebhook(root);
                }

                var payload = JsonSerializer.Deserialize<WebhookPayload>(root.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (payload != null)
                {
                    return await ProcessStandardPayload(payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling webhook payload");
            }
            return Ok();
        }

        [HttpPost("evolution-receive")]
        public async Task<IActionResult> EvolutionWebhook([FromBody] JsonElement root)
        {
            return await ProcessEvolutionWebhook(root);
        }

        private async Task<IActionResult> ProcessEvolutionWebhook(JsonElement root)
        {
            try
            {
                string eventName = "";
                if (root.TryGetProperty("event", out var evProp)) eventName = evProp.GetString() ?? "";

                string instanceName = "";
                if (root.TryGetProperty("instance", out var instProp)) 
                {
                    if (instProp.ValueKind == JsonValueKind.String)
                        instanceName = instProp.GetString() ?? "";
                    else if (instProp.ValueKind == JsonValueKind.Object && instProp.TryGetProperty("instanceName", out var inProp))
                        instanceName = inProp.GetString() ?? "";
                }
                
                int userId = 1;
                if (!string.IsNullOrEmpty(instanceName))
                {
                    var match = Regex.Match(instanceName, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out int parsedId) && parsedId > 0)
                    {
                        userId = parsedId;
                    }
                }

                _logger.LogInformation("[Evolution Webhook] Event: {Event}, Instance: {Instance}, UserId: {UserId}", eventName, instanceName, userId);

                // Auto-sync webhook configuration for active instance in background
                if (!string.IsNullOrEmpty(instanceName))
                {
                    _ = EnsureInstanceWebhookConfiguredAsync(instanceName);
                }

                // 1. Connection update events
                if (eventName.Contains("connection", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("data", out var dataElem))
                {
                    string state = "";
                    if (dataElem.ValueKind == JsonValueKind.Object)
                    {
                        if (dataElem.TryGetProperty("state", out var st)) state = st.GetString() ?? "";
                        if (string.IsNullOrEmpty(state) && dataElem.TryGetProperty("status", out var stProp)) state = stProp.GetString() ?? "";
                    }
                    
                    string status = state.Equals("open", StringComparison.OrdinalIgnoreCase) ? "\u0645\u062a\u0635\u0644" : state.Equals("connecting", StringComparison.OrdinalIgnoreCase) ? "\u0641\u064a\u0020\u0627\u0646\u062a\u0638\u0627\u0631\u0020\u0627\u0644\u0631\u0628\u0637" : "\u063a\u064a\u0631\u0020\u0645\u062a\u0635\u0644";
                    var session = await _context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (session != null)
                    {
                        session.Status = status;
                        if (status == "\u0645\u062a\u0635\u0644") { session.LastConnected = DateTime.UtcNow; session.PairingCode = null; }
                        await _context.SaveChangesAsync();
                    }
                    try { await _hubContext.Clients.User(userId.ToString()).SendAsync("WhatsAppStatusChanged", status); } catch { }
                    try { await _hubContext.Clients.Group($"user_{userId}").SendAsync("WhatsAppStatusChanged", status); } catch { }
                    return Ok();
                }

                // 2. التحقق من صلاحية اشتراك المستخدم ونشاط الحساب لمنع معالجة أي رسائل للحسابات المنتهية
                bool isSubActive = await IsUserSubscriptionActiveAsync(userId);
                if (!isSubActive)
                {
                    _logger.LogWarning("[WhatsApp Webhook Dropped] Message/event '{Event}' dropped for UserId={UserId} because subscription is expired or account inactive.", eventName, userId);

                    if (!string.IsNullOrEmpty(instanceName))
                    {
                        _ = AutoDisconnectExpiredInstanceAsync(instanceName, userId);
                    }

                    return Ok(new { status = "DroppedDueToExpiredSubscription", userId = userId, eventName = eventName });
                }

                // 3. Check for Message Revocations / Delete for everyone across all event types
                var revokedTargets = ExtractRevocationTargets(root, eventName);
                if (revokedTargets.Count > 0)
                {
                    foreach (var target in revokedTargets)
                    {
                        _logger.LogInformation("[WhatsApp Revocation Detected] TargetId={TargetId}, RemoteJid={RemoteJid}, UserId={UserId}", target.messageId, target.remoteJid, userId);
                        await HandleMessageRevocationAsync(userId, target.messageId, target.remoteJid);
                    }
                    return Ok(new { status = "RevokedHandled", count = revokedTargets.Count, targets = revokedTargets.Select(t => t.messageId) });
                }

                // If event is explicitly delete / revoke but no target ID was extracted, use remoteJid or recent fallback
                if (eventName.Contains("delete", StringComparison.OrdinalIgnoreCase) || eventName.Contains("revoke", StringComparison.OrdinalIgnoreCase))
                {
                    string fallbackJid = "";
                    if (root.TryGetProperty("data", out var dEl))
                    {
                        if (dEl.TryGetProperty("remoteJid", out var rjEl)) fallbackJid = rjEl.GetString() ?? "";
                    }
                    _logger.LogInformation("[WhatsApp Delete Fallback] No specific target ID found, revoking latest for UserId={UserId}, RemoteJid={RemoteJid}", userId, fallbackJid);
                    await HandleMessageRevocationAsync(userId, "", fallbackJid);
                    return Ok(new { status = "DeleteFallbackHandled" });
                }

                // 3. Check for Message Edits (WhatsApp edit message feature)
                var editTargets = await ExtractEditTargetsAsync(root, instanceName, userId);
                if (editTargets.Count > 0)
                {
                    foreach (var edit in editTargets)
                    {
                        _logger.LogInformation("[WhatsApp Edit Detected] TargetId={TargetId}, RemoteJid={RemoteJid}, NewText={Text}", edit.TargetMessageId, edit.RemoteJid, edit.NewText);
                        await HandleMessageEditAsync(userId, edit.TargetMessageId, edit.RemoteJid, edit.NewText);
                    }
                    return Ok(new { status = "EditsHandled", count = editTargets.Count });
                }

                // 4. Message processing (upsert / send)
                if ((eventName.Contains("messages", StringComparison.OrdinalIgnoreCase) 
                  || eventName.Contains("send", StringComparison.OrdinalIgnoreCase)
                  || eventName.Contains("upsert", StringComparison.OrdinalIgnoreCase)) 
                  && root.TryGetProperty("data", out var msgData))
                {
                    JsonElement actualMsg = msgData;
                    
                    if (msgData.ValueKind == JsonValueKind.Array && msgData.GetArrayLength() > 0)
                    {
                        actualMsg = msgData[0];
                    }
                    else if (msgData.ValueKind == JsonValueKind.Object && msgData.TryGetProperty("messages", out var msgsArr) && msgsArr.ValueKind == JsonValueKind.Array && msgsArr.GetArrayLength() > 0)
                    {
                        actualMsg = msgsArr[0];
                    }

                    if (actualMsg.ValueKind != JsonValueKind.Object) return Ok();

                    var keyElem = (actualMsg.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.Object) ? k : default;
                    string rawMsgId = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("id", out var idProp)) ? idProp.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(rawMsgId))
                    {
                        string? cachedSec = null;
                        var cachedJids = new List<string>();
                        FindMessageSecretAndJidsRecursive(actualMsg, ref cachedSec, cachedJids);
                        if (!string.IsNullOrEmpty(cachedSec))
                        {
                            _messageSecretCache[rawMsgId] = cachedSec;
                        }
                    }
                    string remoteJid = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("remoteJid", out var rj)) ? rj.GetString() ?? "" : "";
                    string remoteJidAlt = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("remoteJidAlt", out var rja)) ? rja.GetString() ?? "" : "";
                    string senderPn = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("senderPn", out var spn)) ? spn.GetString() ?? "" : "";
                    string senderLid = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("senderLid", out var slid)) ? slid.GetString() ?? "" : "";
                    bool fromMe = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("fromMe", out var fm)) && fm.ValueKind == JsonValueKind.True;
                    string participant = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("participant", out var p)) ? p.GetString() ?? "" : "";
                    string participantAlt = (keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("participantAlt", out var pa)) ? pa.GetString() ?? "" : "";
                    string pushName = (actualMsg.TryGetProperty("pushName", out var pn)) ? pn.GetString() ?? "" : "";

                    if (string.IsNullOrEmpty(remoteJidAlt) && actualMsg.TryGetProperty("remoteJidAlt", out var mrja))
                        remoteJidAlt = mrja.GetString() ?? "";
                    if (string.IsNullOrEmpty(participantAlt) && actualMsg.TryGetProperty("participantAlt", out var mpa))
                        participantAlt = mpa.GetString() ?? "";

                    if (string.IsNullOrEmpty(senderPn) && !string.IsNullOrEmpty(remoteJidAlt) && PhoneHelper.IsRealPhone(PhoneHelper.Normalize(remoteJidAlt)))
                        senderPn = remoteJidAlt;
                    if (string.IsNullOrEmpty(senderPn) && !string.IsNullOrEmpty(participantAlt) && PhoneHelper.IsRealPhone(PhoneHelper.Normalize(participantAlt)))
                        senderPn = participantAlt;

                    if (string.IsNullOrEmpty(senderPn) && actualMsg.TryGetProperty("senderPn", out var spn2))
                        senderPn = spn2.GetString() ?? "";
                    if (string.IsNullOrEmpty(senderPn) && actualMsg.TryGetProperty("sender", out var sProp))
                    {
                        var sStr = sProp.GetString() ?? "";
                        if (sStr.EndsWith("@s.whatsapp.net")) senderPn = sStr;
                    }
                    if (string.IsNullOrEmpty(senderPn) && actualMsg.TryGetProperty("participant", out var pProp))
                    {
                        var pStr = pProp.GetString() ?? "";
                        if (pStr.EndsWith("@s.whatsapp.net")) senderPn = pStr;
                    }
                    if (string.IsNullOrEmpty(senderPn) && keyElem.ValueKind == JsonValueKind.Object && keyElem.TryGetProperty("participant", out var kpProp))
                    {
                        var kpStr = kpProp.GetString() ?? "";
                        if (kpStr.EndsWith("@s.whatsapp.net")) senderPn = kpStr;
                    }

                    bool isGroup = remoteJid.EndsWith("@g.us");
                    string groupSender = !string.IsNullOrEmpty(participant) ? participant : (fromMe ? "" : remoteJid);
                    string effectiveSender = !string.IsNullOrEmpty(senderPn) ? senderPn : groupSender;

                    string text = "";
                    if (actualMsg.TryGetProperty("message", out var msgObj) && msgObj.ValueKind == JsonValueKind.Object)
                    {
                        if (msgObj.TryGetProperty("conversation", out var conv) && !string.IsNullOrEmpty(conv.GetString()))
                            text = conv.GetString() ?? "";
                        else if (msgObj.TryGetProperty("extendedTextMessage", out var ext) && ext.ValueKind == JsonValueKind.Object && ext.TryGetProperty("text", out var extText))
                            text = extText.GetString() ?? "";
                        else if (msgObj.TryGetProperty("imageMessage", out var img) && img.ValueKind == JsonValueKind.Object && img.TryGetProperty("caption", out var imgCap))
                            text = imgCap.GetString() ?? "";
                        else if (msgObj.TryGetProperty("documentMessage", out var doc) && doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("caption", out var docCap))
                            text = docCap.GetString() ?? "";
                        else if (msgObj.TryGetProperty("videoMessage", out var vid) && vid.ValueKind == JsonValueKind.Object && vid.TryGetProperty("caption", out var vidCap))
                            text = vidCap.GetString() ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        if (actualMsg.TryGetProperty("text", out var tProp) && !string.IsNullOrEmpty(tProp.GetString()))
                            text = tProp.GetString() ?? "";
                        else if (actualMsg.TryGetProperty("body", out var bProp) && !string.IsNullOrEmpty(bProp.GetString()))
                            text = bProp.GetString() ?? "";
                        else if (actualMsg.TryGetProperty("messageBody", out var mbProp) && !string.IsNullOrEmpty(mbProp.GetString()))
                            text = mbProp.GetString() ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(text)) return Ok();

                    _logger.LogInformation("[Evolution MSG] remoteJid={RemoteJid}, senderPn={SenderPn}, senderLid={SenderLid}, participant={Participant}, fromMe={FromMe}, pushName={PushName}, text={Text}",
                        remoteJid, senderPn, senderLid, participant, fromMe, pushName, text.Length > 50 ? text[..50] : text);

                    var payload = new WebhookPayload
                    {
                        UserId = userId,
                        From = remoteJid,
                        ChatJid = remoteJid,
                        Sender = effectiveSender,
                        SenderNumber = effectiveSender,
                        SenderPn = senderPn,
                        SenderLid = senderLid,
                        RemoteJidAlt = remoteJidAlt,
                        ParticipantAlt = participantAlt,
                        RawChatJid = remoteJid,
                        RawSenderJid = !string.IsNullOrEmpty(senderLid) ? senderLid : remoteJid,
                        Text = text,
                        MessageText = text,
                        FromMe = fromMe,
                        IsFromMe = fromMe,
                        IsGroup = isGroup,
                        IsLidChat = remoteJid.EndsWith("@lid"),
                        GroupName = isGroup ? pushName : "",
                        PushName = pushName,
                        InstanceName = instanceName,
                        MessageId = rawMsgId
                    };

                    return await ProcessStandardPayload(payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Evolution API webhook");
            }
            return Ok();
        }

        private async Task<IActionResult> ProcessStandardPayload(WebhookPayload payload)
        {
            string rawText = !string.IsNullOrWhiteSpace(payload.Text) ? payload.Text : payload.MessageText ?? "";
            string rawFrom = !string.IsNullOrWhiteSpace(payload.From) ? payload.From : payload.ChatJid ?? "";
            string rawSender = !string.IsNullOrWhiteSpace(payload.Sender) ? payload.Sender : payload.SenderNumber ?? "";
            bool isFromMe = payload.FromMe || payload.IsFromMe;

            if (!payload.UserId.HasValue || payload.UserId.Value <= 0)
            {
                return BadRequest(new { error = "UserId is missing or invalid in webhook payload" });
            }

            int userId = payload.UserId.Value;

            // التحقق من صلاحية اشتراك المستخدم ونشاط الحساب
            bool isSubActive = await IsUserSubscriptionActiveAsync(userId);
            if (!isSubActive)
            {
                _logger.LogWarning("[Standard Webhook Dropped] Message ignored for UserId={UserId} because subscription is expired or account inactive.", userId);
                _ = AutoDisconnectExpiredInstanceAsync($"user_{userId}", userId);
                return Ok(new { status = "DroppedDueToExpiredSubscription", userId = userId });
            }

            var userObj = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            string userPhone = PhoneHelper.Normalize(userObj?.Phone);
            string userFullName = userObj?.FullName?.Trim() ?? "";

            if (!isFromMe)
            {
                string normSender = PhoneHelper.Normalize(rawSender);
                string normPn = PhoneHelper.Normalize(payload.SenderPn);
                if (!string.IsNullOrEmpty(userPhone) && (normSender == userPhone || normPn == userPhone))
                {
                    isFromMe = true;
                }

                if (!isFromMe && !string.IsNullOrEmpty(userFullName) && !string.IsNullOrEmpty(payload.PushName))
                {
                    if (payload.PushName.Equals(userFullName, StringComparison.OrdinalIgnoreCase) ||
                        userFullName.Contains(payload.PushName, StringComparison.OrdinalIgnoreCase) ||
                        payload.PushName.Contains(userFullName, StringComparison.OrdinalIgnoreCase))
                    {
                        isFromMe = true;
                    }
                }
            }

            _logger.LogInformation("[Webhook Process] From: {From}, Sender: {Sender}, SenderPn: {SenderPn}, PushName: {PushName}, UserId: {UserId}, IsGroup: {IsGroup}, IsFromMe: {IsFromMe}, Text: {Text}", 
                rawFrom, rawSender, payload.SenderPn, payload.PushName, userId, payload.IsGroup, isFromMe, rawText?.Length > 80 ? rawText[..80] : rawText);

            if (string.IsNullOrWhiteSpace(rawText))
                return Ok();

            // Privacy & Disconnection check: if user unlinked/disconnected WhatsApp, immediately drop all messages
            var activeSession = await _context.WhatsAppSessions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            if (activeSession == null || activeSession.Status != "متصل")
            {
                _logger.LogInformation("[Webhook Dropped] User {UserId} WhatsApp is disconnected ({Status}). Dropping message.", userId, activeSession?.Status ?? "None");
                return Ok(new { status = "Ignored", reason = "User session is disconnected" });
            }

            string text = rawText.Trim();
            text = text.Replace('\u0660', '0').Replace('\u0661', '1').Replace('\u0662', '2')
                       .Replace('\u0663', '3').Replace('\u0664', '4').Replace('\u0665', '5')
                       .Replace('\u0666', '6').Replace('\u0667', '7').Replace('\u0668', '8').Replace('\u0669', '9');
            text = Regex.Replace(text, @"([\p{L}])(\d+)", "$1 $2");
            text = Regex.Replace(text, @"(\d+)([\p{L}])", "$1 $2");

            var (resolvedPhone, resolvedName) = await ResolveContactInfoAsync(
                rawFrom, payload.SenderPn, payload.SenderLid, rawSender, 
                !string.IsNullOrWhiteSpace(payload.GroupName) ? payload.GroupName : payload.PushName,
                payload.InstanceName, userId, isFromMe
            );

            string chatJid        = PhoneHelper.Normalize(rawFrom);
            string senderNumber   = PhoneHelper.Normalize(!string.IsNullOrEmpty(rawSender) ? rawSender : rawFrom);
            string receiverNumber = PhoneHelper.Normalize(payload.Receiver);
            string rawChatJid     = PhoneHelper.Normalize(payload.RawChatJid);
            string rawSenderJid   = PhoneHelper.Normalize(payload.RawSenderJid);

            _logger.LogInformation("[Phone Resolution] resolvedPhone={ResolvedPhone}, chatJid={ChatJid}, senderNumber={SenderNumber}, rawChatJid={RawChatJid}",
                resolvedPhone, chatJid, senderNumber, rawChatJid);

            // Auto-learn LID to Real Phone mapping on the fly if present
            if (!payload.IsGroup)
            {
                string? candidateLid = new[] { payload.SenderLid, rawSender, rawFrom }.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) && PhoneHelper.IsLid(s));
                string? candidatePhone = new[] { resolvedPhone, payload.SenderPn, senderNumber }.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) && PhoneHelper.IsRealPhone(s));

                if (!string.IsNullOrEmpty(candidateLid) && !string.IsNullOrEmpty(candidatePhone))
                {
                    string normLid = PhoneHelper.Normalize(candidateLid);
                    string normPhone = PhoneHelper.Normalize(candidatePhone);

                    if (!string.IsNullOrEmpty(normLid) && !string.IsNullOrEmpty(normPhone) && normLid != normPhone)
                    {
                        var existingLid = await _context.LidMappings.FirstOrDefaultAsync(l => l.Lid == normLid);
                        if (existingLid == null)
                        {
                            _context.LidMappings.Add(new LidMapping
                            {
                                Lid = normLid,
                                RealPhone = normPhone,
                                ContactName = payload.PushName,
                                CreatedAt = DateTime.UtcNow
                            });
                            try { await _context.SaveChangesAsync(); } catch { }
                        }
                    }
                }
            }

            string normText = text
                .Replace("\u0623", "\u0627").Replace("\u0625", "\u0627").Replace("\u0622", "\u0627")
                .Replace("\u0629", "\u0647").Replace("\u0649", "\u064a");
            string compactText = normText.Replace(" ", "").Replace("\t", "");

            // Activating: #اقفال / اقفال / #تفعيل / تفعيل / #مراقبة / #قفل / #lock / #monitor
            bool isActivating = compactText.Contains("#\u0627\u0642\u0641\u0627\u0644") || compactText.Contains("\u0627\u0642\u0641\u0627\u0644")
                             || compactText.Contains("#\u062a\u0641\u0639\u064a\u0644") || compactText.Contains("\u062a\u0641\u0639\u064a\u0644")
                             || compactText.Contains("#\u0645\u0631\u0627\u0642\u0628") || compactText.Contains("\u0645\u0631\u0627\u0642\u0628")
                             || compactText.Contains("#\u0642\u0641\u0644") || compactText.Contains("\u0642\u0641\u0644")
                             || compactText.Contains("#lock") || compactText.Contains("#monitor") || compactText.Contains("#track");

            // Deactivating: #ايقاف / ايقاف / #تعطيل / تعطيل / #الغاء / الغاء / #وقف / #stop / #unlock
            bool isDeactivating = compactText.Contains("#\u0627\u064a\u0642\u0627\u0641") || compactText.Contains("\u0627\u064a\u0642\u0627\u0641")
                               || compactText.Contains("#\u062a\u0639\u0637\u064a\u0644") || compactText.Contains("\u062a\u0639\u0637\u064a\u0644")
                               || compactText.Contains("#\u0627\u0644\u063a\u0627\u0621") || compactText.Contains("\u0627\u0644\u063a\u0627\u0621")
                               || compactText.Contains("#\u0648\u0642\u0641") || compactText.Contains("\u0648\u0642\u0641")
                               || compactText.Contains("#stop") || compactText.Contains("#unlock") || compactText.Contains("#unmonitor");
            bool isCommand = isActivating || isDeactivating;

            if (isCommand)
            {
                bool isGroup = payload.IsGroup;
                int targetUserId = userId;

                string phoneToSave = "";
                if (isGroup)
                {
                    // For groups, always save the group identifier
                    phoneToSave = !string.IsNullOrEmpty(chatJid) ? chatJid : rawChatJid;
                }
                else
                {
                    if (PhoneHelper.IsRealPhone(resolvedPhone))
                        phoneToSave = resolvedPhone;
                    else if (PhoneHelper.IsRealPhone(PhoneHelper.Normalize(payload.SenderPn)))
                        phoneToSave = PhoneHelper.Normalize(payload.SenderPn);
                    else if (PhoneHelper.IsRealPhone(senderNumber))
                        phoneToSave = senderNumber;
                    else if (PhoneHelper.IsRealPhone(chatJid))
                        phoneToSave = chatJid;
                    else
                    {
                        phoneToSave = !string.IsNullOrEmpty(chatJid) ? chatJid : rawChatJid;
                        _logger.LogInformation("[Command #اقفال] Saved identifier: {Phone}", phoneToSave);
                    }
                }

                string defaultName = isGroup 
                    ? (!string.IsNullOrWhiteSpace(resolvedName) ? resolvedName : (!string.IsNullOrWhiteSpace(payload.GroupName) ? payload.GroupName : "ظ…ط¬ظ…ظˆط¹ط©")) 
                    : (!string.IsNullOrWhiteSpace(resolvedName) ? resolvedName : (!isFromMe && !string.IsNullOrWhiteSpace(payload.PushName) ? payload.PushName : phoneToSave));

                _logger.LogInformation("[Command] isActivating={IsActivating}, phoneToSave={Phone}, name={Name}, isGroup={IsGroup}",
                    isActivating, phoneToSave, defaultName, isGroup);

                var allTargets = new[] { 
                    phoneToSave, PhoneHelper.Normalize(phoneToSave), 
                    chatJid, rawChatJid, rawSenderJid, 
                    resolvedPhone, PhoneHelper.Normalize(payload.SenderPn),
                    senderNumber
                }.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

                var existingNumber = await _context.MonitoredNumbers
                    .FirstOrDefaultAsync(m => m.UserId == targetUserId && (allTargets.Contains(m.NormalizedPhone) || allTargets.Contains(m.PhoneNumber)));
                
                if (existingNumber == null)
                {
                    if (isActivating)
                    {
                        _context.MonitoredNumbers.Add(new MonitoredNumber
                        {
                            PhoneNumber = phoneToSave,
                            NormalizedPhone = PhoneHelper.Normalize(phoneToSave),
                            ContactName = defaultName,
                            UserId = targetUserId,
                            IsActive = true
                        });
                        _logger.LogInformation("[MonitoredNumber ADDED] Phone={Phone}, Name={Name}, UserId={UserId}", phoneToSave, defaultName, targetUserId);
                    }
                }
                else
                {
                    existingNumber.IsActive = isActivating;
                    if (PhoneHelper.IsRealPhone(resolvedPhone) && !PhoneHelper.IsRealPhone(existingNumber.PhoneNumber))
                    {
                        existingNumber.PhoneNumber = resolvedPhone;
                        existingNumber.NormalizedPhone = PhoneHelper.Normalize(resolvedPhone);
                    }
                    if (!string.IsNullOrWhiteSpace(defaultName) && defaultName != "ط±ظ‚ظ… ظ…ط±ط§ظ‚ط¨" && defaultName != "ظ…ط¬ظ…ظˆط¹ط©")
                    {
                        existingNumber.ContactName = defaultName;
                    }
                    _logger.LogInformation("[MonitoredNumber UPDATED] Id={Id}, Phone={Phone}, Active={Active}", existingNumber.Id, existingNumber.PhoneNumber, isActivating);
                }

                await _context.SaveChangesAsync();

                try { await _hubContext.Clients.User(targetUserId.ToString()).SendAsync("MonitoredNumbersChanged"); } catch { }
                try { await _hubContext.Clients.Group($"user_{targetUserId}").SendAsync("MonitoredNumbersChanged"); } catch { }

                AuditLogger.Log(targetUserId, isActivating ? "MONITORING_ENABLED" : "MONITORING_DISABLED", $"تحديث حالة المراقبة ({(isActivating ? "تفعيل" : "تعطيل")}) للمحادثة: {phoneToSave} ({defaultName})", "WhatsApp");
                return Ok(new { status = "Command processed", phone = phoneToSave, name = defaultName, activating = isActivating });
            }
            
            var userMonitoredNumbers = await _context.MonitoredNumbers
                .Where(m => m.UserId == userId && m.IsActive)
                .ToListAsync();

            var identifiers = new[] { 
                resolvedPhone, chatJid, senderNumber, rawChatJid, rawSenderJid, receiverNumber, 
                PhoneHelper.Normalize(payload.SenderPn), PhoneHelper.Normalize(payload.RemoteJidAlt), PhoneHelper.Normalize(payload.ParticipantAlt) 
            }.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            var mappedEntries = await _context.LidMappings
                .AsNoTracking()
                .Where(l => identifiers.Contains(l.Lid) || identifiers.Contains(l.RealPhone))
                .ToListAsync();

            foreach (var m in mappedEntries)
            {
                if (!identifiers.Contains(m.RealPhone)) identifiers.Add(m.RealPhone);
                if (!identifiers.Contains(m.Lid)) identifiers.Add(m.Lid);
            }

            var matchedMonitoredNumber = userMonitoredNumbers.FirstOrDefault(m =>
                identifiers.Any(id => m.NormalizedPhone == id || m.PhoneNumber == id) ||
                m.PhoneNumber == payload.Sender ||
                m.PhoneNumber == payload.From ||
                m.PhoneNumber == rawFrom
            );

            if (matchedMonitoredNumber == null && !string.IsNullOrWhiteSpace(payload.PushName))
            {
                matchedMonitoredNumber = userMonitoredNumbers.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(m.ContactName) &&
                    (m.ContactName.Trim().Equals(payload.PushName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                     m.ContactName.Trim().Contains(payload.PushName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                     payload.PushName.Trim().Contains(m.ContactName.Trim(), StringComparison.OrdinalIgnoreCase)));
            }

            if (matchedMonitoredNumber == null)
            {
                _logger.LogInformation("[Ignored] Not monitored. Identifiers: [{Ids}], PushName: {PushName}", string.Join(", ", identifiers), payload.PushName);
                return Ok(new { status = "Ignored", reason = "Chat/Sender/Receiver is not monitored" });
            }

            if (PhoneHelper.IsRealPhone(resolvedPhone) && !PhoneHelper.IsRealPhone(matchedMonitoredNumber.PhoneNumber))
            {
                matchedMonitoredNumber.PhoneNumber = resolvedPhone;
                matchedMonitoredNumber.NormalizedPhone = PhoneHelper.Normalize(resolvedPhone);
                await _context.SaveChangesAsync();
                _logger.LogInformation("[MonitoredNumber UPGRADED] Id={Id}, OldPhone={Old}, NewPhone={New}", 
                    matchedMonitoredNumber.Id, matchedMonitoredNumber.PhoneNumber, resolvedPhone);
            }

            string effectiveRealPhone = !string.IsNullOrEmpty(resolvedPhone) ? resolvedPhone : matchedMonitoredNumber.PhoneNumber;
            string finalSender = effectiveRealPhone;
            string finalReceiver = !string.IsNullOrEmpty(userPhone) ? userPhone : "";

            if (payload.IsGroup)
            {
                string memberPhone = !string.IsNullOrEmpty(resolvedPhone) ? resolvedPhone : PhoneHelper.Normalize(!string.IsNullOrEmpty(payload.SenderPn) ? payload.SenderPn : payload.Sender ?? "");
                if (isFromMe)
                {
                    finalSender = !string.IsNullOrEmpty(userPhone) ? userPhone : memberPhone;
                    finalReceiver = !string.IsNullOrWhiteSpace(matchedMonitoredNumber.ContactName) 
                        ? matchedMonitoredNumber.ContactName 
                        : matchedMonitoredNumber.PhoneNumber;
                }
                else
                {
                    finalSender = memberPhone;
                    finalReceiver = !string.IsNullOrEmpty(userPhone) ? userPhone : "";
                }
            }
            else
            {
                if (isFromMe)
                {
                    finalSender = !string.IsNullOrEmpty(userPhone) ? userPhone : "";
                    finalReceiver = effectiveRealPhone;
                }
                else
                {
                    finalSender = effectiveRealPhone;
                    finalReceiver = !string.IsNullOrEmpty(userPhone) ? userPhone : "";
                }
            }

            string defaultPartyName = matchedMonitoredNumber.ContactName ?? "";
            if (string.IsNullOrEmpty(defaultPartyName) || defaultPartyName.Contains("") || defaultPartyName.Contains("?"))
            {
                defaultPartyName = !string.IsNullOrWhiteSpace(payload.PushName) ? payload.PushName : (isFromMe ? finalReceiver : finalSender);
            }

            var analysisResult = await _analysisService.AnalyzeMessageAsync(userId, text, defaultPartyName, isFromMe);

            if (analysisResult == null || !analysisResult.Amount.HasValue || analysisResult.Amount.Value <= 0)
            {
                _logger.LogInformation("[Ignored] No financial transaction in text: {Text}", text.Length > 80 ? text[..80] : text);
                return Ok(new { status = "Ignored", reason = "No financial transaction detected" });
            }

            string category = !string.IsNullOrWhiteSpace(analysisResult.Category) ? analysisResult.Category : (isFromMe ? "تسليم" : "استلام");

            string party = !string.IsNullOrWhiteSpace(analysisResult.Party) ? analysisResult.Party : "";

            var newOp = new Operation
            {
                UserId = userId,
                MonitoredNumberId = matchedMonitoredNumber.Id,
                SenderNumber = finalSender,
                ReceiverNumber = finalReceiver,
                Category = category,
                Amount = analysisResult.Amount.Value,
                Currency = analysisResult.Currency ?? "",
                Party = party,
                Notes = analysisResult.Notes,
                Status = analysisResult.Status ?? "\u0645\u0633\u0648\u062f\u0629",
                RawMessage = !string.IsNullOrEmpty(payload.MessageId) ? $"[MSG_ID:{payload.MessageId}]\n{text}" : text,
                IsReviewed = false,
                IsOutgoing = isFromMe,
                CreatedAt = DateTime.UtcNow
            };

            _context.Operations.Add(newOp);
            await _context.SaveChangesAsync();

            _logger.LogInformation("[Operation Saved] ID: {Id}, Amount: {Amount}, Currency: {Currency}, Category: {Category}, Party: {Party}",
                newOp.Id, newOp.Amount, newOp.Currency, newOp.Category, newOp.Party);

            try { await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", newOp); } catch { }
            try 
            { 
                await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNewOperation", newOp);
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", newOp);
                await _hubContext.Clients.All.SendAsync("ReceiveNewOperation", newOp);
            } 
            catch { }

            return Ok(new { status = "Success", operationId = newOp.Id });
        }

        [HttpPost("status")]
        public async Task<IActionResult> UpdateWhatsAppStatus([FromBody] WhatsAppStatusRequest statusReq)
        {
            if (statusReq == null || statusReq.UserId <= 0)
                return BadRequest("Invalid status payload");

            var session = await _context.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == statusReq.UserId);
            if (session != null)
            {
                session.Status = statusReq.Status;
                session.LastConnected = DateTime.UtcNow;
            }
            else
            {
                session = new WhatsAppSession
                {
                    UserId = statusReq.UserId,
                    Status = statusReq.Status,
                    LastConnected = DateTime.UtcNow
                };
                _context.WhatsAppSessions.Add(session);
            }
            
            await _context.SaveChangesAsync();
            try { await _hubContext.Clients.User(statusReq.UserId.ToString()).SendAsync("WhatsAppStatusChanged", statusReq.Status); } catch { }
            try { await _hubContext.Clients.Group($"user_{statusReq.UserId}").SendAsync("WhatsAppStatusChanged", statusReq.Status); } catch { }

            return Ok();
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _configuredWebhooks = new();

        public async Task<bool> EnsureInstanceWebhookConfiguredAsync(string instanceName, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(instanceName)) return false;

            if (!force && _configuredWebhooks.TryGetValue(instanceName, out var lastSync) && DateTime.UtcNow - lastSync < TimeSpan.FromHours(1))
            {
                return true;
            }

            try
            {
                string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
                string webhookUrl = (_configuration["WhatsAppService:WebhookUrl"] ?? "https://eqfall.mostanad.ly/api/WhatsAppWebhook/receive").TrimEnd('/');

                var standardEvents = new[]
                {
                    "CONNECTION_UPDATE",
                    "MESSAGES_UPSERT",
                    "MESSAGES_UPDATE",
                    "MESSAGES_DELETE",
                    "SEND_MESSAGE"
                };

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(8);

                // Format 1 (root level)
                using var req1 = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/webhook/set/{instanceName}");
                req1.Headers.Add("apikey", apiKey);
                req1.Content = JsonContent.Create(new
                {
                    enabled = true,
                    url = webhookUrl,
                    byEvents = false,
                    base64 = false,
                    events = standardEvents
                });
                var res1 = await client.SendAsync(req1);

                // Format 2 (nested webhook)
                using var req2 = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/webhook/set/{instanceName}");
                req2.Headers.Add("apikey", apiKey);
                req2.Content = JsonContent.Create(new
                {
                    webhook = new
                    {
                        enabled = true,
                        url = webhookUrl,
                        byEvents = false,
                        base64 = false,
                        events = standardEvents
                    }
                });
                var res2 = await client.SendAsync(req2);

                bool ok = res1.IsSuccessStatusCode || res2.IsSuccessStatusCode;
                if (ok) _configuredWebhooks[instanceName] = DateTime.UtcNow;
                _logger.LogInformation("[Webhook Auto-Sync] Instance: {Instance}, Status1: {S1}, Status2: {S2}", instanceName, res1.StatusCode, res2.StatusCode);
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Webhook Auto-Sync] Failed for instance {Instance}", instanceName);
                return false;
            }
        }

        private async Task<bool> IsUserSubscriptionActiveAsync(int userId)
        {
            if (userId <= 0) return false;

            if (_userSubscriptionCache.TryGetValue(userId, out var cached) && DateTime.UtcNow < cached.expiresAt)
            {
                return cached.isActive;
            }

            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Subscriptions)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null || !user.IsActive || user.SuspensionReason != null)
                {
                    _userSubscriptionCache[userId] = (false, DateTime.UtcNow.AddSeconds(30));
                    return false;
                }

                var latestSub = user.Subscriptions
                    .OrderByDescending(s => s.ExpiresAt)
                    .FirstOrDefault();

                if (latestSub == null)
                {
                    _userSubscriptionCache[userId] = (false, DateTime.UtcNow.AddSeconds(30));
                    return false;
                }

                var settings = await _context.SubscriptionSettings.AsNoTracking().FirstOrDefaultAsync();
                int graceDays = settings?.GracePeriodDays ?? 3;
                string status = SubscriptionStateHelper.ComputeStatus(latestSub.ExpiresAt, latestSub.IsTrial, DateTime.UtcNow, graceDays);

                bool isActive = status != "Expired";
                _userSubscriptionCache[userId] = (isActive, DateTime.UtcNow.AddSeconds(30));
                return isActive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WhatsAppWebhook] Error verifying subscription for user {UserId}", userId);
                return true;
            }
        }

        private async Task AutoDisconnectExpiredInstanceAsync(string instanceName, int userId)
        {
            try
            {
                string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(8);

                try
                {
                    using var logoutReq = new HttpRequestMessage(HttpMethod.Delete, $"{nodeUrl}/instance/logout/{instanceName}");
                    logoutReq.Headers.Add("apikey", apiKey);
                    await client.SendAsync(logoutReq);
                    _logger.LogInformation("[WhatsAppWebhook] Logged out Evolution instance {Instance} for expired user {UserId}", instanceName, userId);
                }
                catch { }

                try
                {
                    using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"{nodeUrl}/instance/delete/{instanceName}");
                    delReq.Headers.Add("apikey", apiKey);
                    await client.SendAsync(delReq);
                }
                catch { }

                using var scope = HttpContext.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var session = await db.WhatsAppSessions.FirstOrDefaultAsync(s => s.UserId == userId);
                if (session != null)
                {
                    session.Status = "غير متصل";
                    session.PairingCode = null;
                    await db.SaveChangesAsync();
                }

                try { await _hubContext.Clients.User(userId.ToString()).SendAsync("WhatsAppStatusChanged", "غير متصل"); } catch { }
                try { await _hubContext.Clients.Group($"user_{userId}").SendAsync("WhatsAppStatusChanged", "غير متصل"); } catch { }

                AuditLogger.Log(userId, "WHATSAPP_AUTO_DISCONNECT", "تم فصل جلسة الواتساب تلقائياً لمحاولة معالجة رسائل باشتراك منتهي", "System");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WhatsAppWebhook] AutoDisconnect error for user {UserId}", userId);
            }
        }

        [HttpGet("debug-webhooks")]
        public IActionResult GetDebugWebhooks()
        {
            return Ok(_recentRawWebhooks.ToArray().Reverse());
        }

        [HttpGet("proxy-evolution")]
        public async Task<IActionResult> ProxyEvolution([FromQuery] string path)
        {
            string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
            string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
            using var client = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{nodeUrl}/{path.TrimStart('/')}");
            req.Headers.Add("apikey", apiKey);
            var res = await client.SendAsync(req);
            var content = await res.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [HttpPost("proxy-evolution")]
        public async Task<IActionResult> ProxyEvolutionPost([FromQuery] string path, [FromBody] JsonElement body)
        {
            string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
            string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
            using var client = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/{path.TrimStart('/')}");
            req.Headers.Add("apikey", apiKey);
            req.Content = new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json");
            var res = await client.SendAsync(req);
            var content = await res.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }

        [HttpGet("sync-all-webhooks")]
        [HttpPost("sync-all-webhooks")]
        public async Task<IActionResult> SyncAllWebhooks()
        {
            string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
            string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
            string webhookUrl = (_configuration["WhatsAppService:WebhookUrl"] ?? "https://eqfall.mostanad.ly/api/WhatsAppWebhook/receive").TrimEnd('/');

            var standardEvents = new[]
            {
                "CONNECTION_UPDATE",
                "MESSAGES_UPSERT",
                "MESSAGES_UPDATE",
                "MESSAGES_DELETE",
                "SEND_MESSAGE"
            };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            string currentWebhook = "";
            try
            {
                using var findReq = new HttpRequestMessage(HttpMethod.Get, $"{nodeUrl}/webhook/find/user_1");
                findReq.Headers.Add("apikey", apiKey);
                var findRes = await client.SendAsync(findReq);
                currentWebhook = $"Status: {findRes.StatusCode}, Body: {await findRes.Content.ReadAsStringAsync()}";
            }
            catch (Exception ex)
            {
                currentWebhook = "Find error: " + ex.Message;
            }

            string setFormat1Result = "";
            try
            {
                using var req1 = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/webhook/set/user_1");
                req1.Headers.Add("apikey", apiKey);
                req1.Content = JsonContent.Create(new
                {
                    enabled = true,
                    url = webhookUrl,
                    byEvents = false,
                    base64 = false,
                    events = standardEvents
                });
                var res1 = await client.SendAsync(req1);
                setFormat1Result = $"Status: {res1.StatusCode}, Body: {await res1.Content.ReadAsStringAsync()}";
            }
            catch (Exception ex)
            {
                setFormat1Result = "Req1 error: " + ex.Message;
            }

            string setFormat2Result = "";
            try
            {
                using var req2 = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/webhook/set/user_1");
                req2.Headers.Add("apikey", apiKey);
                req2.Content = JsonContent.Create(new
                {
                    webhook = new
                    {
                        enabled = true,
                        url = webhookUrl,
                        byEvents = false,
                        base64 = false,
                        events = standardEvents
                    }
                });
                var res2 = await client.SendAsync(req2);
                setFormat2Result = $"Status: {res2.StatusCode}, Body: {await res2.Content.ReadAsStringAsync()}";
            }
            catch (Exception ex)
            {
                setFormat2Result = "Req2 error: " + ex.Message;
            }

            var sessions = await _context.WhatsAppSessions
                .Where(s => s.Status == "متصل" || s.Status == "في انتظار الربط")
                .ToListAsync();

            var diag = new List<object>();
            foreach (var s in sessions)
            {
                string inst = $"user_{s.UserId}";
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/webhook/set/{inst}");
                    req.Headers.Add("apikey", apiKey);
                    req.Content = JsonContent.Create(new
                    {
                        webhook = new
                        {
                            enabled = true,
                            url = webhookUrl,
                            byEvents = false,
                            base64 = false,
                            events = standardEvents
                        }
                    });
                    var res = await client.SendAsync(req);
                    diag.Add(new { userId = s.UserId, inst, status = res.StatusCode.ToString() });
                }
                catch (Exception ex)
                {
                    diag.Add(new { userId = s.UserId, inst, error = ex.Message });
                }
            }

            return Ok(new
            {
                nodeUrl,
                webhookUrl,
                currentWebhook,
                setFormat1Result,
                setFormat2Result,
                instances = diag
            });
        }

        private List<(string messageId, string remoteJid)> ExtractRevocationTargets(JsonElement root, string eventName)
        {
            var targets = new List<(string messageId, string remoteJid)>();
            try
            {
                bool isDeleteEvent = eventName.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                                     eventName.Contains("revoke", StringComparison.OrdinalIgnoreCase);

                if (root.TryGetProperty("data", out var msgData))
                {
                    if (msgData.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in msgData.EnumerateArray())
                        {
                            ExtractTargetsFromElement(item, isDeleteEvent, targets);
                        }
                    }
                    else if (msgData.ValueKind == JsonValueKind.Object)
                    {
                        ExtractTargetsFromElement(msgData, isDeleteEvent, targets);
                    }
                }

                // Global search for protocolMessage anywhere in root
                string rawJson = root.GetRawText();
                if (rawJson.Contains("protocolMessage", StringComparison.OrdinalIgnoreCase))
                {
                    FindProtocolMessageRevokes(root, targets);
                }

                // Global search for messageStubType 68 (REVOKE)
                if (rawJson.Contains("messageStubType", StringComparison.OrdinalIgnoreCase))
                {
                    FindStubTypeRevokes(root, targets);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting revocation targets");
            }

            return targets.Where(t => !string.IsNullOrEmpty(t.messageId)).DistinctBy(t => t.messageId).ToList();
        }

        private void ExtractTargetsFromElement(JsonElement elem, bool isDeleteEvent, List<(string messageId, string remoteJid)> targets)
        {
            if (elem.ValueKind != JsonValueKind.Object) return;

            // 1. "keys" array: { "keys": [ { "id": "...", "remoteJid": "..." } ] }
            if (elem.TryGetProperty("keys", out var keysElem) && keysElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var k in keysElem.EnumerateArray())
                {
                    if (k.ValueKind == JsonValueKind.Object)
                    {
                        string id = k.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                        string rj = k.TryGetProperty("remoteJid", out var rjProp) ? rjProp.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(id)) targets.Add((id, rj));
                    }
                }
            }

            // 2. "messages" array: { "messages": [ ... ] }
            if (elem.TryGetProperty("messages", out var msgsElem) && msgsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in msgsElem.EnumerateArray())
                {
                    ExtractTargetsFromElement(m, isDeleteEvent, targets);
                }
            }

            // 3. "key" object: { "key": { "id": "...", "remoteJid": "..." } }
            if (elem.TryGetProperty("key", out var keyElem) && keyElem.ValueKind == JsonValueKind.Object)
            {
                string id = keyElem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                string rj = keyElem.TryGetProperty("remoteJid", out var rjProp) ? rjProp.GetString() ?? "" : "";

                bool isRevoke = isDeleteEvent;
                if (elem.TryGetProperty("update", out var upd) && upd.ValueKind == JsonValueKind.Object)
                {
                    if (upd.TryGetProperty("messageStubType", out var mst) && (mst.GetInt32() == 68 || mst.GetInt32() == 0))
                        isRevoke = true;
                    if (upd.ToString().Contains("REVOKE", StringComparison.OrdinalIgnoreCase))
                        isRevoke = true;
                    if (upd.TryGetProperty("message", out var mProp) && mProp.ValueKind == JsonValueKind.Null)
                        isRevoke = true;
                }

                if (isRevoke && !string.IsNullOrEmpty(id))
                {
                    targets.Add((id, rj));
                }
            }

            // 4. Direct "id" and "remoteJid" on delete event
            if (isDeleteEvent)
            {
                if (elem.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    string id = idProp.GetString() ?? "";
                    string rj = elem.TryGetProperty("remoteJid", out var rjProp) ? rjProp.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(id)) targets.Add((id, rj));
                }
            }
        }

        private void FindProtocolMessageRevokes(JsonElement elem, List<(string messageId, string remoteJid)> targets)
        {
            if (elem.ValueKind == JsonValueKind.Object)
            {
                if (elem.TryGetProperty("protocolMessage", out var protoMsg) && protoMsg.ValueKind == JsonValueKind.Object)
                {
                    // Check if it's an edit message (type 14 or has editedMessage)
                    bool isEdit = protoMsg.TryGetProperty("editedMessage", out _);
                    if (protoMsg.TryGetProperty("type", out var tProp) && tProp.ValueKind == JsonValueKind.Number && tProp.GetInt32() == 14)
                        isEdit = true;

                    if (!isEdit && protoMsg.TryGetProperty("key", out var protoKey) && protoKey.ValueKind == JsonValueKind.Object)
                    {
                        string id = protoKey.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                        string rj = protoKey.TryGetProperty("remoteJid", out var rjProp) ? rjProp.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(id)) targets.Add((id, rj));
                    }
                }

                foreach (var prop in elem.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        FindProtocolMessageRevokes(prop.Value, targets);
                    }
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in elem.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        FindProtocolMessageRevokes(item, targets);
                    }
                }
            }
        }

        private async Task<List<WhatsAppEditTarget>> ExtractEditTargetsAsync(JsonElement root, string instanceName, int userId)
        {
            var targets = new List<WhatsAppEditTarget>();
            try
            {
                string rawJson = root.GetRawText();

                // 1. Search protocolMessage with editedMessage or type 14 anywhere in the JSON
                if (rawJson.Contains("editedMessage", StringComparison.OrdinalIgnoreCase) || 
                    (rawJson.Contains("protocolMessage", StringComparison.OrdinalIgnoreCase) && rawJson.Contains("\"type\":14")))
                {
                    FindProtocolMessageEdits(root, targets);
                }

                // 2. Search messages.update containing message text or update object
                if (root.TryGetProperty("data", out var dataElem))
                {
                    var items = new List<JsonElement>();
                    if (dataElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in dataElem.EnumerateArray()) items.Add(item);
                    }
                    else if (dataElem.ValueKind == JsonValueKind.Object)
                    {
                        items.Add(dataElem);
                    }

                    foreach (var item in items)
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;

                        string text = "";
                        string id = "";
                        string rj = "";

                        // Extract key if present
                        if (item.TryGetProperty("key", out var kObj) && kObj.ValueKind == JsonValueKind.Object)
                        {
                            id = kObj.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            rj = kObj.TryGetProperty("remoteJid", out var rjProp) ? rjProp.GetString() ?? "" : "";
                        }

                        if (item.TryGetProperty("update", out var upd) && upd.ValueKind == JsonValueKind.Object)
                        {
                            if (string.IsNullOrEmpty(id) && upd.TryGetProperty("key", out var ukObj) && ukObj.ValueKind == JsonValueKind.Object)
                            {
                                id = ukObj.TryGetProperty("id", out var uidProp) ? uidProp.GetString() ?? "" : "";
                                rj = ukObj.TryGetProperty("remoteJid", out var urjProp) ? urjProp.GetString() ?? "" : "";
                            }

                            if (upd.TryGetProperty("message", out var msgObj) && msgObj.ValueKind == JsonValueKind.Object)
                            {
                                text = ExtractTextFromMessageElement(msgObj);
                            }

                            if (string.IsNullOrEmpty(text) && upd.TryGetProperty("text", out var tProp) && !string.IsNullOrEmpty(tProp.GetString()))
                                text = tProp.GetString() ?? "";
                            if (string.IsNullOrEmpty(text) && upd.TryGetProperty("messageBody", out var mbProp) && !string.IsNullOrEmpty(mbProp.GetString()))
                                text = mbProp.GetString() ?? "";
                            if (string.IsNullOrEmpty(text) && upd.TryGetProperty("conversation", out var convProp) && !string.IsNullOrEmpty(convProp.GetString()))
                                text = convProp.GetString() ?? "";
                        }

                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(text) && !IsJidOrIdentifier(text) && IsCleanReadableText(text))
                        {
                            targets.Add(new WhatsAppEditTarget { TargetMessageId = id, RemoteJid = rj, NewText = text });
                        }
                    }
                }

                // 3. Search secretEncryptedMessage (sent when user edits message from WhatsApp mobile)
                if (rawJson.Contains("secretEncryptedMessage", StringComparison.OrdinalIgnoreCase))
                {
                    var secretEdits = new List<SecretEncryptedEditInfo>();
                    FindSecretEncryptedMessageEdits(root, secretEdits);
                    foreach (var se in secretEdits)
                    {
                        if (targets.Any(t => t.TargetMessageId == se.TargetMessageId)) continue;

                        string decryptedText = await DecryptSecretEncryptedEditAsync(se, instanceName, userId);
                        if (!string.IsNullOrWhiteSpace(decryptedText) && !IsJidOrIdentifier(decryptedText) && IsCleanReadableText(decryptedText))
                        {
                            targets.Add(new WhatsAppEditTarget { TargetMessageId = se.TargetMessageId, RemoteJid = se.RemoteJid, NewText = decryptedText });
                        }
                        else
                        {
                            string fallbackText = await FetchMessageTextFromEvolutionApiAsync(se.TargetMessageId, instanceName);
                            if (!string.IsNullOrEmpty(fallbackText) && !IsJidOrIdentifier(fallbackText) && IsCleanReadableText(fallbackText))
                            {
                                var existingOp = await _context.Operations.FirstOrDefaultAsync(o => o.UserId == userId && o.RawMessage != null && o.RawMessage.Contains(se.TargetMessageId));
                                if (existingOp != null && !string.IsNullOrEmpty(existingOp.RawMessage) && existingOp.RawMessage.Contains(fallbackText))
                                {
                                    _logger.LogInformation("[WhatsApp Edit Fallback Ignored] Fallback text is identical to existing message text: {Text}", fallbackText);
                                }
                                else
                                {
                                    targets.Add(new WhatsAppEditTarget { TargetMessageId = se.TargetMessageId, RemoteJid = se.RemoteJid, NewText = fallbackText });
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Secret edit target: {TargetMessageId}", se.TargetMessageId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting edit targets");
            }

            return targets.Where(t => !string.IsNullOrEmpty(t.TargetMessageId) && !string.IsNullOrEmpty(t.NewText))
                          .DistinctBy(t => t.TargetMessageId).ToList();
        }

        private void FindSecretEncryptedMessageEdits(JsonElement elem, List<SecretEncryptedEditInfo> list, List<string>? parentJids = null)
        {
            var currentJids = new List<string>();
            if (parentJids != null) currentJids.AddRange(parentJids);

            if (elem.ValueKind == JsonValueKind.Object)
            {
                if (elem.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.Object)
                {
                    if (k.TryGetProperty("remoteJid", out var rj) && !string.IsNullOrEmpty(rj.GetString())) currentJids.Add(rj.GetString()!);
                    if (k.TryGetProperty("participant", out var p) && !string.IsNullOrEmpty(p.GetString())) currentJids.Add(p.GetString()!);
                    if (k.TryGetProperty("remoteJidAlt", out var rja) && !string.IsNullOrEmpty(rja.GetString())) currentJids.Add(rja.GetString()!);
                }

                if (elem.TryGetProperty("secretEncryptedMessage", out var sem) && sem.ValueKind == JsonValueKind.Object)
                {
                    string encPayload = sem.TryGetProperty("encPayload", out var ep) ? ep.GetString() ?? "" : "";
                    string encIv = sem.TryGetProperty("encIv", out var ei) ? ei.GetString() ?? "" : "";
                    string targetId = "";
                    string targetRj = "";
                    bool fromMe = false;
                    string participant = "";

                    if (sem.TryGetProperty("targetMessageKey", out var tmk) && tmk.ValueKind == JsonValueKind.Object)
                    {
                        if (tmk.TryGetProperty("id", out var tid)) targetId = tid.GetString() ?? "";
                        if (tmk.TryGetProperty("remoteJid", out var trj)) { targetRj = trj.GetString() ?? ""; currentJids.Add(targetRj); }
                        if (tmk.TryGetProperty("participant", out var tp)) { participant = tp.GetString() ?? ""; currentJids.Add(participant); }
                        if (tmk.TryGetProperty("fromMe", out var tfm)) fromMe = tfm.GetBoolean();
                    }

                    if (!string.IsNullOrEmpty(targetId) && !string.IsNullOrEmpty(encPayload) && !string.IsNullOrEmpty(encIv))
                    {
                        list.Add(new SecretEncryptedEditInfo
                        {
                            TargetMessageId = targetId,
                            RemoteJid = targetRj,
                            EncPayload = encPayload,
                            EncIv = encIv,
                            FromMe = fromMe,
                            Participant = participant,
                            CandidateJids = currentJids.Distinct().ToList()
                        });
                    }
                }

                foreach (var prop in elem.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        FindSecretEncryptedMessageEdits(prop.Value, list, currentJids);
                    }
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in elem.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        FindSecretEncryptedMessageEdits(item, list, currentJids);
                    }
                }
            }
        }

        private async Task<string> DecryptSecretEncryptedEditAsync(SecretEncryptedEditInfo editInfo, string instanceName, int userId)
        {
            try
            {
                string? secretB64 = null;
                if (_messageSecretCache.TryGetValue(editInfo.TargetMessageId, out var cached))
                {
                    secretB64 = cached;
                }

                var extraJids = new List<string>(editInfo.CandidateJids);

                if (string.IsNullOrEmpty(secretB64))
                {
                    var (fetchedSecret, fetchedJids) = await FetchOriginalMessageSecretAndJidsAsync(editInfo.TargetMessageId, instanceName);
                    if (!string.IsNullOrEmpty(fetchedSecret))
                    {
                        secretB64 = fetchedSecret;
                        _messageSecretCache[editInfo.TargetMessageId] = fetchedSecret;
                    }
                    extraJids.AddRange(fetchedJids);
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null && !string.IsNullOrEmpty(user.Phone))
                {
                    string cleanPhone = PhoneHelper.Normalize(user.Phone);
                    extraJids.Add($"{cleanPhone}@s.whatsapp.net");
                }

                if (string.IsNullOrEmpty(secretB64))
                {
                    _logger.LogWarning("[WhatsApp Decrypt Edit] messageSecret not found for target {MsgId}", editInfo.TargetMessageId);
                    return "";
                }

                byte[] secret = Convert.FromBase64String(secretB64);
                byte[] iv = Convert.FromBase64String(editInfo.EncIv);
                byte[] payload = Convert.FromBase64String(editInfo.EncPayload);

                if (payload.Length <= 16) return "";

                byte[] ciphertext = new byte[payload.Length - 16];
                byte[] tag = new byte[16];
                Buffer.BlockCopy(payload, 0, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(payload, payload.Length - 16, tag, 0, 16);

                var cleanCandidates = new List<string>();
                foreach (var j in extraJids)
                {
                    if (string.IsNullOrWhiteSpace(j)) continue;
                    string trimmed = j.Trim();
                    if (!cleanCandidates.Contains(trimmed)) cleanCandidates.Add(trimmed);
                    string nonAd = JidToNonAD(trimmed);
                    if (!cleanCandidates.Contains(nonAd)) cleanCandidates.Add(nonAd);
                }
                if (!cleanCandidates.Contains("")) cleanCandidates.Add("");

                string[] labels = new[] { "Message Edit", "WhatsApp Message Edit" };
                byte[][] salts = new byte[][] { new byte[32], Array.Empty<byte>() };

                foreach (var origJid in cleanCandidates)
                {
                    foreach (var modJid in cleanCandidates)
                    {
                        foreach (var label in labels)
                        {
                            byte[] infoBytes = BuildHkdfInfo(editInfo.TargetMessageId, origJid, modJid, label);
                            foreach (var salt in salts)
                            {
                                byte[] key;
                                try
                                {
                                    key = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, salt, infoBytes);
                                }
                                catch { continue; }

                                try
                                {
                                    byte[] plain = new byte[ciphertext.Length];
                                    using var aes = new AesGcm(key, tag.Length);
                                    aes.Decrypt(iv, ciphertext, tag, plain);

                                    string extracted = ExtractTextFromDecryptedMessage(plain);
                                    if (!string.IsNullOrWhiteSpace(extracted))
                                    {
                                        _logger.LogInformation("[WhatsApp Decrypt Edit SUCCESS] TargetId={Id}, ExtractedText='{Text}'", editInfo.TargetMessageId, extracted);
                                        return extracted;
                                    }
                                }
                                catch
                                {
                                    // Auth tag mismatch, proceed to next candidate
                                }
                            }
                        }
                    }
                }

                _logger.LogWarning("[WhatsApp Decrypt Edit FAILED] Could not decrypt target {MsgId}", editInfo.TargetMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WhatsApp Decrypt Edit] Exception decrypting target {MsgId}", editInfo.TargetMessageId);
            }

            return "";
        }

        private static byte[] BuildHkdfInfo(string origMsgId, string origJid, string modJid, string label)
        {
            using var ms = new MemoryStream();
            byte[] bId = Encoding.UTF8.GetBytes(origMsgId);
            byte[] bOrig = Encoding.UTF8.GetBytes(JidToNonAD(origJid));
            byte[] bMod = Encoding.UTF8.GetBytes(JidToNonAD(modJid));
            byte[] bLabel = Encoding.UTF8.GetBytes(label);

            ms.Write(bId, 0, bId.Length);
            ms.Write(bOrig, 0, bOrig.Length);
            ms.Write(bMod, 0, bMod.Length);
            ms.Write(bLabel, 0, bLabel.Length);
            return ms.ToArray();
        }

        private static string JidToNonAD(string jid)
        {
            if (string.IsNullOrEmpty(jid) || !jid.Contains("@")) return jid ?? "";
            var parts = jid.Split('@', 2);
            string user = parts[0].Split(':')[0];
            return $"{user}@{parts[1]}";
        }

        private static string ExtractTextFromDecryptedMessage(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            int idx = 0;

            while (idx < data.Length)
            {
                byte tagByte = data[idx++];
                int wireType = tagByte & 0x07;
                int fieldNum = tagByte >> 3;

                if (wireType == 0) // varint
                {
                    while (idx < data.Length && (data[idx++] & 0x80) != 0) { }
                }
                else if (wireType == 1) // 64-bit
                {
                    idx += 8;
                }
                else if (wireType == 2) // length-delimited (string / embedded message)
                {
                    int len = 0;
                    int shift = 0;
                    while (idx < data.Length)
                    {
                        byte b = data[idx++];
                        len |= (b & 0x7F) << shift;
                        if ((b & 0x80) == 0) break;
                        shift += 7;
                    }

                    if (idx + len <= data.Length && len > 0)
                    {
                        byte[] fieldBytes = new byte[len];
                        Buffer.BlockCopy(data, idx, fieldBytes, 0, len);
                        idx += len;

                        // Recursively search nested protobuf messages first (e.g. field 14: editedMessage, field 6: extendedTextMessage)
                        string nested = ExtractTextFromDecryptedMessage(fieldBytes);
                        if (!string.IsNullOrEmpty(nested) && !IsJidOrIdentifier(nested) && IsCleanReadableText(nested))
                        {
                            return nested;
                        }

                        // Field 1 is "conversation" (in Message) or "text" (in ExtendedTextMessage)
                        if (fieldNum == 1)
                        {
                            try
                            {
                                string candidate = Encoding.UTF8.GetString(fieldBytes).Trim();
                                if (!IsJidOrIdentifier(candidate) && IsCleanReadableText(candidate))
                                {
                                    return candidate;
                                }
                            }
                            catch { }
                        }
                    }
                }
                else if (wireType == 5) // 32-bit
                {
                    idx += 4;
                }
                else
                {
                    break;
                }
            }

            return "";
        }

        private static bool IsCleanReadableText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 1) return false;

            // Reject if contains unprintable control characters
            if (text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')) return false;

            // Reject typical binary mojibake noise patterns (e.g. repeated symbols, dots with letters, binary noise)
            if (Regex.IsMatch(text, @"(?:b{2,}\.+b|ط[ؤه]\.+|[\u0600-\u06FF]{1,2}[_'][\u0600-\u06FF]{1,2}[_'])")) return false;

            // Must be at least 95% letters, digits, spaces, and normal punctuation
            int validCount = 0;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c))
                {
                    validCount++;
                }
            }

            return ((double)validCount / text.Length) >= 0.95;
        }

        private async Task<(string? secret, List<string> jids)> FetchOriginalMessageSecretAndJidsAsync(string messageId, string instanceName)
        {
            var jids = new List<string>();
            string? secret = null;

            try
            {
                string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
                string inst = !string.IsNullOrEmpty(instanceName) ? instanceName : "user_1";

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/chat/findMessages/{inst}");
                req.Headers.Add("apikey", apiKey);
                req.Content = JsonContent.Create(new
                {
                    where = new
                    {
                        key = new
                        {
                            id = messageId
                        }
                    }
                });

                var res = await client.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var content = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    FindMessageSecretAndJidsRecursive(doc.RootElement, ref secret, jids);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch original message secret from Evolution API for {MsgId}", messageId);
            }

            return (secret, jids.Distinct().ToList());
        }

        private void FindMessageSecretAndJidsRecursive(JsonElement elem, ref string? secret, List<string> jids)
        {
            if (elem.ValueKind == JsonValueKind.Object)
            {
                if (elem.TryGetProperty("messageSecret", out var msProp) && msProp.ValueKind == JsonValueKind.String)
                {
                    secret = msProp.GetString();
                }

                if (elem.TryGetProperty("remoteJid", out var rj) && rj.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(rj.GetString()))
                    jids.Add(rj.GetString()!);
                if (elem.TryGetProperty("participant", out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(p.GetString()))
                    jids.Add(p.GetString()!);
                if (elem.TryGetProperty("remoteJidAlt", out var rja) && rja.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(rja.GetString()))
                    jids.Add(rja.GetString()!);
                if (elem.TryGetProperty("senderPn", out var spn) && spn.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(spn.GetString()))
                    jids.Add(spn.GetString()!);
                if (elem.TryGetProperty("senderLid", out var slid) && slid.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(slid.GetString()))
                    jids.Add(slid.GetString()!);

                foreach (var prop in elem.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        FindMessageSecretAndJidsRecursive(prop.Value, ref secret, jids);
                    }
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in elem.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        FindMessageSecretAndJidsRecursive(item, ref secret, jids);
                    }
                }
            }
        }

        private async Task<string> FetchMessageTextFromEvolutionApiAsync(string messageId, string instanceName)
        {
            try
            {
                string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
                string inst = !string.IsNullOrEmpty(instanceName) ? instanceName : "user_1";

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/chat/findMessages/{inst}");
                req.Headers.Add("apikey", apiKey);
                req.Content = JsonContent.Create(new
                {
                    where = new
                    {
                        key = new
                        {
                            id = messageId
                        }
                    }
                });

                var res = await client.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    var content = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var msg = root[0];
                        if (msg.TryGetProperty("message", out var mObj))
                            return ExtractTextFromMessageElement(mObj);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("messages", out var mProp))
                        {
                            if (mProp.ValueKind == JsonValueKind.Array && mProp.GetArrayLength() > 0)
                            {
                                if (mProp[0].TryGetProperty("message", out var mObj))
                                    return ExtractTextFromMessageElement(mObj);
                            }
                            else if (mProp.ValueKind == JsonValueKind.Object && mProp.TryGetProperty("records", out var recArr) && recArr.ValueKind == JsonValueKind.Array && recArr.GetArrayLength() > 0)
                            {
                                if (recArr[0].TryGetProperty("message", out var mObj))
                                    return ExtractTextFromMessageElement(mObj);
                            }
                        }
                        if (root.TryGetProperty("message", out var mObj2))
                            return ExtractTextFromMessageElement(mObj2);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch message text from Evolution API for {MsgId}", messageId);
            }
            return "";
        }

        private void FindProtocolMessageEdits(JsonElement elem, List<WhatsAppEditTarget> targets)
        {
            if (elem.ValueKind == JsonValueKind.Object)
            {
                if (elem.TryGetProperty("protocolMessage", out var protoMsg) && protoMsg.ValueKind == JsonValueKind.Object)
                {
                    bool isEdit = protoMsg.TryGetProperty("editedMessage", out var edMsg) && edMsg.ValueKind == JsonValueKind.Object;
                    if (!isEdit && protoMsg.TryGetProperty("type", out var tProp) && tProp.ValueKind == JsonValueKind.Number && tProp.GetInt32() == 14)
                    {
                        isEdit = true;
                    }

                    if (isEdit)
                    {
                        string id = "";
                        string rj = "";

                        if (protoMsg.TryGetProperty("key", out var protoKey) && protoKey.ValueKind == JsonValueKind.Object)
                        {
                            id = protoKey.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                            rj = protoKey.TryGetProperty("remoteJid", out var rjProp) ? rjProp.GetString() ?? "" : "";
                        }

                        if (string.IsNullOrEmpty(id) && protoMsg.TryGetProperty("targetMessageKey", out var tmk) && tmk.ValueKind == JsonValueKind.Object)
                        {
                            id = tmk.TryGetProperty("id", out var tidProp) ? tidProp.GetString() ?? "" : "";
                            rj = tmk.TryGetProperty("remoteJid", out var trjProp) ? trjProp.GetString() ?? "" : "";
                        }

                        string text = "";
                        if (protoMsg.TryGetProperty("editedMessage", out var emObj) && emObj.ValueKind == JsonValueKind.Object)
                        {
                            text = ExtractTextFromMessageElement(emObj);
                        }

                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(text))
                        {
                            targets.Add(new WhatsAppEditTarget { TargetMessageId = id, RemoteJid = rj, NewText = text });
                        }
                    }
                }

                foreach (var prop in elem.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        FindProtocolMessageEdits(prop.Value, targets);
                    }
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in elem.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        FindProtocolMessageEdits(item, targets);
                    }
                }
            }
        }

        private static bool IsJidOrIdentifier(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            s = s.Trim();
            return s.Contains("@lid", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("@g.us", StringComparison.OrdinalIgnoreCase) ||
                   s.Contains("@broadcast", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("lid@", StringComparison.OrdinalIgnoreCase);
        }

        private string ExtractTextFromMessageElement(JsonElement msgObj)
        {
            if (msgObj.ValueKind != JsonValueKind.Object) return "";

            // Unwrap editedMessage wrapper
            if (msgObj.TryGetProperty("editedMessage", out var em) && em.ValueKind == JsonValueKind.Object)
            {
                var s = ExtractTextFromMessageElement(em);
                if (!string.IsNullOrEmpty(s)) return s;
            }

            // Unwrap generic nested message wrapper
            if (msgObj.TryGetProperty("message", out var nestedMsg) && nestedMsg.ValueKind == JsonValueKind.Object)
            {
                var s = ExtractTextFromMessageElement(nestedMsg);
                if (!string.IsNullOrEmpty(s)) return s;
            }

            // Unwrap ephemeral or viewOnce wrappers
            if (msgObj.TryGetProperty("ephemeralMessage", out var eph) && eph.ValueKind == JsonValueKind.Object && eph.TryGetProperty("message", out var ephMsg))
                return ExtractTextFromMessageElement(ephMsg);
            if (msgObj.TryGetProperty("viewOnceMessage", out var vom) && vom.ValueKind == JsonValueKind.Object && vom.TryGetProperty("message", out var vomMsg))
                return ExtractTextFromMessageElement(vomMsg);
            if (msgObj.TryGetProperty("viewOnceMessageV2", out var vom2) && vom2.ValueKind == JsonValueKind.Object && vom2.TryGetProperty("message", out var vomMsg2))
                return ExtractTextFromMessageElement(vomMsg2);

            if (msgObj.TryGetProperty("conversation", out var conv) && !string.IsNullOrEmpty(conv.GetString()))
            {
                var s = conv.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }
            if (msgObj.TryGetProperty("extendedTextMessage", out var ext) && ext.ValueKind == JsonValueKind.Object && ext.TryGetProperty("text", out var extText))
            {
                var s = extText.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }
            if (msgObj.TryGetProperty("imageMessage", out var img) && img.ValueKind == JsonValueKind.Object && img.TryGetProperty("caption", out var imgCap))
            {
                var s = imgCap.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }
            if (msgObj.TryGetProperty("documentMessage", out var doc) && doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("caption", out var docCap))
            {
                var s = docCap.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }
            if (msgObj.TryGetProperty("videoMessage", out var vid) && vid.ValueKind == JsonValueKind.Object && vid.TryGetProperty("caption", out var vidCap))
            {
                var s = vidCap.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }
            if (msgObj.TryGetProperty("text", out var tProp) && !string.IsNullOrEmpty(tProp.GetString()))
            {
                var s = tProp.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }
            if (msgObj.TryGetProperty("body", out var bProp) && !string.IsNullOrEmpty(bProp.GetString()))
            {
                var s = bProp.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }
            if (msgObj.TryGetProperty("messageBody", out var mbProp) && !string.IsNullOrEmpty(mbProp.GetString()))
            {
                var s = mbProp.GetString()!;
                if (!IsJidOrIdentifier(s)) return s;
            }

            // If it contains protocolMessage with editedMessage
            if (msgObj.TryGetProperty("protocolMessage", out var pm) && pm.ValueKind == JsonValueKind.Object && pm.TryGetProperty("editedMessage", out var edm))
                return ExtractTextFromMessageElement(edm);

            return "";
        }

        private async Task HandleMessageEditAsync(int userId, string targetMsgId, string? remoteJid, string newText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newText)) return;

                string cleanText = newText.Trim();
                if (IsJidOrIdentifier(cleanText) || !IsCleanReadableText(cleanText))
                {
                    _logger.LogInformation("[WhatsApp Edit Ignored] newText '{Text}' is a JID, identifier, or junk.", cleanText);
                    return;
                }

                cleanText = cleanText.Replace('٠', '0').Replace('١', '1').Replace('٢', '2')
                                     .Replace('٣', '3').Replace('٤', '4').Replace('٥', '5')
                                     .Replace('٦', '6').Replace('٧', '7').Replace('٨', '8').Replace('٩', '9');
                cleanText = Regex.Replace(cleanText, @"([\p{L}])(\d+)", "$1 $2");
                cleanText = Regex.Replace(cleanText, @"(\d+)([\p{L}])", "$1 $2");

                Operation? operation = null;

                // 1. Search by message ID in RawMessage
                if (!string.IsNullOrEmpty(targetMsgId))
                {
                    operation = await _context.Operations
                        .FirstOrDefaultAsync(o => o.UserId == userId && o.RawMessage != null && o.RawMessage.Contains(targetMsgId));
                }

                // 2. Search by remote party in last 2 hours
                if (operation == null && !string.IsNullOrEmpty(remoteJid))
                {
                    string cleanJid = PhoneHelper.Normalize(remoteJid);
                    var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
                    operation = await _context.Operations
                        .Where(o => o.UserId == userId && o.CreatedAt >= twoHoursAgo && o.Status != "محذوف")
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefaultAsync(o => (!string.IsNullOrEmpty(o.SenderNumber) && o.SenderNumber.Contains(cleanJid)) 
                                               || (!string.IsNullOrEmpty(o.ReceiverNumber) && o.ReceiverNumber.Contains(cleanJid)));
                }

                if (operation == null)
                {
                    _logger.LogInformation("[WhatsApp Edit] No existing operation found for targetMsgId: {TargetMsgId}", targetMsgId);
                    return;
                }

                // Re-analyze with new text
                string party = operation.Party ?? "";
                bool isFromMe = operation.IsOutgoing;
                var analysisResult = await _analysisService.AnalyzeMessageAsync(userId, cleanText, party, isFromMe);

                if (analysisResult == null || !analysisResult.Amount.HasValue || analysisResult.Amount.Value <= 0 || analysisResult.Status == "غير مالي")
                {
                    _logger.LogInformation("[WhatsApp Edit] No valid financial transaction in edited text for op #{Id}: {Text}", operation.Id, cleanText);
                    if (!string.IsNullOrWhiteSpace(cleanText) && cleanText.Length >= 4 && !IsJidOrIdentifier(cleanText) && IsCleanReadableText(cleanText))
                    {
                        string warningBadge = "⚠️ تم رصد تعديل لهذه الرسالة في الواتساب إلى نص غير مالي أو غير مفهوم";
                        if (string.IsNullOrEmpty(operation.Notes))
                            operation.Notes = warningBadge;
                        else if (!operation.Notes.Contains("تعديل"))
                            operation.Notes = $"{warningBadge} | {operation.Notes}";
                        
                        if (string.IsNullOrEmpty(operation.RawMessage) || IsJidOrIdentifier(operation.RawMessage))
                        {
                            operation.RawMessage = !string.IsNullOrEmpty(targetMsgId) ? $"[MSG_ID:{targetMsgId}]\n{cleanText}" : cleanText;
                        }
                        operation.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    return;
                }

                decimal oldAmount = operation.Amount ?? 0;
                decimal newAmount = analysisResult.Amount.Value;
                string currency = !string.IsNullOrWhiteSpace(analysisResult.Currency) ? analysisResult.Currency : (operation.Currency ?? "LYD");
                // Update category if the edited message specified a category, otherwise preserve existing
                string category = !string.IsNullOrWhiteSpace(analysisResult.Category)
                    ? analysisResult.Category
                    : (operation.Category ?? (isFromMe ? "تسليم" : "استلام"));
                // Only change party if extracted from text and not a raw phone number
                string newParty = (!string.IsNullOrWhiteSpace(analysisResult.Party) 
                                  && analysisResult.Party != operation.SenderNumber 
                                  && analysisResult.Party != operation.ReceiverNumber) 
                    ? analysisResult.Party 
                    : party;

                string oldCategory = operation.Category ?? "";
                bool categoryChanged = !string.IsNullOrEmpty(category) && !string.Equals(oldCategory, category, StringComparison.OrdinalIgnoreCase);
                bool amountChanged = Math.Abs(newAmount - oldAmount) > 0.001m;

                string editBadge;
                if (amountChanged && categoryChanged)
                {
                    operation.Amount = newAmount;
                    editBadge = $"✏️ تم تعديل العملية في الواتساب: المبلغ من {oldAmount:0.##} إلى {newAmount:0.##} {currency}، والتصنيف من {oldCategory} إلى {category}";
                }
                else if (amountChanged)
                {
                    operation.Amount = newAmount;
                    editBadge = $"✏️ تم تعديل المبلغ في الواتساب من {oldAmount:0.##} إلى {newAmount:0.##} {currency}";
                }
                else if (categoryChanged)
                {
                    editBadge = $"✏️ تم تعديل تصنيف العملية في الواتساب من {oldCategory} إلى {category}";
                }
                else
                {
                    editBadge = $"✏️ تم تعديل نص الرسالة في الواتساب";
                }

                operation.Category = category;
                operation.Currency = currency;
                operation.Party = newParty;
                operation.RawMessage = !string.IsNullOrEmpty(targetMsgId) ? $"[MSG_ID:{targetMsgId}]\n{cleanText}" : cleanText;
                operation.UpdatedAt = DateTime.UtcNow;

                if (string.IsNullOrEmpty(operation.Notes))
                {
                    operation.Notes = editBadge;
                }
                else if (!operation.Notes.Contains("تعديل"))
                {
                    operation.Notes = $"{editBadge} | {operation.Notes}";
                }
                else
                {
                    operation.Notes = $"{editBadge} | " + Regex.Replace(operation.Notes, @"[✏️⚠️] تم [^|]+(\| )?", "").Trim();
                }

                await _context.SaveChangesAsync();

                string auditDetails = $"تم تعديل العملية #{operation.Id} في الواتساب";
                if (amountChanged) auditDetails += $" [المبلغ: {oldAmount:0.##} -> {newAmount:0.##} {currency}]";
                if (categoryChanged) auditDetails += $" [التصنيف: {oldCategory} -> {category}]";
                auditDetails += $" | النص: {cleanText}";

                try
                {
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNewOperation", operation);
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", operation);
                    await _hubContext.Clients.All.SendAsync("ReceiveNewOperation", operation);
                }
                catch { }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message edit for targetMsgId: {TargetMsgId}", targetMsgId);
            }
        }

        private void FindStubTypeRevokes(JsonElement elem, List<(string messageId, string remoteJid)> targets)
        {
            if (elem.ValueKind == JsonValueKind.Object)
            {
                if (elem.TryGetProperty("messageStubType", out var mst) && (mst.GetInt32() == 68 || mst.GetInt32() == 0))
                {
                    if (elem.TryGetProperty("key", out var kObj) && kObj.ValueKind == JsonValueKind.Object)
                    {
                        string id = kObj.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                        string rj = kObj.TryGetProperty("remoteJid", out var rjProp) ? rjProp.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(id)) targets.Add((id, rj));
                    }
                }

                foreach (var prop in elem.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        FindStubTypeRevokes(prop.Value, targets);
                    }
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in elem.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        FindStubTypeRevokes(item, targets);
                    }
                }
            }
        }

        private async Task HandleMessageRevocationAsync(int userId, string messageId, string? remoteJid)
        {
            try
            {
                Operation? operation = null;

                // 1. Search by message ID in RawMessage
                if (!string.IsNullOrEmpty(messageId))
                {
                    operation = await _context.Operations
                        .FirstOrDefaultAsync(o => o.UserId == userId && o.RawMessage != null && o.RawMessage.Contains(messageId));
                }

                // 2. Search by remote party phone in last 2 hours
                if (operation == null && !string.IsNullOrEmpty(remoteJid))
                {
                    string cleanJid = PhoneHelper.Normalize(remoteJid);
                    var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
                    operation = await _context.Operations
                        .Where(o => o.UserId == userId && o.CreatedAt >= twoHoursAgo && o.Status != "محذوف")
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefaultAsync(o => (!string.IsNullOrEmpty(o.SenderNumber) && o.SenderNumber.Contains(cleanJid)) 
                                               || (!string.IsNullOrEmpty(o.ReceiverNumber) && o.ReceiverNumber.Contains(cleanJid)));
                }

                // 3. Fallback: if still not found, match the most recent un-deleted operation in the last 15 minutes
                if (operation == null)
                {
                    var fifteenMinutesAgo = DateTime.UtcNow.AddMinutes(-15);
                    operation = await _context.Operations
                        .Where(o => o.UserId == userId && o.CreatedAt >= fifteenMinutesAgo && o.Status != "محذوف")
                        .OrderByDescending(o => o.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                if (operation != null)
                {
                    operation.Status = "محذوف";
                    operation.UpdatedAt = DateTime.UtcNow;
                    if (string.IsNullOrEmpty(operation.Notes))
                    {
                        operation.Notes = "⚠️ تم حذف الرسالة الأصلية من الواتساب (Delete for everyone)";
                    }
                    else if (!operation.Notes.Contains("تم حذف الرسالة"))
                    {
                        operation.Notes += " | ⚠️ تم حذف الرسالة الأصلية من الواتساب";
                    }

                    await _context.SaveChangesAsync();

                    try
                    {
                        await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNewOperation", operation);
                        await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", operation);
                        await _hubContext.Clients.All.SendAsync("ReceiveNewOperation", operation);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message revocation for MsgId: {MsgId}", messageId);
            }
        }
    }

    public class WhatsAppEditTarget
    {
        public string TargetMessageId { get; set; } = "";
        public string RemoteJid { get; set; } = "";
        public string NewText { get; set; } = "";
    }
}
