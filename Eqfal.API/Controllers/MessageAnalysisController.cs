using Microsoft.AspNetCore.Mvc;
using Eqfal.API.Services;
using System.Threading.Tasks;

namespace Eqfal.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    public class MessageAnalysisController : ControllerBase
    {
        private readonly IMessageAnalysisService _analysisService;

        public MessageAnalysisController(IMessageAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        public class AnalyzeRequest
        {
            public int UserId { get; set; } = 1;
            public string Text { get; set; } = string.Empty;
            public string DefaultParty { get; set; } = "";
            public bool IsOutgoing { get; set; } = false;
        }

        [HttpPost("analyze")]
        [HttpPost("test")]
        public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Text cannot be empty" });
            }

            var result = await _analysisService.AnalyzeMessageAsync(request.UserId, request.Text, request.DefaultParty, request.IsOutgoing);
            return Ok(result);
        }

        [HttpGet("test-gemini")]
        public async Task<IActionResult> TestGemini(
            [FromServices] GeminiAnalysisService gemini,
            [FromServices] Microsoft.Extensions.Configuration.IConfiguration config)
        {
            string? key = config["Gemini:ApiKey"];
            string model = config["Gemini:Model"] ?? "gemini-3.6-flash";
            string maskedKey = string.IsNullOrWhiteSpace(key) ? "NONE" : (key.Length > 8 ? key[..8] + "..." : key);

            var diag = await gemini.TestConnectionAsync();

            return Ok(new
            {
                configuredModel = model,
                configuredKey = maskedKey,
                aiWorking = diag.success,
                details = diag.details
            });
        }
    }
}