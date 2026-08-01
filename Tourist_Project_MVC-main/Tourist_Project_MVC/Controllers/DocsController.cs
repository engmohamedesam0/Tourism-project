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

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var sections = await _provider.GetSectionsAsync(cancellationToken);
            return View("Landing", sections);
        }

        [HttpGet("{section}/{slug}")]
        public async Task<IActionResult> Article(string section, string slug, CancellationToken cancellationToken)
        {
            var article = await _provider.GetArticleAsync(section, slug, cancellationToken);
            if (article == null)
                return NotFound();

            var sections = await _provider.GetSectionsAsync(cancellationToken);
            var flat = sections.SelectMany(s => s.Articles).OrderBy(a => a.Order).ToList();
            var currentIndex = flat.FindIndex(a => a.Section.Equals(section, System.StringComparison.OrdinalIgnoreCase) && a.Slug.Equals(slug, System.StringComparison.OrdinalIgnoreCase));

            var vm = new View_Model.DocViewModel
            {
                Article = article,
                Sections = sections.ToList(),
                PrevArticle = currentIndex > 0 ? flat[currentIndex - 1] : null,
                NextArticle = currentIndex < flat.Count - 1 ? flat[currentIndex + 1] : null
            };

            return View("Article", vm);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(System.Array.Empty<DocSearchResult>());

            var results = await _provider.SearchAsync(q, cancellationToken);
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
