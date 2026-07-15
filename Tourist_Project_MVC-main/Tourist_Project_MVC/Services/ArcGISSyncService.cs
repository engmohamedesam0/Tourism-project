using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Services;

public interface IArcGISSyncService
{
    Task SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default);
    Task SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default);
}

public class ArcGISSyncService : IArcGISSyncService, IAsyncDisposable
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ArcGISSyncService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ArcGISSyncService(IHttpClientFactory clientFactory, IConfiguration config, ILogger<ArcGISSyncService> logger)
    {
        _clientFactory = clientFactory;
        _config = config;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    private string? ApiKey => _config["ArcGIS:ApiKey"];
    private string? DestinationsLayerUrl => _config["ArcGIS:DestinationsLayerUrl"];
    private string? BranchesLayerUrl => _config["ArcGIS:BranchesLayerUrl"];

    public async Task SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default)
    {
        var layerUrl = DestinationsLayerUrl;
        if (string.IsNullOrWhiteSpace(layerUrl) || string.IsNullOrWhiteSpace(ApiKey)) return;

        var list = destinations.ToList();
        if (!list.Any()) return;

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-ESRI-Authorization", ApiKey);

            var adds = list
                .Where(d => d.Location != null)
                .Select(d => new
                {
                    attributes = new
                    {
                        id = d.Id,
                        name = d.Name,
                        city = d.City,
                        category = d.Category ?? "",
                        description = d.Description ?? "",
                        ticket_price = d.TicketPrice ?? 0m,
                        rating = d.Rating ?? 0m,
                        tags = d.Tags ?? "",
                        visits = d.Visits,
                        status = d.Status
                    },
                    geometry = new
                    {
                        x = d.Location.X,
                        y = d.Location.Y,
                        spatialReference = new { wkid = 4326 }
                    }
                })
                .ToArray();

            if (adds.Length == 0) return;

            var payload = new { adds };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{layerUrl}/applyEdits?f=json&token={Uri.EscapeDataString(ApiKey)}";
            var response = await client.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ArcGIS destinations sync failed with status {Status}", response.StatusCode);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("addResults", out var addsResults))
            {
                foreach (var result in addsResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        _logger.LogWarning("ArcGIS destination add failed: {Error}", result.GetRawText());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArcGIS destinations sync failed");
        }
    }

    public async Task SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default)
    {
        var layerUrl = BranchesLayerUrl;
        if (string.IsNullOrWhiteSpace(layerUrl) || string.IsNullOrWhiteSpace(ApiKey)) return;

        var list = branches.ToList();
        if (!list.Any()) return;

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-ESRI-Authorization", ApiKey);

            var adds = list
                .Where(b => b.Location != null)
                .Select(b => new
                {
                    attributes = new
                    {
                        id = b.Id,
                        name = b.Name,
                        address = b.Address,
                        contact_number = b.ContactNumber ?? 0,
                        sponsor_id = b.SponsorId
                    },
                    geometry = new
                    {
                        x = b.Location.X,
                        y = b.Location.Y,
                        spatialReference = new { wkid = 4326 }
                    }
                })
                .ToArray();

            if (adds.Length == 0) return;

            var payload = new { adds };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{layerUrl}/applyEdits?f=json&token={Uri.EscapeDataString(ApiKey)}";
            var response = await client.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ArcGIS branches sync failed with status {Status}", response.StatusCode);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("addResults", out var addsResults))
            {
                foreach (var result in addsResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        _logger.LogWarning("ArcGIS branch add failed: {Error}", result.GetRawText());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ArcGIS branches sync failed");
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
