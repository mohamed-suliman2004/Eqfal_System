using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eqfal.API.Data;
using Eqfal.API.Models;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class WhatsAppAuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WhatsAppAuthController(AppDbContext context)
        {
            _context = context;
        }

        // GET /WhatsAppAuth/active-users
        [HttpGet("active-users")]
        public async Task<IActionResult> GetActiveUserIds()
        {
            var userIds = await _context.WhatsAppAuthStates
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync();

            return Ok(userIds);
        }

        // GET /WhatsAppAuth/get-all?userId=1
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllKeys([FromQuery] int userId)
        {
            if (userId <= 0) return BadRequest(new { message = "معرّف المستخدم مطلوب" });

            var states = await _context.WhatsAppAuthStates
                .Where(a => a.UserId == userId)
                .Select(a => new { keyId = a.KeyId, jsonData = a.JsonData })
                .ToListAsync();

            var result = new Dictionary<string, string>();
            foreach (var s in states)
            {
                result[s.keyId] = s.jsonData;
            }

            return Ok(result);
        }

        public class SaveBatchItem
        {
            public string KeyId { get; set; } = string.Empty;
            public string JsonData { get; set; } = string.Empty;
        }

        public class SaveBatchRequest
        {
            public int UserId { get; set; }
            public List<SaveBatchItem> Items { get; set; } = new();
        }

        // POST /WhatsAppAuth/save-batch
        [HttpPost("save-batch")]
        public async Task<IActionResult> SaveBatch([FromBody] SaveBatchRequest request)
        {
            if (request.UserId <= 0 || request.Items == null || !request.Items.Any())
            {
                return Ok(new { success = true });
            }

            var keyIds = request.Items.Select(i => i.KeyId).Distinct().ToList();

            var existingMap = await _context.WhatsAppAuthStates
                .Where(a => a.UserId == request.UserId && keyIds.Contains(a.KeyId))
                .ToDictionaryAsync(a => a.KeyId);

            var now = DateTime.UtcNow;

            foreach (var item in request.Items)
            {
                if (string.IsNullOrWhiteSpace(item.KeyId)) continue;

                if (existingMap.TryGetValue(item.KeyId, out var existing))
                {
                    existing.JsonData = item.JsonData;
                    existing.UpdatedAt = now;
                }
                else
                {
                    _context.WhatsAppAuthStates.Add(new WhatsAppAuthState
                    {
                        UserId = request.UserId,
                        KeyId = item.KeyId,
                        JsonData = item.JsonData,
                        UpdatedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        public class DeleteKeyRequest
        {
            public int UserId { get; set; }
            public List<string> KeyIds { get; set; } = new();
        }

        // POST /WhatsAppAuth/delete-keys
        [HttpPost("delete-keys")]
        public async Task<IActionResult> DeleteKeys([FromBody] DeleteKeyRequest request)
        {
            if (request.UserId <= 0 || request.KeyIds == null || !request.KeyIds.Any())
            {
                return Ok(new { success = true });
            }

            var itemsToDelete = await _context.WhatsAppAuthStates
                .Where(a => a.UserId == request.UserId && request.KeyIds.Contains(a.KeyId))
                .ToListAsync();

            if (itemsToDelete.Any())
            {
                _context.WhatsAppAuthStates.RemoveRange(itemsToDelete);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        // POST /WhatsAppAuth/clear?userId=1
        [HttpPost("clear")]
        public async Task<IActionResult> ClearAll([FromQuery] int userId)
        {
            if (userId <= 0) return BadRequest(new { message = "معرّف المستخدم مطلوب" });

            var items = await _context.WhatsAppAuthStates
                .Where(a => a.UserId == userId)
                .ToListAsync();

            if (items.Any())
            {
                _context.WhatsAppAuthStates.RemoveRange(items);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "تم مسح بيانات الجلسة بالكامل" });
        }
    }
}
