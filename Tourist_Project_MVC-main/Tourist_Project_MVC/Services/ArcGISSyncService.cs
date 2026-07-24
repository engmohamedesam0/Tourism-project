using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Services;

namespace Tourist_Project_MVC.Services;

public interface IArcGISSyncService
{
    Task<ArcGISSyncResult> SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default);
}

public record ArcGISSyncResult(bool Success, string? Error, int AddedCount, int UpdatedCount)
{
    public static ArcGISSyncResult Ok(int added = 0, int updated = 0) => new(true, null, added, updated);
    public static ArcGISSyncResult Failed(string error, int added = 0, int updated = 0) => new(false, error, added, updated);
}

public class ArcGISSyncService : IArcGISSyncService, IAsyncDisposable
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ArcGISSyncService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IArcGisAppTokenService _tokenService;
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _fieldCache = new();
    private static readonly SemaphoreSlim _fieldCacheLock = new(1, 1);

    public ArcGISSyncService(IHttpClientFactory clientFactory, IConfiguration config, ILogger<ArcGISSyncService> logger, IArcGisAppTokenService tokenService)
    {
        _clientFactory = clientFactory;
        _config = config;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _tokenService = tokenService;
    }

    private string? DestinationsLayerUrl => _config["ArcGIS:DestinationsLayerUrl"];
    private string? BranchesLayerUrl => _config["ArcGIS:BranchesLayerUrl"];

    private static string LayerUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/0")) return trimmed;
        return trimmed + "/0";
    }

    private static string? ResolveField(Dictionary<string, string>? map, string logicalName)
    {
        if (map == null) return logicalName;
        return map.TryGetValue(logicalName, out var real) ? real : logicalName;
    }

    private async Task<Dictionary<string, string>?> GetFieldMapAsync(HttpClient client, string layerUrl, string token, CancellationToken ct)
    {
        if (_fieldCache.TryGetValue(layerUrl, out var cached)) return cached;

        await _fieldCacheLock.WaitAsync(ct);
        try
        {
            if (_fieldCache.TryGetValue(layerUrl, out cached)) return cached;

            var schemaUrl = $"{layerUrl}?f=json&token={Uri.EscapeDataString(token)}";
            using var schemaResponse = await client.GetAsync(schemaUrl, ct);
            if (!schemaResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("ArcGIS field schema fetch failed for {LayerUrl} with status {Status}", layerUrl, schemaResponse.StatusCode);
                return null;
            }

            var schemaBody = await schemaResponse.Content.ReadAsStringAsync(ct);
            using var schemaDoc = JsonDocument.Parse(schemaBody);
            if (!schemaDoc.RootElement.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("ArcGIS field schema missing 'fields' array for {LayerUrl}", layerUrl);
                return null;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields.EnumerateArray())
            {
                if (field.TryGetProperty("name", out var nameEl))
                {
                    var name = nameEl.GetString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        map[name] = name;
                    }
                }
            }

            _logger.LogInformation("ArcGIS field schema for {LayerUrl}: {FieldCount} fields ({Fields})", layerUrl, map.Count, string.Join(", ", map.Keys));
            _fieldCache[layerUrl] = map;
            return map;
        }
        finally
        {
            _fieldCacheLock.Release();
        }
    }

    private async Task<int?> QueryObjectIdAsync(HttpClient client, string layerUrl, int id, string token, string idFieldName, CancellationToken ct)
    {
        var queryUrl = $"{layerUrl}/query?where={Uri.EscapeDataString(idFieldName)}={id}&f=json&token={Uri.EscapeDataString(token)}&outFields=OBJECTID&returnGeometry=false";
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

    private static string ExtractArcGISErrorMessage(JsonElement resultElement)
    {
        if (resultElement.TryGetProperty("error", out var err))
        {
            if (err.TryGetProperty("description", out var desc) && desc.GetString() is string d) return d;
            if (err.TryGetProperty("message", out var msg) && msg.GetString() is string m) return m;
            return err.GetRawText();
        }
        return resultElement.GetRawText();
    }

    public async Task<ArcGISSyncResult> SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default)
    {
        var layerUrl = LayerUrl(DestinationsLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        var list = destinations.ToList();
        if (!list.Any()) return ArcGISSyncResult.Ok();

        string token;
        try
        {
            token = await _tokenService.GetAccessTokenAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS destinations sync skipped: unable to acquire access token");
            return ArcGISSyncResult.Failed("Unable to acquire ArcGIS access token.");
        }

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");
            var adds = new List<object>();
            var updates = new List<object>();
            var addsTargetOids = new List<int>();
            var updatesTargetOids = new List<int>();

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var idField = ResolveField(fieldMap, "Id") ?? "Id";

            foreach (var d in list.Where(x => x.Location != null))
            {
                var existingOid = await QueryObjectIdAsync(client, layerUrl, d.Id, token, idField, ct);
                var attrs = new Dictionary<string, object>
                {
                    [ResolveField(fieldMap, "Id") ?? "Id"] = d.Id,
                    [ResolveField(fieldMap, "Name") ?? "Name"] = d.Name,
                    [ResolveField(fieldMap, "City") ?? "City"] = d.City,
                    [ResolveField(fieldMap, "Category") ?? "Category"] = d.Category ?? "",
                    [ResolveField(fieldMap, "TicketPrice") ?? "TicketPrice"] = d.TicketPrice ?? 0m,
                    [ResolveField(fieldMap, "Rating") ?? "Rating"] = d.Rating ?? 0m,
                    [ResolveField(fieldMap, "Visits") ?? "Visits"] = d.Visits,
                    [ResolveField(fieldMap, "Status") ?? "Status"] = d.Status,
                    [ResolveField(fieldMap, "latitude") ?? "latitude"] = d.Location.Y,
                    [ResolveField(fieldMap, "longitude") ?? "longitude"] = d.Location.X
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
                            [ResolveField(fieldMap, "Id") ?? "Id"] = d.Id,
                            [ResolveField(fieldMap, "Name") ?? "Name"] = d.Name,
                            [ResolveField(fieldMap, "City") ?? "City"] = d.City,
                            [ResolveField(fieldMap, "Category") ?? "Category"] = d.Category ?? "",
                            [ResolveField(fieldMap, "TicketPrice") ?? "TicketPrice"] = d.TicketPrice ?? 0m,
                            [ResolveField(fieldMap, "Rating") ?? "Rating"] = d.Rating ?? 0m,
                            [ResolveField(fieldMap, "Visits") ?? "Visits"] = d.Visits,
                            [ResolveField(fieldMap, "Status") ?? "Status"] = d.Status,
                            [ResolveField(fieldMap, "latitude") ?? "latitude"] = d.Location.Y,
                            [ResolveField(fieldMap, "longitude") ?? "longitude"] = d.Location.X
                        },
                        geometry = geometry
                    });
                    updatesTargetOids.Add(existingOid.Value);
                }
                else
                {
                    adds.Add(feature);
                    addsTargetOids.Add(d.Id);
                }
            }

            if (adds.Count == 0 && updates.Count == 0) return ArcGISSyncResult.Ok();

            var formFields = new Dictionary<string, string>
            {
                ["f"] = "json"
            };
            if (adds.Count > 0)
                formFields["adds"] = JsonSerializer.Serialize(adds, _jsonOptions);
            if (updates.Count > 0)
                formFields["updates"] = JsonSerializer.Serialize(updates, _jsonOptions);

            var content = new FormUrlEncodedContent(formFields);

            var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
            var response = await client.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("ArcGIS destinations sync failed with HTTP status {Status}. Body: {Body}", response.StatusCode, errBody);
                return ArcGISSyncResult.Failed($"ArcGIS returned HTTP {(int)response.StatusCode}: {errBody}");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("ArcGIS applyEdits response: {Body}", body);

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var errMsg = ExtractArcGISErrorMessage(error);
                _logger.LogError("ArcGIS applyEdits returned top-level error: {Error}", errMsg);
                return ArcGISSyncResult.Failed($"ArcGIS applyEdits error: {errMsg}");
            }

            if (doc.RootElement.TryGetProperty("addResults", out var addResults))
            {
                int i = 0;
                foreach (var result in addResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        var errMsg = ExtractArcGISErrorMessage(result);
                        var targetId = i < addsTargetOids.Count ? addsTargetOids[i] : -1;
                        _logger.LogError("ArcGIS destination add failed for Id={TargetId}: {Error}", targetId, errMsg);
                        return ArcGISSyncResult.Failed($"ArcGIS destination add failed for Id={targetId}: {errMsg}");
                    }
                    i++;
                }
            }

            if (doc.RootElement.TryGetProperty("updateResults", out var updateResults))
            {
                int i = 0;
                foreach (var result in updateResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        var errMsg = ExtractArcGISErrorMessage(result);
                        var targetOid = i < updatesTargetOids.Count ? updatesTargetOids[i] : -1;
                        _logger.LogError("ArcGIS destination update failed for OBJECTID={TargetOid}: {Error}", targetOid, errMsg);
                        return ArcGISSyncResult.Failed($"ArcGIS destination update failed for OBJECTID={targetOid}: {errMsg}");
                    }
                    i++;
                }
            }

            return ArcGISSyncResult.Ok(added: adds.Count, updated: updates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS destinations sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS destinations sync failed: {ex.Message}");
        }
    }

    public async Task<ArcGISSyncResult> SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default)
    {
        var layerUrl = LayerUrl(BranchesLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        var list = branches.ToList();
        if (!list.Any()) return ArcGISSyncResult.Ok();

        string token;
        try
        {
            token = await _tokenService.GetAccessTokenAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS branches sync skipped: unable to acquire access token");
            return ArcGISSyncResult.Failed("Unable to acquire ArcGIS access token.");
        }

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");
            var adds = new List<object>();
            var updates = new List<object>();
            var addsTargetOids = new List<int>();
            var updatesTargetOids = new List<int>();

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var idField = ResolveField(fieldMap, "Id") ?? "Id";

            foreach (var b in list.Where(x => x.Location != null))
            {
                var existingOid = await QueryObjectIdAsync(client, layerUrl, b.Id, token, idField, ct);
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
                            [ResolveField(fieldMap, "Id") ?? "Id"] = b.Id,
                            [ResolveField(fieldMap, "SponsorId") ?? "SponsorId"] = b.SponsorId,
                            [ResolveField(fieldMap, "Name") ?? "Name"] = b.Name,
                            [ResolveField(fieldMap, "Address") ?? "Address"] = b.Address,
                            [ResolveField(fieldMap, "ContactNumber") ?? "ContactNumber"] = b.ContactNumber ?? 0,
                            [ResolveField(fieldMap, "latitude") ?? "latitude"] = b.Location.Y,
                            [ResolveField(fieldMap, "longitude") ?? "longitude"] = b.Location.X
                        },
                        geometry = geometry
                    });
                    updatesTargetOids.Add(existingOid.Value);
                }
                else
                {
                    adds.Add(new
                    {
                        attributes = new Dictionary<string, object>
                        {
                            [ResolveField(fieldMap, "Id") ?? "Id"] = b.Id,
                            [ResolveField(fieldMap, "SponsorId") ?? "SponsorId"] = b.SponsorId,
                            [ResolveField(fieldMap, "Name") ?? "Name"] = b.Name,
                            [ResolveField(fieldMap, "Address") ?? "Address"] = b.Address,
                            [ResolveField(fieldMap, "ContactNumber") ?? "ContactNumber"] = b.ContactNumber ?? 0,
                            [ResolveField(fieldMap, "latitude") ?? "latitude"] = b.Location.Y,
                            [ResolveField(fieldMap, "longitude") ?? "longitude"] = b.Location.X
                        },
                        geometry = geometry
                    });
                    addsTargetOids.Add(b.Id);
                }
            }

            if (adds.Count == 0 && updates.Count == 0) return ArcGISSyncResult.Ok();

            var formFields = new Dictionary<string, string>
            {
                ["f"] = "json"
            };
            if (adds.Count > 0)
                formFields["adds"] = JsonSerializer.Serialize(adds, _jsonOptions);
            if (updates.Count > 0)
                formFields["updates"] = JsonSerializer.Serialize(updates, _jsonOptions);

            var content = new FormUrlEncodedContent(formFields);

            var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
            var response = await client.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("ArcGIS branches sync failed with HTTP status {Status}. Body: {Body}", response.StatusCode, errBody);
                return ArcGISSyncResult.Failed($"ArcGIS returned HTTP {(int)response.StatusCode}: {errBody}");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("ArcGIS applyEdits response: {Body}", body);

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var errMsg = ExtractArcGISErrorMessage(error);
                _logger.LogError("ArcGIS applyEdits returned top-level error: {Error}", errMsg);
                return ArcGISSyncResult.Failed($"ArcGIS applyEdits error: {errMsg}");
            }

            if (doc.RootElement.TryGetProperty("addResults", out var addResults))
            {
                int i = 0;
                foreach (var result in addResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        var errMsg = ExtractArcGISErrorMessage(result);
                        var targetId = i < addsTargetOids.Count ? addsTargetOids[i] : -1;
                        _logger.LogError("ArcGIS branch add failed for Id={TargetId}: {Error}", targetId, errMsg);
                        return ArcGISSyncResult.Failed($"ArcGIS branch add failed for Id={targetId}: {errMsg}");
                    }
                    i++;
                }
            }

            if (doc.RootElement.TryGetProperty("updateResults", out var updateResults))
            {
                int i = 0;
                foreach (var result in updateResults.EnumerateArray())
                {
                    if (result.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
                    {
                        var errMsg = ExtractArcGISErrorMessage(result);
                        var targetOid = i < updatesTargetOids.Count ? updatesTargetOids[i] : -1;
                        _logger.LogError("ArcGIS branch update failed for OBJECTID={TargetOid}: {Error}", targetOid, errMsg);
                        return ArcGISSyncResult.Failed($"ArcGIS branch update failed for OBJECTID={targetOid}: {errMsg}");
                    }
                    i++;
                }
            }

            return ArcGISSyncResult.Ok(added: adds.Count, updated: updates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS branches sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS branches sync failed: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
