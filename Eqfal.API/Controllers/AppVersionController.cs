using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Eqfal.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("[controller]")]
    [Route("api/[controller]")]
    [Route("app-version")]
    [Route("api/app-version")]
    [Route("version")]
    [Route("api/version")]
    public class AppVersionController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AppVersionController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult GetVersionInfo([FromQuery] string? platform = null, [FromQuery] int? buildNumber = null)
        {
            var section = _configuration.GetSection("AppVersion");

            var latestVersion = section["LatestVersion"] ?? "1.0.8";
            var latestBuildNumber = int.TryParse(section["LatestBuildNumber"], out var lbn) ? lbn : 8;
            var minRequiredVersion = section["MinRequiredVersion"] ?? "1.0.0";
            var minRequiredBuildNumber = int.TryParse(section["MinRequiredBuildNumber"], out var mbn) ? mbn : 1;
            var isForceUpdate = bool.TryParse(section["IsForceUpdate"], out var fu) && fu;
            var title = section["Title"] ?? "تحديث جديد متوفر";
            var releaseNotes = section["ReleaseNotes"] ?? "• تحسين سرعة واستقرار التطبيق.\n• دقة عالية في تحليل رسائل العمليات.\n• إصلاحات عامة في شاشة المسودات والتقارير.";
            var storeUrl = section["StoreUrl"] ?? "https://play.google.com/store/apps/details?id=ly.mostanad.eqfal_app";

            bool updateAvailable = buildNumber.HasValue && buildNumber.Value < latestBuildNumber;
            bool forceUpdate = isForceUpdate || (buildNumber.HasValue && buildNumber.Value < minRequiredBuildNumber);

            return Ok(new
            {
                latestVersion,
                latestBuildNumber,
                minRequiredVersion,
                minRequiredBuildNumber,
                isForceUpdate = forceUpdate,
                updateAvailable,
                title,
                releaseNotes,
                storeUrl
            });
        }
    }
}
