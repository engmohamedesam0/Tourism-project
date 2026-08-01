using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Tourist_Project_MVC.Controllers
{
    public class MapController : Controller
    {
        private readonly IConfiguration _config;

        public MapController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult GetMapConfig()
        {
            var token = _config["ArcGIS:ApiKey"] ?? string.Empty;

            return Json(new
            {
                apiKey = token,
                destinationsLayerUrl = _config["ArcGIS:DestinationsLayerUrl"] ?? string.Empty,
                branchesLayerUrl = _config["ArcGIS:BranchesLayerUrl"] ?? string.Empty,
                portalId = _config["ArcGIS:PortalId"] ?? string.Empty
            });
        }
    }
}
