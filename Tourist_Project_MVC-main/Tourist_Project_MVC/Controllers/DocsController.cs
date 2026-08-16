using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Services;

namespace Tourist_Project_MVC.Controllers
{
    [Route("Docs")]
    public class DocsController : Controller
    {
        private readonly IDocContentProvider _provider;

        public DocsController(IDocContentProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Returns the documentation sections the current user is allowed to see.
        /// - Admin: For Tourists + For Sponsors + For Admins
        /// - Sponsor: For Sponsors only
        /// - Tourist (User): For Tourists only
        /// - Anonymous: For Tourists + For Sponsors (Admin docs are never exposed)
        /// </summary>
        private IReadOnlyList<string> GetAllowedSections()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return new[] { "tourists", "sponsors" };

            if (User.IsInRole("Admin"))
                return new[] { "tourists", "sponsors", "admins" };

            if (User.IsInRole("Sponsor"))
                return new[] { "sponsors" };

            return new[] { "tourists" };
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var allowed = GetAllowedSections();
            var sections = (await _provider.GetSectionsAsync(cancellationToken))
                .Where(s => allowed.Contains(s.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();
            return View("Landing", sections);
        }

        [HttpGet("{section}/{slug}")]
        public async Task<IActionResult> Article(string section, string slug, CancellationToken cancellationToken)
        {
            var allowed = GetAllowedSections();
            if (!allowed.Contains(section, StringComparer.OrdinalIgnoreCase))
                return NotFound();

            var article = await _provider.GetArticleAsync(section, slug, cancellationToken);
            if (article == null)
                return NotFound();

            var sections = (await _provider.GetSectionsAsync(cancellationToken))
                .Where(s => allowed.Contains(s.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();
            var flat = sections.SelectMany(s => s.Articles).OrderBy(a => a.Order).ToList();
            var currentIndex = flat.FindIndex(a => a.Section.Equals(section, System.StringComparison.OrdinalIgnoreCase) && a.Slug.Equals(slug, System.StringComparison.OrdinalIgnoreCase));

            var vm = new View_Model.DocViewModel
            {
                Article = article,
                Sections = sections,
                PrevArticle = currentIndex > 0 ? flat[currentIndex - 1] : null,
                NextArticle = currentIndex < flat.Count - 1 ? flat[currentIndex + 1] : null
            };

            return View("Article", vm);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? section, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(System.Array.Empty<DocSearchResult>());

            var results = await _provider.SearchAsync(q, section, cancellationToken);
            var allowed = GetAllowedSections();
            results = results.Where(r => allowed.Contains(r.Section, StringComparer.OrdinalIgnoreCase)).ToList();
            return Json(results);
        }

        [HttpGet("reload")]
        public async Task<IActionResult> Reload([FromQuery] string? token, CancellationToken cancellationToken)
        {
            var expected = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Docs:ReloadToken"];
            if (string.IsNullOrEmpty(expected) || expected != token)
                return Unauthorized();

            await _provider.ReloadAsync(cancellationToken);
            return Ok(new { message = "Docs reloaded" });
        }
    }
}
