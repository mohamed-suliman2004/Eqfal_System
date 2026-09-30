using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Eqfal.API.Data;
using Eqfal.API.Helpers;
using Eqfal.API.Models;
using Eqfal.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IKeywordSeederService _seederService;
        private readonly ILogger<AuthController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(
            AppDbContext context,
            IConfiguration configuration,
            IKeywordSeederService seederService,
            ILogger<AuthController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _seederService = seederService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class RegisterRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? UsernameEmail { get; set; }
            public string Password { get; set; } = string.Empty;
        }

        public class UpdateFcmTokenRequest
        {
            public string Token { get; set; } = string.Empty;
        }

        public class ForgotPasswordRequest
        {
            public string Phone { get; set; } = string.Empty;
        }

        public class VerifyOtpRequest
        {
            public string Phone { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
        }

        public class ResetPasswordRequest
        {
            public string Phone { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "\u064a\u0631\u062c\u0649\u0020\u0625\u062f\u062e\u0627\u0644\u0020\u0627\u0633\u0645\u0020\u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u0020\u0648\u0643\u0644\u0645\u0629\u0020\u0627\u0644\u0645\u0631\u0648\u0631" });
            }

            var inputUsername = request.Username.Trim();
            var cleanPhone = PhoneHelper.Normalize(inputUsername);

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                (u.UsernameEmail == inputUsername 
                 || (inputUsername.Equals("admin", StringComparison.OrdinalIgnoreCase) && u.UsernameEmail == "admin@eqfal.com")
                 || (!string.IsNullOrEmpty(cleanPhone) && u.Phone == cleanPhone)
                 || u.Phone == inputUsername));

            bool isPasswordValid = false;
            if (user != null)
            {
                if (user.PasswordHash == request.Password)
                {
                    isPasswordValid = true;
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    await _context.SaveChangesAsync();
                }
                else if (user.PasswordHash.StartsWith("$2"))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                }
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            if (user == null || !isPasswordValid)
            {
                AuditLogger.Log(user?.Id, "LOGIN_FAILED", $"محاولة تسجيل دخول فاشلة لاسم المستخدم: {inputUsername}", clientIp);
                return Unauthorized(new { message = "بيانات الدخول غير صحيحة" });
            }

            if (!user.IsActive)
            {
                AuditLogger.Log(user.Id, "LOGIN_BLOCKED", $"محاولة دخول لحساب معطل: {user.FullName}", clientIp);
                return Unauthorized(new { message = "تم تعطيل هذا الحساب، يرجى التواصل مع الإدارة" });
            }

            AuditLogger.Log(user.Id, "LOGIN_SUCCESS", $"تسجيل دخول ناجح للمستخدم: {user.FullName} ({user.Phone})", clientIp);
            var token = GenerateJwtToken(user);

            return Ok(new
            {
                token,
                user = new
                {
                    id = user.Id,
                    name = user.FullName,
                    fullName = user.FullName,
                    phone = user.Phone ?? "",
                    email = user.UsernameEmail,
                    usernameEmail = user.UsernameEmail
                }
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "يرجى ملء جميع الحقول المطلوبة (الاسم الكامل، رقم الهاتف، كلمة المرور)" });
            }

            if (request.Password.Length < 8)
            {
                return BadRequest(new { message = "كلمة المرور يجب أن لا تقل عن 8 خانات" });
            }

            var cleanPhone = PhoneHelper.Normalize(request.Phone);
            if (string.IsNullOrEmpty(cleanPhone) || !PhoneHelper.IsRealPhone(cleanPhone))
            {
                return BadRequest(new { message = "يرجى إدخال رقم هاتف صحيح مع رمز الدولة" });
            }

            string finalUsernameEmail = !string.IsNullOrWhiteSpace(request.Email) 
                ? request.Email.Trim().ToLower() 
                : (!string.IsNullOrWhiteSpace(request.UsernameEmail) ? request.UsernameEmail.Trim().ToLower() : $"{cleanPhone}@eqfal.app");

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Phone == cleanPhone || u.UsernameEmail == finalUsernameEmail);
            if (existingUser != null)
            {
                if (existingUser.Phone == cleanPhone)
                {
                    return BadRequest(new { message = "رقم الهاتف مسجل مسبقاً، يرجى تسجيل الدخول أو استخدام رقم آخر" });
                }
                return BadRequest(new { message = "البريد الإلكتروني مسجل مسبقاً، يرجى تسجيل الدخول أو استخدام بريد آخر" });
            }

            var newUser = new User
            {
                FullName = request.FullName.Trim(),
                Phone = cleanPhone,
                UsernameEmail = finalUsernameEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Seed initial keywords
            try
            {
                await _seederService.SeedDefaultKeywordsAsync(newUser.Id);
            }
            catch { }

            var clientIpReg = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            AuditLogger.Log(newUser.Id, "REGISTER", $"تسجيل حساب مستخدم جديد: {newUser.FullName} ({newUser.Phone})", clientIpReg);

            var token = GenerateJwtToken(newUser);

            return Ok(new
            {
                token,
                user = new
                {
                    id = newUser.Id,
                    name = newUser.FullName,
                    fullName = newUser.FullName,
                    phone = newUser.Phone,
                    email = newUser.UsernameEmail,
                    usernameEmail = newUser.UsernameEmail
                }
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Phone))
            {
                return BadRequest(new { message = "يرجى إدخال رقم الهاتف" });
            }

            string cleanPhone = PhoneHelper.Normalize(request.Phone);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == cleanPhone || u.Phone == request.Phone.Trim());
            if (user == null)
            {
                return NotFound(new { message = "رقم الهاتف غير مسجل لدينا في النظام" });
            }

            string otp = Random.Shared.Next(100000, 999999).ToString();
            user.ResetOtp = otp;
            user.ResetOtpExpiry = DateTime.UtcNow.AddMinutes(15);
            await _context.SaveChangesAsync();

            string msg = $"رمز التحقق الخاص بك لإعادة تعيين كلمة المرور في تطبيق إقفال هو:\n*{otp}*\nصالح لمدة 15 دقيقة.";
            await SendWhatsAppMessageAsync(cleanPhone, msg);

            var clientIpF = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            AuditLogger.Log(user.Id, "FORGOT_PASSWORD_REQUEST", $"طلب استعادة كلمة المرور وإرسال OTP عبر واتساب للرقم: {cleanPhone}", clientIpF);

            return Ok(new { message = "تم إرسال رمز التحقق بنجاح إلى رقم هاتفك عبر واتساب" });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Otp))
            {
                return BadRequest(new { message = "يرجى إدخال رقم الهاتف ورمز التحقق" });
            }

            string cleanPhone = PhoneHelper.Normalize(request.Phone);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == cleanPhone || u.Phone == request.Phone.Trim());
            if (user == null)
            {
                return NotFound(new { message = "رقم الهاتف غير مسجل لدينا" });
            }

            if (string.IsNullOrEmpty(user.ResetOtp) || user.ResetOtp != request.Otp.Trim())
            {
                return BadRequest(new { message = "رمز التحقق غير صحيح، يرجى التأكد من الرمز المدخل" });
            }

            if (user.ResetOtpExpiry.HasValue && user.ResetOtpExpiry.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد" });
            }

            var clientIpV = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            AuditLogger.Log(user.Id, "VERIFY_OTP_SUCCESS", $"تم التحقق من رمز OTP بنجاح للرقم: {cleanPhone}", clientIpV);

            return Ok(new { message = "تم التحقق من الرمز بنجاح" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Otp) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "يرجى ملء جميع الحقول المطلوبة" });
            }

            if (request.NewPassword.Length < 8)
            {
                return BadRequest(new { message = "كلمة المرور الجديدة يجب أن لا تقل عن 8 خانات" });
            }

            string cleanPhone = PhoneHelper.Normalize(request.Phone);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == cleanPhone || u.Phone == request.Phone.Trim());
            if (user == null)
            {
                return NotFound(new { message = "رقم الهاتف غير مسجل لدينا" });
            }

            if (string.IsNullOrEmpty(user.ResetOtp) || user.ResetOtp != request.Otp.Trim())
            {
                return BadRequest(new { message = "رمز التحقق غير صحيح، يرجى التأكد من الرمز" });
            }

            if (user.ResetOtpExpiry.HasValue && user.ResetOtpExpiry.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد" });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.ResetOtp = null;
            user.ResetOtpExpiry = null;
            await _context.SaveChangesAsync();

            var clientIpR = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            AuditLogger.Log(user.Id, "RESET_PASSWORD_SUCCESS", $"تم تغيير كلمة المرور بنجاح للمستخدم: {user.FullName} ({cleanPhone})", clientIpR);

            return Ok(new { message = "تم تغيير كلمة المرور بنجاح، يمكنك الآن تسجيل الدخول" });
        }

        private async Task<bool> SendWhatsAppMessageAsync(string phone, string message)
        {
            try
            {
                string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
                string cleanPhone = PhoneHelper.Normalize(phone);
                if (string.IsNullOrEmpty(cleanPhone)) return false;

                // 1. Fetch official designated OTP sender instance from SystemSettings
                int officialUserId = 1;
                try
                {
                    var setting = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "OtpSenderUserId");
                    if (setting != null && int.TryParse(setting.Value, out int parsed) && parsed > 0)
                    {
                        officialUserId = parsed;
                    }
                }
                catch { }

                string primaryInstance = $"user_{officialUserId}";

                // 2. Build candidate list starting with the official designated instance
                var candidateInstances = new List<string> { primaryInstance };
                
                // Add any other connected instances as fallback
                try
                {
                    var activeSessions = await _context.WhatsAppSessions
                        .AsNoTracking()
                        .Where(s => (s.Status == "متصل" || s.Status == "open" || s.Status == "connected") && s.UserId != officialUserId)
                        .Select(s => $"user_{s.UserId}")
                        .ToListAsync();
                    candidateInstances.AddRange(activeSessions);
                }
                catch { }

                candidateInstances.Add("user_1");
                candidateInstances.Add("user_3");
                var distinctInstances = candidateInstances.Distinct().ToList();

                using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();

                foreach (var inst in distinctInstances)
                {
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Post, $"{nodeUrl}/message/sendText/{inst}");
                        req.Headers.Add("apikey", apiKey);
                        req.Content = JsonContent.Create(new
                        {
                            number = cleanPhone,
                            text = message
                        });
                        var res = await client.SendAsync(req);
                        if (res.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("[OTP Sender] Successfully sent OTP to {Phone} via designated instance: {Instance}", cleanPhone, inst);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[OTP Sender] Failed to send via {Instance}: {Err}", inst, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp OTP to {Phone}", phone);
            }
            return false;
        }

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "EqfalSuperSecretKeyForJWTAuth_1234567890";
            var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim("phone", user.Phone ?? string.Empty),
                    new Claim("email", user.UsernameEmail)
                }),
                Expires = DateTime.UtcNow.AddDays(30),
                Issuer = _configuration["Jwt:Issuer"] ?? "EqfalAPI",
                Audience = _configuration["Jwt:Audience"] ?? "EqfalApp",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [Authorize]
[HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                        || c.Type == "nameid" 
                                                        || c.Type == "sub" 
                                                        || c.Type.Contains("nameidentifier"))?.Value;

            int userId = 1;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int parsedId))
            {
                userId = parsedId;
            }

            var user = await _context.Users
                .Include(u => u.WhatsAppSession)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "المستخدم غير موجود" });
            }

            string rawStatus = user.WhatsAppSession?.Status ?? "";
            bool isConnected = (rawStatus.Contains("متصل") && !rawStatus.Contains("غير")) || 
                               rawStatus.Equals("open", StringComparison.OrdinalIgnoreCase) || 
                               rawStatus.Equals("connected", StringComparison.OrdinalIgnoreCase);

            // Live check from Evolution API if not already confirmed connected
            if (!isConnected)
            {
                try
                {
                    string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                    string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
                    string instanceName = $"user_{userId}";

                    using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(2);
                    using var req = new HttpRequestMessage(HttpMethod.Get, $"{nodeUrl}/instance/connectionState/{instanceName}");
                    req.Headers.Add("apikey", apiKey);
                    var res = await client.SendAsync(req);
                    if (res.IsSuccessStatusCode)
                    {
                        var jsonStr = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonStr);
                        if (doc.RootElement.TryGetProperty("instance", out var instProp) &&
                            instProp.TryGetProperty("state", out var stateProp))
                        {
                            var state = stateProp.GetString() ?? "";
                            if (state.Equals("open", StringComparison.OrdinalIgnoreCase))
                            {
                                isConnected = true;
                                if (user.WhatsAppSession != null)
                                {
                                    user.WhatsAppSession.Status = "متصل";
                                    user.WhatsAppSession.LastConnected = DateTime.UtcNow;
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            string linkedPhone = isConnected 
                ? (!string.IsNullOrEmpty(user.WhatsAppSession?.PairingCode) && PhoneHelper.IsRealPhone(user.WhatsAppSession.PairingCode) ? user.WhatsAppSession.PairingCode : (user.Phone ?? ""))
                : "";

            return Ok(new
            {
                id = user.Id,
                name = user.FullName,
                fullName = user.FullName,
                phone = user.Phone ?? "",
                email = user.UsernameEmail,
                usernameEmail = user.UsernameEmail,
                createdAt = user.CreatedAt.ToString("yyyy/MM/dd"),
                isWhatsAppConnected = isConnected,
                linkedWhatsAppPhone = linkedPhone,
                linkedWhatsAppName = isConnected ? user.FullName : "",
                whatsappStatus = isConnected ? "متصل" : "غير متصل"
            });
        }

        public class UpdateProfileRequest
        {
            public string? FullName { get; set; }
            public string? Phone { get; set; }
            public string? Email { get; set; }
            public string? UsernameEmail { get; set; }
            public string? OldPassword { get; set; }
            public string? NewPassword { get; set; }
        }

        [Authorize]
        [HttpPost("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                            || c.Type == "nameid" 
                                                            || c.Type == "sub" 
                                                            || c.Type.Contains("nameidentifier"))?.Value;

                int userId = 1;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int parsedId))
                {
                    userId = parsedId;
                }

                var user = await _context.Users
                    .Include(u => u.WhatsAppSession)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new { message = "المستخدم غير موجود" });
                }

                if (!string.IsNullOrWhiteSpace(request.FullName))
                    user.FullName = request.FullName.Trim();

                if (!string.IsNullOrWhiteSpace(request.Phone))
                {
                    string cleanPhone = PhoneHelper.Normalize(request.Phone);
                    var phoneExists = await _context.Users.AnyAsync(u => u.Id != userId && u.Phone == cleanPhone);
                    if (phoneExists)
                return BadRequest(new { message = "\u064a\u0631\u062c\u0649\u0020\u0625\u062f\u062e\u0627\u0644\u0020\u0627\u0633\u0645\u0020\u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u0020\u0648\u0643\u0644\u0645\u0629\u0020\u0627\u0644\u0645\u0631\u0648\u0631" });
                    user.Phone = cleanPhone;
                }

                string emailToUpdate = !string.IsNullOrWhiteSpace(request.Email) ? request.Email : (!string.IsNullOrWhiteSpace(request.UsernameEmail) ? request.UsernameEmail : "");
                if (!string.IsNullOrWhiteSpace(emailToUpdate))
                {
                    string cleanEmail = emailToUpdate.Trim().ToLower();
                    var emailExists = await _context.Users.AnyAsync(u => u.Id != userId && u.UsernameEmail == cleanEmail);
                    if (emailExists)
                return BadRequest(new { message = "\u064a\u0631\u062c\u0649\u0020\u0625\u062f\u062e\u0627\u0644\u0020\u0627\u0633\u0645\u0020\u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u0020\u0648\u0643\u0644\u0645\u0629\u0020\u0627\u0644\u0645\u0631\u0648\u0631" });
                    user.UsernameEmail = cleanEmail;
                }

                if (!string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    if (string.IsNullOrWhiteSpace(request.OldPassword))
                    {
                return BadRequest(new { message = "\u064a\u0631\u062c\u0649\u0020\u0625\u062f\u062e\u0627\u0644\u0020\u0627\u0633\u0645\u0020\u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u0020\u0648\u0643\u0644\u0645\u0629\u0020\u0627\u0644\u0645\u0631\u0648\u0631" });
                    }

                    bool isOldValid = user.PasswordHash.StartsWith("$2") 
                        ? BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash)
                        : (user.PasswordHash == request.OldPassword);

                    if (!isOldValid)
                    {
                return BadRequest(new { message = "\u064a\u0631\u062c\u0649\u0020\u0625\u062f\u062e\u0627\u0644\u0020\u0627\u0633\u0645\u0020\u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u0020\u0648\u0643\u0644\u0645\u0629\u0020\u0627\u0644\u0645\u0631\u0648\u0631" });
                    }

                    if (request.NewPassword.Length < 8)
                    {
                return BadRequest(new { message = "\u064a\u0631\u062c\u0649\u0020\u0625\u062f\u062e\u0627\u0644\u0020\u0627\u0633\u0645\u0020\u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645\u0020\u0648\u0643\u0644\u0645\u0629\u0020\u0627\u0644\u0645\u0631\u0648\u0631" });
                    }

                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                }

                await _context.SaveChangesAsync();

                var clientIpP = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                AuditLogger.Log(user.Id, "UPDATE_PROFILE", $"تم تحديث بيانات الملف الشخصي للمستخدم: {user.FullName}", clientIpP);

                string rawStatus = user.WhatsAppSession?.Status ?? "";
                bool isConnected = (rawStatus.Contains("متصل") && !rawStatus.Contains("غير")) || 
                                   rawStatus.Equals("open", StringComparison.OrdinalIgnoreCase) || 
                                   rawStatus.Equals("connected", StringComparison.OrdinalIgnoreCase);

                string linkedPhone = isConnected 
                    ? (!string.IsNullOrEmpty(user.WhatsAppSession?.PairingCode) && PhoneHelper.IsRealPhone(user.WhatsAppSession.PairingCode) ? user.WhatsAppSession.PairingCode : (user.Phone ?? ""))
                    : "";

                return Ok(new
                {
                    message = "تم تحديث البيانات بنجاح",
                    user = new
                    {
                        id = user.Id,
                        fullName = user.FullName,
                        name = user.FullName,
                        phone = user.Phone ?? "",
                        email = user.UsernameEmail,
                        usernameEmail = user.UsernameEmail,
                        createdAt = user.CreatedAt.ToString("yyyy/MM/dd"),
                        isWhatsAppConnected = isConnected,
                        linkedWhatsAppPhone = linkedPhone,
                        linkedWhatsAppName = isConnected ? user.FullName : ""
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return StatusCode(500, new { message = "حدث خطأ أثناء تحديث البيانات: " + ex.Message });
            }
        }

        [Authorize]
        [HttpPost("delete-account")]
        [HttpDelete("account")]
        public async Task<IActionResult> DeleteAccount()
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                            || c.Type == "nameid" 
                                                            || c.Type == "sub" 
                                                            || c.Type.Contains("nameidentifier"))?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "جلسة المستخدم غير صالحة" });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    return NotFound(new { message = "المستخدم غير موجود" });
                }

                // 1. Delete WhatsApp Instance in Evolution API
                try
                {
                    string nodeUrl = (_configuration["WhatsAppService:NodeUrl"] ?? "http://localhost:8080").TrimEnd('/');
                    string apiKey = _configuration["WhatsAppService:ApiKey"] ?? "MostanadWdsSecretKey2026!";
                    string instanceName = $"user_{userId}";

                    using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
                    using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"{nodeUrl}/instance/delete/{instanceName}");
                    delReq.Headers.Add("apikey", apiKey);
                    await client.SendAsync(delReq);
                }
                catch { }

                // 2. Remove all User-related data from DB
                var operations = await _context.Operations.Where(o => o.UserId == userId).ToListAsync();
                if (operations.Any()) _context.Operations.RemoveRange(operations);

                var numbers = await _context.MonitoredNumbers.Where(m => m.UserId == userId).ToListAsync();
                if (numbers.Any()) _context.MonitoredNumbers.RemoveRange(numbers);

                var keywords = await _context.DynamicKeywords.Where(k => k.UserId == userId).ToListAsync();
                if (keywords.Any()) _context.DynamicKeywords.RemoveRange(keywords);

                var sessions = await _context.WhatsAppSessions.Where(s => s.UserId == userId).ToListAsync();
                if (sessions.Any()) _context.WhatsAppSessions.RemoveRange(sessions);

                var logs = await _context.AuditLogs.Where(l => l.UserId == userId).ToListAsync();
                if (logs.Any()) _context.AuditLogs.RemoveRange(logs);

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                AuditLogger.Log(userId, "ACCOUNT_DELETED", $"تم حذف الحساب بالكامل: {user.FullName} ({user.Phone})", clientIp);

                return Ok(new { success = true, message = "تم حذف الحساب وكافة بياناته نهائياً" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user account");
                return StatusCode(500, new { message = "حدث خطأ أثناء حذف الحساب: " + ex.Message });
            }
        }
    }
}
