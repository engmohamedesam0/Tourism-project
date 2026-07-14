using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;

namespace Tourist_Project_MVC.Controllers
{
    public class MapController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IConfiguration _config;
        private const string EmptyFeatureCollection = "{\"type\":\"FeatureCollection\",\"features\":[]}";

        public MapController(IHttpClientFactory clientFactory, IConfiguration config)
        {
            _clientFactory = clientFactory;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> GetDestinationsGeoJson()
        {
            return await FetchWfsAsync(_config["GeoServer:DestinationsTypeName"]!);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranchesGeoJson()
        {
            return await FetchWfsAsync(_config["GeoServer:BranchesTypeName"]!);
        }

        private async Task<IActionResult> FetchWfsAsync(string typeName)
        {
            var baseUrl = _config["GeoServer:BaseUrl"] ?? string.Empty;
            var maxFeatures = _config["GeoServer:MaxFeatures"] ?? "50";

            var url = $"{baseUrl}?service=WFS&version=1.0.0&request=GetFeature"
                + $"&typeName={Uri.EscapeDataString(typeName)}"
                + $"&outputFormat=application/json&maxFeatures={maxFeatures}";

            try
            {
                var client = _clientFactory.CreateClient();
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return Content(EmptyFeatureCollection, "application/json");

                var text = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(text) || !text.Contains("\"type\""))
                    return Content(EmptyFeatureCollection, "application/json");

                return Content(text, "application/json");
            }
            catch
            {
                return Content(EmptyFeatureCollection, "application/json");
            }
        }
    }
}
