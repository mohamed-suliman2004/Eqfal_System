using System.Threading.Tasks;

namespace Eqfal.API.Services
{
    public class MessageAnalysisResult
    {
        public string Category { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Party { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// True if the category was matched from a user-defined keyword.
        /// When true, the controller must NOT override it based on message direction.
        /// </summary>
        public bool CategoryFromKeyword { get; set; } = false;
        public bool IsConflicted { get; set; } = false;
    }

    public interface IMessageAnalysisService
    {
        Task<MessageAnalysisResult> AnalyzeMessageAsync(int userId, string text, string defaultParty = "", bool isOutgoing = false);
    }
}