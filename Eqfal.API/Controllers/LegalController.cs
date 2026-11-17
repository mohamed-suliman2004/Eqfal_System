using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Eqfal.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    public class LegalController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public LegalController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string GetHtmlContent(string fileName)
        {
            var candidates = new[]
            {
                Path.Combine(_env.WebRootPath ?? "", fileName),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", fileName),
                Path.Combine(AppContext.BaseDirectory, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), fileName)
            };

            foreach (var path in candidates)
            {
                if (System.IO.File.Exists(path))
                {
                    return System.IO.File.ReadAllText(path);
                }
            }

            return $"<!DOCTYPE html><html><head><meta charset='utf-8'><title>{fileName}</title></head><body><h1>{fileName}</h1><p>سياسة وخدمات منظومة إقفال - شركة المستند للحلول البرمجية.</p></body></html>";
        }

        [HttpGet("/privacy")]
        [HttpGet("/api/privacy")]
        [HttpGet("/privacy.html")]
        [HttpGet("/api/privacy.html")]
        public IActionResult GetPrivacy()
        {
            var html = GetHtmlContent("privacy.html");
            return Content(html, "text/html; charset=utf-8");
        }

        [HttpGet("/terms")]
        [HttpGet("/api/terms")]
        [HttpGet("/terms.html")]
        [HttpGet("/api/terms.html")]
        public IActionResult GetTerms()
        {
            var html = GetHtmlContent("terms.html");
            return Content(html, "text/html; charset=utf-8");
        }

        [HttpGet("/delete-account")]
        [HttpGet("/api/delete-account")]
        [HttpGet("/delete-account.html")]
        [HttpGet("/api/delete-account.html")]
        public IActionResult GetDeleteAccount()
        {
            var html = GetHtmlContent("delete-account.html");
            return Content(html, "text/html; charset=utf-8");
        }
    }
}
