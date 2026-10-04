using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
using Microsoft.Extensions.Logging;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    [Authorize]
    public class OperationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<OperationsHub> _hubContext;
        private readonly IMessageAnalysisService _analysisService;
        private readonly ILogger<OperationsController> _logger;

        public OperationsController(
            AppDbContext context, 
            IHubContext<OperationsHub> hubContext, 
            IMessageAnalysisService analysisService,
            ILogger<OperationsController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _analysisService = analysisService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var claims = User.Claims;
            var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                      || c.Type == "nameid" 
                                                      || c.Type == "sub" 
                                                      || c.Type.Contains("nameidentifier"))?.Value;

            if (int.TryParse(userIdClaim, out int userId) && userId > 0)
                return userId;

            throw new UnauthorizedAccessException("المستخدم غير مسجل الدخول أو الرمز غير صالح.");
        }

        // GET: /Operations or /api/Operations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Operation>>> GetOperations()
        {
            try
            {
                int userId = GetCurrentUserId();

                var operations = await _context.Operations
                    .AsNoTracking()
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                // Upgrade any LID sender/receiver numbers to real phone numbers
                                Dictionary<string, string> mappings;
                try
                {
                    mappings = await _context.LidMappings
                        .AsNoTracking()
                        .Where(l => !string.IsNullOrEmpty(l.Lid))
                        .GroupBy(l => l.Lid)
                        .ToDictionaryAsync(g => g.Key, g => g.First().RealPhone);
                }
                catch
                {
                    mappings = new Dictionary<string, string>();
                }
                foreach (var op in operations)
                {
                    if (!string.IsNullOrEmpty(op.ReceiverNumber) && !PhoneHelper.IsRealPhone(op.ReceiverNumber))
                    {
                        string normRec = PhoneHelper.Normalize(op.ReceiverNumber);
                        if (mappings.TryGetValue(normRec, out var realRec) && PhoneHelper.IsRealPhone(realRec))
                        {
                            op.ReceiverNumber = realRec;
                        }
                    }
                    if (!string.IsNullOrEmpty(op.SenderNumber) && !PhoneHelper.IsRealPhone(op.SenderNumber))
                    {
                        string normSnd = PhoneHelper.Normalize(op.SenderNumber);
                        if (mappings.TryGetValue(normSnd, out var realSnd) && PhoneHelper.IsRealPhone(realSnd))
                        {
                            op.SenderNumber = realSnd;
                        }
                    }
                }

                return operations;
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "جلسة العمل منتهية أو غير صالحة" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching operations");
                return StatusCode(500, new { message = "حدث خطأ أثناء جلب العمليات" });
            }
        }

        // GET: /Operations/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Operation>> GetOperation(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                var operation = await _context.Operations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
                if (operation == null)
                {
                    return NotFound(new { message = "العملية غير موجودة" });
                }

                if (!string.IsNullOrEmpty(operation.ReceiverNumber) && !PhoneHelper.IsRealPhone(operation.ReceiverNumber))
                {
                    string normRec = PhoneHelper.Normalize(operation.ReceiverNumber);
                    var map = await _context.LidMappings.AsNoTracking().FirstOrDefaultAsync(l => l.Lid == normRec);
                    if (map != null && PhoneHelper.IsRealPhone(map.RealPhone))
                    {
                        operation.ReceiverNumber = map.RealPhone;
                    }
                }

                if (!string.IsNullOrEmpty(operation.SenderNumber) && !PhoneHelper.IsRealPhone(operation.SenderNumber))
                {
                    string normSnd = PhoneHelper.Normalize(operation.SenderNumber);
                    var map = await _context.LidMappings.AsNoTracking().FirstOrDefaultAsync(l => l.Lid == normSnd);
                    if (map != null && PhoneHelper.IsRealPhone(map.RealPhone))
                    {
                        operation.SenderNumber = map.RealPhone;
                    }
                }

                return operation;
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }

        // POST: /Operations
        [HttpPost]
        public async Task<ActionResult<Operation>> PostOperation([FromBody] Operation operation)
        {
            try
            {
                int userId = GetCurrentUserId();
                operation.UserId = userId;
                operation.CreatedAt = DateTime.UtcNow;
                _context.Operations.Add(operation);
                await _context.SaveChangesAsync();

                // بث فوري للمستخدم فقط
                try 
                { 
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNewOperation", operation);
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", operation);
                } 
                catch { }

                return CreatedAtAction(nameof(GetOperation), new { id = operation.Id }, operation);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating operation");
                return StatusCode(500, new { message = "حدث خطأ أثناء إنشاء العملية" });
            }
        }

        // PUT: /Operations/5 (or POST: /Operations/5, /Operations/5/update)
        [HttpPut("{id}")]
        [HttpPost("{id}")]
        [HttpPost("{id}/update")]
        public async Task<IActionResult> PutOperation(int id, [FromBody] Operation updateDto)
        {
            try
            {
                int userId = GetCurrentUserId();
                var existingOp = await _context.Operations.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
                if (existingOp == null)
                {
                    return NotFound(new { message = "العملية غير موجودة أو تابعة لمستخدم آخر" });
                }

                if (!string.IsNullOrWhiteSpace(updateDto.Category)) existingOp.Category = updateDto.Category;
                if (updateDto.Amount.HasValue) existingOp.Amount = updateDto.Amount;
                if (!string.IsNullOrWhiteSpace(updateDto.Currency)) existingOp.Currency = updateDto.Currency;
                if (!string.IsNullOrWhiteSpace(updateDto.Party)) existingOp.Party = updateDto.Party;
                if (!string.IsNullOrWhiteSpace(updateDto.SenderNumber)) existingOp.SenderNumber = updateDto.SenderNumber;
                if (!string.IsNullOrWhiteSpace(updateDto.ReceiverNumber)) existingOp.ReceiverNumber = updateDto.ReceiverNumber;
                if (!string.IsNullOrWhiteSpace(updateDto.Source)) existingOp.Source = updateDto.Source;
                if (updateDto.Notes != null) existingOp.Notes = updateDto.Notes;
                if (!string.IsNullOrWhiteSpace(updateDto.Status)) existingOp.Status = updateDto.Status;
                existingOp.IsReviewed = updateDto.IsReviewed;
                existingOp.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // بث فوري للمستخدم فقط
                try 
                { 
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNewOperation", existingOp);
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", existingOp);
                } 
                catch { }

                return Ok(existingOp);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating operation {Id}", id);
                return StatusCode(500, new { message = "حدث خطأ أثناء تعديل العملية: " + ex.Message });
            }
        }

        // DELETE: /Operations/5 (or POST: /Operations/5/delete)
        [HttpDelete("{id}")]
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteOperation(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                var operation = await _context.Operations.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
                if (operation == null)
                {
                    return NotFound(new { message = "العملية غير موجودة أو تابعة لمستخدم آخر" });
                }

                _context.Operations.Remove(operation);
                await _context.SaveChangesAsync();

                // بث فوري للمستخدم فقط
                try 
                { 
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNewOperation", new { id, deleted = true });
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", new { id, deleted = true });
                } 
                catch { }

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting operation {Id}", id);
                return StatusCode(500, new { message = "حدث خطأ أثناء حذف العملية: " + ex.Message });
            }
        }

        public class BatchDeleteRequest
        {
            public List<int> Ids { get; set; } = new();
        }

        // POST/DELETE: /Operations/batch-delete
        [HttpPost("batch-delete")]
        [HttpDelete("batch-delete")]
        public async Task<IActionResult> BatchDeleteOperations([FromBody] BatchDeleteRequest request)
        {
            if (request == null || request.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest(new { message = "لم يتم تحديد أي عمليات للحذف" });
            }

            try
            {
                int userId = GetCurrentUserId();
                var operations = await _context.Operations
                    .Where(o => o.UserId == userId && request.Ids.Contains(o.Id))
                    .ToListAsync();

                if (operations.Count == 0)
                {
                    return NotFound(new { message = "لم يتم العثور على العمليات المحددة" });
                }

                int deletedCount = operations.Count;
                var deletedIds = operations.Select(o => o.Id).ToList();

                _context.Operations.RemoveRange(operations);
                await _context.SaveChangesAsync();

                try
                {
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveOperationsBatchDeleted", deletedIds);
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveOperationsBatchDeleted", deletedIds);
                }
                catch { }

                return Ok(new { message = $"تم حذف {deletedCount} عملية بنجاح", count = deletedCount, deletedIds });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch deleting operations");
                return StatusCode(500, new { message = "حدث خطأ أثناء الحذف الجماعي: " + ex.Message });
            }
        }

        // POST: /Operations/{id}/restore
        [HttpPost("{id}/restore")]
        [HttpPut("{id}/restore")]
        public async Task<IActionResult> RestoreOperation(int id)
        {
            try
            {
                int userId = GetCurrentUserId();
                var operation = await _context.Operations.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
                if (operation == null)
                {
                    return NotFound(new { message = "العملية غير موجودة أو تابعة لمستخدم آخر" });
                }

                operation.Status = "معتمد";
                operation.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                try
                {
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNewOperation", operation);
                    await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNewOperation", operation);
                }
                catch { }

                return Ok(operation);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring operation {Id}", id);
                return StatusCode(500, new { message = "حدث خطأ أثناء استعادة العملية: " + ex.Message });
            }
        }

        public class AnalyzePayload
        {
            public string? Text { get; set; }
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeMessage([FromBody] AnalyzePayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.Text))
                return BadRequest("النص مطلوب");

            try
            {
                int userId = GetCurrentUserId();
                var analysis = await _analysisService.AnalyzeMessageAsync(userId, payload.Text);

                return Ok(new
                {
                    category = analysis.Category,
                    amount = analysis.Amount,
                    currency = analysis.Currency,
                    party = analysis.Party,
                    source = "تحليل يدوي",
                    notes = string.IsNullOrWhiteSpace(analysis.Notes) ? null : analysis.Notes
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
        }
    }
}