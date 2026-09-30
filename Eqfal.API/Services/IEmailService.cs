using System.Threading.Tasks;
using Eqfal.API.Models;

namespace Eqfal.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendSupportTicketNotificationAsync(SupportTicket ticket);
    }
}
