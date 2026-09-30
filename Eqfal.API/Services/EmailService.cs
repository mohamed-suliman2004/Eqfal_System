using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Eqfal.API.Models;

namespace Eqfal.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> SendSupportTicketNotificationAsync(SupportTicket ticket)
        {
            try
            {
                var smtpServer = _config["EmailSettings:SmtpServer"] ?? "smtp.zoho.com";
                var port = int.TryParse(_config["EmailSettings:Port"], out var p) ? p : 587;
                var useSsl = bool.TryParse(_config["EmailSettings:UseSsl"], out var ssl) ? ssl : true;
                var senderEmail = _config["EmailSettings:SenderEmail"] ?? "support@mostanad.ly";
                var senderName = _config["EmailSettings:SenderName"] ?? "نظام إقفال - الدعم الفني";
                var supportEmail = _config["EmailSettings:SupportEmail"] ?? "support@mostanad.ly";
                var password = _config["EmailSettings:Password"];

                if (string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning("EmailService: SMTP Password is not configured yet. Ticket #{Id} saved to database.", ticket.Id);
                    return false;
                }

                using var message = new MailMessage();
                message.From = new MailAddress(senderEmail, senderName);
                message.To.Add(new MailAddress(supportEmail));
                if (!string.IsNullOrWhiteSpace(ticket.Email))
                {
                    try
                    {
                        message.ReplyToList.Add(new MailAddress(ticket.Email, ticket.FullName));
                    }
                    catch { }
                }

                message.Subject = $"[طلب دعم فني #{ticket.Id}] من: {ticket.FullName}";
                message.IsBodyHtml = true;

                message.Body = $@"
<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
<meta charset=""utf-8"">
<style>
    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f1f5f9; margin: 0; padding: 20px; }}
    .card {{ background-color: #ffffff; border-radius: 12px; max-width: 600px; margin: auto; padding: 24px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); direction: rtl; text-align: right; }}
    .header {{ border-bottom: 2px solid #e2e8f0; padding-bottom: 16px; margin-bottom: 20px; }}
    .header h2 {{ color: #1e293b; margin: 0; font-size: 20px; }}
    .badge {{ display: inline-block; background: #e0e7ff; color: #4338ca; padding: 4px 10px; border-radius: 6px; font-weight: bold; font-size: 12px; margin-top: 6px; }}
    .field {{ margin-bottom: 14px; }}
    .label {{ color: #64748b; font-size: 13px; font-weight: bold; margin-bottom: 4px; }}
    .value {{ color: #0f172a; font-size: 15px; background: #f8fafc; padding: 10px 14px; border-radius: 8px; border: 1px solid #e2e8f0; }}
    .message-box {{ background: #f8fafc; border: 1px solid #cbd5e1; border-right: 4px solid #6366f1; padding: 14px; border-radius: 8px; white-space: pre-wrap; font-size: 15px; color: #1e293b; }}
    .footer {{ margin-top: 24px; font-size: 12px; color: #94a3b8; text-align: center; }}
</style>
</head>
<body>
<div class=""card"">
    <div class=""header"">
        <h2>📩 طلب دعم فني جديد من تطبيق إقفال</h2>
        <div class=""badge"">رقم التذكرة: #{ticket.Id}</div>
    </div>
    <div class=""field"">
        <div class=""label"">👤 اسم العميل:</div>
        <div class=""value"">{ticket.FullName}</div>
    </div>
    <div class=""field"">
        <div class=""label"">📞 رقم الهاتف:</div>
        <div class=""value""><a href=""tel:{ticket.PhoneNumber}"" style=""color:#4338ca; text-decoration:none;"">{ticket.PhoneNumber}</a></div>
    </div>
    <div class=""field"">
        <div class=""label"">✉️ البريد الإلكتروني:</div>
        <div class=""value"">{(string.IsNullOrWhiteSpace(ticket.Email) ? "غير متوفر" : $"<a href=\"mailto:{ticket.Email}\" style=\"color:#4338ca; text-decoration:none;\">{ticket.Email}</a>")}</div>
    </div>
    <div class=""field"">
        <div class=""label"">📅 تاريخ وتوقيت الطلب:</div>
        <div class=""value"">{ticket.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC</div>
    </div>
    <div class=""field"">
        <div class=""label"">📝 تفاصيل المشكلة:</div>
        <div class=""message-box"">{WebUtility.HtmlEncode(ticket.Message)}</div>
    </div>
    <div class=""footer"">
        تم إرسال هذا الإشعار تلقائياً بواسطة نظام إقفال (Eqfal System)
    </div>
</div>
</body>
</html>";

                using var client = new SmtpClient(smtpServer, port)
                {
                    EnableSsl = useSsl,
                    Credentials = new NetworkCredential(senderEmail, password),
                    Timeout = 10000
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("EmailService: Support ticket #{Id} email sent successfully to {SupportEmail}", ticket.Id, supportEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailService: Failed to send support ticket #{Id} email.", ticket.Id);
                return false;
            }
        }
    }
}
