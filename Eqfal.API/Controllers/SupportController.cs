using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Eqfal.API.Models;
using Eqfal.API.Services;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class SupportController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IEmailService _emailService;

        public SupportController(AppDbContext db, IEmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        public class CreateSupportTicketDto
        {
            public string FullName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? Subject { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        [HttpPost("ticket")]
        public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequest(new { message = "يرجى كتابة الاسم بالكامل." });

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                return BadRequest(new { message = "يرجى كتابة رقم الهاتف للتواصل." });

            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "يرجى كتابة تفاصيل المشكلة." });

            int? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var parsedId))
            {
                userId = parsedId;
            }

            var cleanPhone = dto.PhoneNumber.Trim();

            // Anti-Spam: Check cooldown (minimum 2 minutes between tickets)
            var cooldownTime = DateTime.UtcNow.AddMinutes(-2);
            var hasRecentTicket = await _db.SupportTickets.AnyAsync(t =>
                (userId.HasValue && t.UserId == userId.Value && t.CreatedAt >= cooldownTime) ||
                (t.PhoneNumber == cleanPhone && t.CreatedAt >= cooldownTime));

            if (hasRecentTicket)
            {
                return StatusCode(429, new
                {
                    success = false,
                    message = "لقد قمت بإرسال طلب دعم مؤخراً. يرجى الانتظار دقيقتين قبل إرسال طلب جديد لمنع التكرار."
                });
            }

            // Anti-Spam: Daily limit (maximum 5 tickets per user/phone in 24 hours)
            var dayAgo = DateTime.UtcNow.AddHours(-24);
            var dailyCount = await _db.SupportTickets.CountAsync(t =>
                (userId.HasValue && t.UserId == userId.Value && t.CreatedAt >= dayAgo) ||
                (t.PhoneNumber == cleanPhone && t.CreatedAt >= dayAgo));

            if (dailyCount >= 5)
            {
                return StatusCode(429, new
                {
                    success = false,
                    message = "تم الوصول للحد الأقصى المسموح لطلبات الدعم اليوم (5 طلبات). فريقنا يعمل على معالجة طلباتك السابقة."
                });
            }

            var ticket = new SupportTicket
            {
                UserId = userId,
                FullName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                Subject = string.IsNullOrWhiteSpace(dto.Subject) ? "طلب دعم فني عام" : dto.Subject.Trim(),
                Message = dto.Message.Trim(),
                CreatedAt = DateTime.UtcNow,
                Status = "جديد"
            };

            _db.SupportTickets.Add(ticket);
            await _db.SaveChangesAsync();

            // Attempt to send email asynchronously (does not block or fail ticket creation if SMTP not configured)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendSupportTicketNotificationAsync(ticket);
                }
                catch { }
            });

            return Ok(new
            {
                success = true,
                ticketId = ticket.Id,
                message = "تم استلام طلبك بنجاح. سيتواصل معك فريق الدعم الفني في أقرب وقت."
            });
        }

        [HttpGet("tickets")]
        public async Task<IActionResult> GetTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var query = _db.SupportTickets.AsNoTracking().OrderByDescending(t => t.CreatedAt);
            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                items
            });
        }
    }
}
