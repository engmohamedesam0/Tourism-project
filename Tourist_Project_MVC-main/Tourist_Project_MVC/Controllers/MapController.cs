using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Tourist_Project_MVC.Services;

namespace Tourist_Project_MVC.Controllers
{
    public class MapController : Controller
    {
        private readonly IConfiguration _config;
        private readonly IArcGisAppTokenService _tokenService;

        public MapController(IConfiguration config, IArcGisAppTokenService tokenService)
        {
            _config = config;
            _tokenService = tokenService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMapConfig()
        {
            string token = string.Empty;
            try
            {
                token = await _tokenService.GetAccessTokenAsync();
            }
            catch
            {
                // Fallback to static API key if OAuth is not configured or fails
                token = _config["ArcGIS:ApiKey"] ?? string.Empty;
            }

            return Json(new
            {
                apiKey = token,
                destinationsLayerUrl = _config["ArcGIS:DestinationsLayerUrl"] ?? string.Empty,
                branchesLayerUrl = _config["ArcGIS:BranchesLayerUrl"] ?? string.Empty
            });
        }
    }
}
