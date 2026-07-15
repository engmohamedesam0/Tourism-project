using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
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

    private static string LayerUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/0")) return trimmed;
        return trimmed + "/0";
    }

    private async Task<int?> QueryObjectIdAsync(HttpClient client, string layerUrl, int id, string token, CancellationToken ct)
    {
        var queryUrl = $"{layerUrl}/query?where=Id={id}&f=json&token={Uri.EscapeDataString(token)}&outFields=OBJECTID&returnGeometry=false";
        using var response = await client.GetAsync(queryUrl, ct);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("features", out var features) && features.GetArrayLength() > 0)
        {
            var first = features[0];
            if (first.TryGetProperty("attributes", out var attrs) && attrs.TryGetProperty("OBJECTID", out var oid))
            {
                return oid.GetInt32();
            }
        }
        return null;
    }

    public async Task SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default)
    {
        var layerUrl = LayerUrl(DestinationsLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl) || string.IsNullOrWhiteSpace(ApiKey)) return;

        var list = destinations.ToList();
        if (!list.Any()) return;

        try
        {
            var client = _clientFactory.CreateClient();
            var adds = new List<object>();
            var updates = new List<object>();

            foreach (var d in list.Where(x => x.Location != null))
            {
                var existingOid = await QueryObjectIdAsync(client, layerUrl, d.Id, ApiKey, ct);
                var attrs = new
                {
                    Id = d.Id,
                    Name = d.Name,
                    City = d.City,
                    Category = d.Category ?? "",
                    TicketPrice = d.TicketPrice ?? 0m,
                    Rating = d.Rating ?? 0m,
                    Visits = d.Visits,
                    Status = d.Status,
                    latitude = d.Location.Y,
                    longitude = d.Location.X
                };
                var geometry = new
                {
                    x = d.Location.X,
                    y = d.Location.Y,
                    spatialReference = new { wkid = 4326 }
                };
                var feature = new { attributes = attrs, geometry = geometry };

                if (existingOid.HasValue)
                {
                    updates.Add(new
                    {
                        attributes = new Dictionary<string, object>
                        {
                            ["OBJECTID"] = existingOid.Value,
                            ["Id"] = d.Id,
                            ["Name"] = d.Name,
                            ["City"] = d.City,
                            ["Category"] = d.Category ?? "",
                            ["TicketPrice"] = d.TicketPrice ?? 0m,
                            ["Rating"] = d.Rating ?? 0m,
                            ["Visits"] = d.Visits,
                            ["Status"] = d.Status,
                            ["latitude"] = d.Location.Y,
                            ["longitude"] = d.Location.X
                        },
                        geometry = geometry
                    });
                }
                else
                {
                    adds.Add(feature);
                }
            }

            if (adds.Count == 0 && updates.Count == 0) return;

            var payload = new Dictionary<string, object>();
            if (adds.Count > 0) payload["adds"] = adds;
            if (updates.Count > 0) payload["updates"] = updates;

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
            if (doc.RootElement.TryGetProperty("addResults", out var addResults))
            {
                foreach (var result in addResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        _logger.LogWarning("ArcGIS destination add failed: {Error}", result.GetRawText());
                    }
                }
            }
            if (doc.RootElement.TryGetProperty("updateResults", out var updateResults))
            {
                foreach (var result in updateResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        _logger.LogWarning("ArcGIS destination update failed: {Error}", result.GetRawText());
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
        var layerUrl = LayerUrl(BranchesLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl) || string.IsNullOrWhiteSpace(ApiKey)) return;

        var list = branches.ToList();
        if (!list.Any()) return;

        try
        {
            var client = _clientFactory.CreateClient();
            var adds = new List<object>();
            var updates = new List<object>();

            foreach (var b in list.Where(x => x.Location != null))
            {
                var existingOid = await QueryObjectIdAsync(client, layerUrl, b.Id, ApiKey, ct);
                var geometry = new
                {
                    x = b.Location.X,
                    y = b.Location.Y,
                    spatialReference = new { wkid = 4326 }
                };

                if (existingOid.HasValue)
                {
                    updates.Add(new
                    {
                        attributes = new Dictionary<string, object>
                        {
                            ["OBJECTID"] = existingOid.Value,
                            ["Id"] = b.Id,
                            ["SponsorId"] = b.SponsorId,
                            ["Name"] = b.Name,
                            ["Address"] = b.Address,
                            ["ContactNumber"] = b.ContactNumber ?? 0,
                            ["latitude"] = b.Location.Y,
                            ["longitude"] = b.Location.X
                        },
                        geometry = geometry
                    });
                }
                else
                {
                    adds.Add(new
                    {
                        attributes = new
                        {
                            Id = b.Id,
                            SponsorId = b.SponsorId,
                            Name = b.Name,
                            Address = b.Address,
                            ContactNumber = b.ContactNumber ?? 0,
                            latitude = b.Location.Y,
                            longitude = b.Location.X
                        },
                        geometry = geometry
                    });
                }
            }

            if (adds.Count == 0 && updates.Count == 0) return;

            var payload = new Dictionary<string, object>();
            if (adds.Count > 0) payload["adds"] = adds;
            if (updates.Count > 0) payload["updates"] = updates;

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
            if (doc.RootElement.TryGetProperty("addResults", out var addResults))
            {
                foreach (var result in addResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        _logger.LogWarning("ArcGIS branch add failed: {Error}", result.GetRawText());
                    }
                }
            }
            if (doc.RootElement.TryGetProperty("updateResults", out var updateResults))
            {
                foreach (var result in updateResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        _logger.LogWarning("ArcGIS branch update failed: {Error}", result.GetRawText());
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
