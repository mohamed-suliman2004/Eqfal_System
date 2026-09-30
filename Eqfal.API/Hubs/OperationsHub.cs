using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Eqfal.API.Hubs
{
    public class OperationsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userIdClaim = Context.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier 
                                                                    || c.Type == "nameid" 
                                                                    || c.Type == "sub" 
                                                                    || c.Type.Contains("nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                try
                {
                    var httpCtx = Context.GetHttpContext();
                    var token = httpCtx?.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        var parts = token.Split('.');
                        if (parts.Length > 1)
                        {
                            string rawBase64 = parts[1].PadRight((parts[1].Length + 3) / 4 * 4, '=').Replace('-', '+').Replace('_', '/');
                            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(rawBase64));
                            using var doc = JsonDocument.Parse(payloadJson);
                            if (doc.RootElement.TryGetProperty("nameid", out var nid)) userIdClaim = nid.GetString();
                            else if (doc.RootElement.TryGetProperty("sub", out var sub)) userIdClaim = sub.GetString();
                            else if (doc.RootElement.TryGetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", out var wsNid)) userIdClaim = wsNid.GetString();
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId) && userId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            await base.OnConnectedAsync();
        }

        public async Task JoinUserGroup(int userId)
        {
            if (userId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
        }
    }
}
