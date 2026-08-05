using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services;

public interface IArcGISSyncService
{
    Task<ArcGISSyncResult> SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncDestinationsFromArcGIS(CancellationToken ct = default);
    Task<(bool Success, string? Error, int? CreatedObjectId, int? CreatedId)> AddDestinationToArcGISAsync(Destination destination, CancellationToken ct = default);
    Task<ArcGISSyncResult> DeleteDestinationFromArcGISAsync(int destinationId, CancellationToken ct = default);
    Task<ArcGISSyncResult> UpdateDestinationOnArcGISAsync(Destination destination, CancellationToken ct = default);
    Task<ArcGISDestinationSnapshot> GetDestinationSnapshotAsync(int? databaseId = null, CancellationToken ct = default);
}

public record ArcGISSyncResult(bool Success, string? Error, int AddedCount, int UpdatedCount)
{
    public static ArcGISSyncResult Ok(int added = 0, int updated = 0) => new(true, null, added, updated);
    public static ArcGISSyncResult Failed(string error, int added = 0, int updated = 0) => new(false, error, added, updated);
}

public class ArcGISSyncService : IArcGISSyncService, IAsyncDisposable, IDisposable
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ArcGISSyncService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TouristContext _context;
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _fieldCache = new();
    private static readonly SemaphoreSlim _fieldCacheLock = new(1, 1);

    public ArcGISSyncService(IHttpClientFactory clientFactory, IConfiguration config, ILogger<ArcGISSyncService> logger, TouristContext context)
    {
        _clientFactory = clientFactory;
        _config = config;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _context = context;
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
        var queryUrl = $"{layerUrl}/query?where={Uri.EscapeDataString(idFieldName)}={id}&f=json&token={Uri.EscapeDataString(token)}&outFields=ObjectId&returnGeometry=false";
        using var response = await client.GetAsync(queryUrl, ct);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("features", out var features) && features.GetArrayLength() > 0)
        {
            var first = features[0];
            if (first.TryGetProperty("attributes", out var attrs))
            {
                foreach (var property in attrs.EnumerateObject())
                {
                    if (property.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number)
                        return property.Value.GetInt32();
                }
            }
        }
        return null;
    }

    private async Task<(bool CanCreate, string? Error)> EnsureLayerCanCreateAsync(HttpClient client, string layerUrl, string token, CancellationToken ct)
    {
        var metadataUrl = $"{layerUrl}?f=json&token={Uri.EscapeDataString(token)}";
        using var response = await client.GetAsync(metadataUrl, ct);
        if (!response.IsSuccessStatusCode)
            return (false, $"ArcGIS layer metadata returned HTTP {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("error", out var error))
            return (false, $"ArcGIS layer metadata error: {ExtractArcGISErrorMessage(error)}");

        var capabilities = doc.RootElement.TryGetProperty("capabilities", out var capabilitiesElement)
            ? capabilitiesElement.GetString() ?? string.Empty
            : string.Empty;
        var canCreate = capabilities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => value.Equals("Create", StringComparison.OrdinalIgnoreCase));
        return canCreate
            ? (true, null)
            : (false, "The configured ArcGIS layer does not currently allow feature creation.");
    }

    private async Task<int> GetNextDestinationIdAsync(HttpClient client, string layerUrl, string token, string idField, CancellationToken ct)
    {
        var queryUrl = $"{layerUrl}/query?where=1%3D1&f=json&token={Uri.EscapeDataString(token)}&outFields={Uri.EscapeDataString(idField)}&orderByFields={Uri.EscapeDataString(idField)}%20DESC&resultRecordCount=1&returnGeometry=false";
        using var response = await client.GetAsync(queryUrl, ct);
        if (!response.IsSuccessStatusCode) return 1;
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("features", out var features) && features.GetArrayLength() > 0 && features[0].TryGetProperty("attributes", out var attrs))
        {
            foreach (var property in attrs.EnumerateObject())
            {
                if (property.Name.Equals(idField, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var currentId))
                    return currentId + 1;
            }
        }
        return 1;
    }

    private static double WebMercatorX(double longitude) => longitude * 20037508.34 / 180d;

    private static double WebMercatorY(double latitude)
    {
        var clamped = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var radians = clamped * Math.PI / 180d;
        return Math.Log(Math.Tan(Math.PI / 4d + radians / 2d)) * 20037508.34 / Math.PI;
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

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS destinations sync skipped: API Key is missing");
            return ArcGISSyncResult.Failed("API Key is missing.");
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
                    [ResolveField(fieldMap, "English_Name") ?? "English_Name"] = d.Name,
                    [ResolveField(fieldMap, "Arabic_Name") ?? "Arabic_Name"] = d.ArabicName ?? "",
                    [ResolveField(fieldMap, "Governorate") ?? "Governorate"] = d.City,
                    [ResolveField(fieldMap, "Category") ?? "Category"] = d.Category ?? "",
                    [ResolveField(fieldMap, "Description") ?? "Description"] = d.Description ?? "",
                    [ResolveField(fieldMap, "Status") ?? "Status"] = d.Status,
                    [ResolveField(fieldMap, "Visits") ?? "Visits"] = d.Visits,
                    [ResolveField(fieldMap, "Rating") ?? "Rating"] = d.Rating ?? 0m,
                    [ResolveField(fieldMap, "Tags") ?? "Tags"] = d.Tags ?? "",
                    [ResolveField(fieldMap, "Images") ?? "Images"] = d.PhotoUrls?.Replace("\\n", "|") ?? "",
                    [ResolveField(fieldMap, "TicketRequired") ?? "TicketRequired"] = d.TicketRequired ?? "No",
                    [ResolveField(fieldMap, "ForeignPrice") ?? "ForeignPrice"] = d.ForeignPrice ?? 0,
                    [ResolveField(fieldMap, "StudentForeignPrice") ?? "StudentForeignPrice"] = d.StudentForeignPrice ?? 0,
                    [ResolveField(fieldMap, "EgyptianPrice") ?? "EgyptianPrice"] = d.EgyptianPrice ?? 0,
                    [ResolveField(fieldMap, "StudentEgyptianPrice") ?? "StudentEgyptianPrice"] = d.StudentEgyptianPrice ?? 0,
                    [ResolveField(fieldMap, "Days") ?? "Days"] = d.Days ?? "",
                    [ResolveField(fieldMap, "Open_at") ?? "Open_at"] = d.OpenAt ?? 0,
                    [ResolveField(fieldMap, "Close_at") ?? "Close_at"] = d.CloseAt ?? 0,
                    [ResolveField(fieldMap, "Booking") ?? "Booking"] = d.Booking ?? "",
                    [ResolveField(fieldMap, "Latitiude") ?? "Latitiude"] = d.Location.Y,
                    [ResolveField(fieldMap, "Longitude") ?? "Longitude"] = d.Location.X
                };
                var geometry = new
                {
                    x = WebMercatorX(d.Location.X),
                    y = WebMercatorY(d.Location.Y),
                    spatialReference = new { wkid = 102100 }
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
                            [ResolveField(fieldMap, "English_Name") ?? "English_Name"] = d.Name,
                            [ResolveField(fieldMap, "Arabic_Name") ?? "Arabic_Name"] = d.ArabicName ?? "",
                            [ResolveField(fieldMap, "Governorate") ?? "Governorate"] = d.City,
                            [ResolveField(fieldMap, "Category") ?? "Category"] = d.Category ?? "",
                            [ResolveField(fieldMap, "Description") ?? "Description"] = d.Description ?? "",
                            [ResolveField(fieldMap, "Status") ?? "Status"] = d.Status,
                            [ResolveField(fieldMap, "Visits") ?? "Visits"] = d.Visits,
                            [ResolveField(fieldMap, "Rating") ?? "Rating"] = d.Rating ?? 0m,
                            [ResolveField(fieldMap, "Tags") ?? "Tags"] = d.Tags ?? "",
                            [ResolveField(fieldMap, "Images") ?? "Images"] = d.PhotoUrls?.Replace("\\n", "|") ?? "",
                            [ResolveField(fieldMap, "TicketRequired") ?? "TicketRequired"] = d.TicketRequired ?? "No",
                            [ResolveField(fieldMap, "ForeignPrice") ?? "ForeignPrice"] = d.ForeignPrice ?? 0,
                            [ResolveField(fieldMap, "StudentForeignPrice") ?? "StudentForeignPrice"] = d.StudentForeignPrice ?? 0,
                            [ResolveField(fieldMap, "EgyptianPrice") ?? "EgyptianPrice"] = d.EgyptianPrice ?? 0,
                            [ResolveField(fieldMap, "StudentEgyptianPrice") ?? "StudentEgyptianPrice"] = d.StudentEgyptianPrice ?? 0,
                            [ResolveField(fieldMap, "Days") ?? "Days"] = d.Days ?? "",
                            [ResolveField(fieldMap, "Open_at") ?? "Open_at"] = d.OpenAt ?? 0,
                            [ResolveField(fieldMap, "Close_at") ?? "Close_at"] = d.CloseAt ?? 0,
                            [ResolveField(fieldMap, "Booking") ?? "Booking"] = d.Booking ?? "",
                            [ResolveField(fieldMap, "Latitiude") ?? "Latitiude"] = d.Location.Y,
                            [ResolveField(fieldMap, "Longitude") ?? "Longitude"] = d.Location.X
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

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS branches sync skipped: API Key is missing");
            return ArcGISSyncResult.Failed("API Key is missing.");
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

    public async Task<ArcGISDestinationSnapshot> GetDestinationSnapshotAsync(int? databaseId = null, CancellationToken ct = default)
    {
        var layerUrl = LayerUrl(DestinationsLayerUrl);
        var token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(layerUrl) || string.IsNullOrWhiteSpace(token))
            return new(Array.Empty<ArcGISFieldDefinition>(), Array.Empty<ArcGISDestinationRecord>(), "ArcGIS destination layer is not configured.");

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");
            using var metadataResponse = await client.GetAsync($"{layerUrl}?f=json&token={Uri.EscapeDataString(token)}", ct);
            if (!metadataResponse.IsSuccessStatusCode)
                return new(Array.Empty<ArcGISFieldDefinition>(), Array.Empty<ArcGISDestinationRecord>(), $"ArcGIS schema returned HTTP {(int)metadataResponse.StatusCode}.");
            using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(ct));
            if (metadata.RootElement.TryGetProperty("error", out var metadataError))
                return new(Array.Empty<ArcGISFieldDefinition>(), Array.Empty<ArcGISDestinationRecord>(), ExtractArcGISErrorMessage(metadataError));

            var fields = new List<ArcGISFieldDefinition>();
            if (metadata.RootElement.TryGetProperty("fields", out var fieldArray))
            {
                foreach (var field in fieldArray.EnumerateArray())
                {
                    var name = field.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    fields.Add(new(
                        name,
                        field.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "" : "",
                        field.TryGetProperty("alias", out var aliasElement) ? aliasElement.GetString() ?? name : name,
                        !field.TryGetProperty("nullable", out var nullableElement) || nullableElement.GetBoolean(),
                        !field.TryGetProperty("editable", out var editableElement) || editableElement.GetBoolean()));
                }
            }

            var records = new List<ArcGISDestinationRecord>();
            var where = databaseId.HasValue ? $"Id={databaseId.Value}" : "1=1";
            async Task<(bool Success, bool More, string? Error)> ReadPageAsync(string pageWhere, int offset)
            {
                var queryUrl = $"{layerUrl}/query?where={Uri.EscapeDataString(pageWhere)}&outFields=*&returnGeometry=true&resultOffset={offset}&resultRecordCount=1000&f=json&token={Uri.EscapeDataString(token)}";
                using var response = await client.GetAsync(queryUrl, ct);
                if (!response.IsSuccessStatusCode) return (false, false, $"ArcGIS query returned HTTP {(int)response.StatusCode}.");
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (document.RootElement.TryGetProperty("error", out var queryError)) return (false, false, ExtractArcGISErrorMessage(queryError));
                if (!document.RootElement.TryGetProperty("features", out var features)) return (true, false, null);
                foreach (var feature in features.EnumerateArray())
                {
                    var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    if (feature.TryGetProperty("attributes", out var attributesElement))
                    {
                        foreach (var property in attributesElement.EnumerateObject())
                            attributes[property.Name] = property.Value.ValueKind switch
                            {
                                JsonValueKind.Null => null,
                                JsonValueKind.String => property.Value.GetString(),
                                JsonValueKind.Number when property.Value.TryGetInt64(out var integer) => integer,
                                JsonValueKind.Number when property.Value.TryGetDouble(out var number) => number,
                                JsonValueKind.True or JsonValueKind.False => property.Value.GetBoolean(),
                                _ => property.Value.GetRawText()
                            };
                    }
                    int? objectId = attributes.TryGetValue("ObjectId", out var objectValue) ? ConvertNullableInt(objectValue) : null;
                    int? id = attributes.TryGetValue("Id", out var idValue) ? ConvertNullableInt(idValue) : null;
                    double? latitude = attributes.TryGetValue("Latitiude", out var latitudeValue) ? ConvertNullableDouble(latitudeValue) : null;
                    double? longitude = attributes.TryGetValue("Longitude", out var longitudeValue) ? ConvertNullableDouble(longitudeValue) : null;
                    if ((!latitude.HasValue || !longitude.HasValue) && feature.TryGetProperty("geometry", out var geometry))
                    {
                        var x = geometry.TryGetProperty("x", out var xElement) ? xElement.GetDouble() : 0;
                        var y = geometry.TryGetProperty("y", out var yElement) ? yElement.GetDouble() : 0;
                        longitude = WebMercatorToLongitude(x);
                        latitude = WebMercatorToLatitude(y);
                    }
                    records.Add(new(objectId, id, attributes, latitude, longitude));
                }
                var more = document.RootElement.TryGetProperty("exceededTransferLimit", out var exceeded) && exceeded.ValueKind == JsonValueKind.True;
                return (true, more, null);
            }
            var offset = 0;
            while (true)
            {
                var pageResult = await ReadPageAsync(where, offset);
                if (!pageResult.Success) return new(fields, records, pageResult.Error);
                if (!pageResult.More || databaseId.HasValue) break;
                offset += 1000;
            }
            return new(fields, records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS destination snapshot failed");
            return new(Array.Empty<ArcGISFieldDefinition>(), Array.Empty<ArcGISDestinationRecord>(), ex.Message);
        }
    }

    private static int? ConvertNullableInt(object? value) => value switch { null => null, int integer => integer, long longValue => (int)longValue, double number => (int)number, _ when int.TryParse(value.ToString(), out var parsed) => parsed, _ => null };
    private static double? ConvertNullableDouble(object? value) => value switch { null => null, double number => number, float number => number, int integer => integer, long longValue => longValue, _ when double.TryParse(value.ToString(), out var parsed) => parsed, _ => null };
    private static double WebMercatorToLongitude(double x) => x / 20037508.34 * 180d;
    private static double WebMercatorToLatitude(double y) => 180d / Math.PI * (2d * Math.Atan(Math.Exp(y / 20037508.34 * Math.PI)) - Math.PI / 2d);

    public async Task<ArcGISSyncResult> SyncDestinationsFromArcGIS(CancellationToken ct = default)
    {
        var layerUrl = LayerUrl(DestinationsLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS destinations pull-sync skipped: API Key is missing");
            return ArcGISSyncResult.Failed("API Key is missing.");
        }

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            if (fieldMap == null)
            {
                return ArcGISSyncResult.Failed("Failed to fetch ArcGIS field schema for destinations.");
            }

            var queryUrl = $"{layerUrl}/query?where=1%3D1&outFields=*&returnGeometry=true&resultRecordCount=500&f=json&token={Uri.EscapeDataString(token)}";
            using var response = await client.GetAsync(queryUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("ArcGIS destinations query failed with HTTP status {Status}. Body: {Body}", response.StatusCode, errBody);
                return ArcGISSyncResult.Failed($"ArcGIS query returned HTTP {(int)response.StatusCode}: {errBody}");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var errMsg = ExtractArcGISErrorMessage(error);
                _logger.LogError("ArcGIS destinations query returned error: {Error}", errMsg);
                return ArcGISSyncResult.Failed($"ArcGIS query error: {errMsg}");
            }

            if (!doc.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
            {
                _logger.LogWarning("ArcGIS destinations query returned no features.");
                return ArcGISSyncResult.Ok();
            }

            var queryWasCapped = doc.RootElement.TryGetProperty("exceededTransferLimit", out var exceededTransferLimit)
                && exceededTransferLimit.ValueKind == JsonValueKind.True;
            var remoteIds = new HashSet<int>();
            var upserts = new List<Destination>();

            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("attributes", out var attrs)) continue;

                var dest = new Destination();

                if (attrs.TryGetProperty("Id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                    dest.Id = idEl.GetInt32();

                if (attrs.TryGetProperty("English_Name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                    dest.Name = nameEl.GetString() ?? string.Empty;

                if (attrs.TryGetProperty("Arabic_Name", out var arabicNameEl) && arabicNameEl.ValueKind == JsonValueKind.String)
                    dest.ArabicName = arabicNameEl.GetString();

                if (attrs.TryGetProperty("Governorate", out var govEl) && govEl.ValueKind == JsonValueKind.String)
                    dest.City = govEl.GetString() ?? string.Empty;

                if (attrs.TryGetProperty("Category", out var catEl) && catEl.ValueKind == JsonValueKind.String)
                    dest.Category = catEl.GetString();

                if (attrs.TryGetProperty("Description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                {
                    var raw = descEl.GetString();
                    dest.Description = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                if (attrs.TryGetProperty("Status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String)
                {
                    var rawStatus = statusEl.GetString() ?? "Active";
                    // ArcGIS stores "N/A" for some records; treat those as Active.
                    dest.Status = rawStatus is "N/A" or "" or "n/a" ? "Active" : rawStatus;
                }

                if (attrs.TryGetProperty("Visits", out var visitsEl) && visitsEl.ValueKind == JsonValueKind.Number)
                    dest.Visits = visitsEl.GetInt32();

                if (attrs.TryGetProperty("Rating", out var ratingEl) && ratingEl.ValueKind == JsonValueKind.Number)
                    dest.Rating = Math.Round((decimal)ratingEl.GetDouble(), 0, MidpointRounding.AwayFromZero);

                if (attrs.TryGetProperty("Tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.String)
                {
                    var raw = tagsEl.GetString();
                    dest.Tags = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                if (attrs.TryGetProperty("Images", out var imagesEl) && imagesEl.ValueKind == JsonValueKind.String)
                {
                    // ArcGIS stores image URLs separated by '|' — normalise to '\n'
                    // which is what PhotoUrlList expects for splitting.
                    var rawImages = imagesEl.GetString();
                    dest.PhotoUrls = string.IsNullOrWhiteSpace(rawImages)
                        ? null
                        : rawImages.Replace("|", "\n");
                }

                if (attrs.TryGetProperty("TicketRequired", out var ticketReqEl) && ticketReqEl.ValueKind == JsonValueKind.String)
                {
                    var raw = ticketReqEl.GetString();
                    dest.TicketRequired = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                if (attrs.TryGetProperty("ForeignPrice", out var fpEl) && fpEl.ValueKind == JsonValueKind.Number)
                    dest.ForeignPrice = fpEl.GetInt32();

                if (attrs.TryGetProperty("StudentForeignPrice", out var sfpEl) && sfpEl.ValueKind == JsonValueKind.Number)
                    dest.StudentForeignPrice = sfpEl.GetInt32();

                if (attrs.TryGetProperty("EgyptianPrice", out var epEl) && epEl.ValueKind == JsonValueKind.Number)
                    dest.EgyptianPrice = epEl.GetInt32();

                if (attrs.TryGetProperty("StudentEgyptianPrice", out var sepEl) && sepEl.ValueKind == JsonValueKind.Number)
                    dest.StudentEgyptianPrice = sepEl.GetInt32();

                if (attrs.TryGetProperty("Days", out var daysEl) && daysEl.ValueKind == JsonValueKind.String)
                {
                    var raw = daysEl.GetString();
                    dest.Days = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                if (attrs.TryGetProperty("Open_at", out var openEl) && openEl.ValueKind == JsonValueKind.Number)
                    dest.OpenAt = openEl.GetInt32();

                if (attrs.TryGetProperty("Close_at", out var closeEl) && closeEl.ValueKind == JsonValueKind.Number)
                    dest.CloseAt = closeEl.GetInt32();

                if (attrs.TryGetProperty("Booking", out var bookingEl) && bookingEl.ValueKind == JsonValueKind.String)
                {
                    var raw = bookingEl.GetString();
                    dest.Booking = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                double lat = 0, lng = 0;
                bool hasLat = attrs.TryGetProperty("Latitiude", out var latEl) && latEl.ValueKind == JsonValueKind.Number;
                bool hasLng = attrs.TryGetProperty("Longitude", out var lngEl) && lngEl.ValueKind == JsonValueKind.Number;
                if (hasLat) lat = latEl.GetDouble();
                if (hasLng) lng = lngEl.GetDouble();

                if (hasLat && hasLng)
                {
                    dest.Location = new Point(lng, lat) { SRID = 4326 };
                }

                remoteIds.Add(dest.Id);
                upserts.Add(dest);
            }

            var dbDestinations = _context.Destinations.ToList();
            var dbIds = new HashSet<int>(dbDestinations.Select(d => d.Id));

            var toRemove = queryWasCapped
                ? new List<Destination>()
                : dbDestinations.Where(d => !remoteIds.Contains(d.Id)).ToList();
            if (queryWasCapped)
            {
                _logger.LogWarning("ArcGIS destinations query exceeded the transfer limit; skipping local deletions to avoid removing records outside the returned page.");
            }
            foreach (var d in toRemove)
            {
                _context.Destinations.Remove(d);
                _logger.LogInformation("ArcGIS pull-sync removing local Destination Id={Id} (not in remote layer)", d.Id);
            }

            foreach (var remote in upserts)
            {
                var existing = dbDestinations.FirstOrDefault(d => d.Id == remote.Id);
                if (existing != null)
                {
                    existing.Name = remote.Name;
                    existing.ArabicName = remote.ArabicName;
                    existing.City = remote.City;
                    existing.Category = remote.Category;
                    existing.Description = remote.Description;
                    existing.Status = remote.Status;
                    existing.Visits = remote.Visits;
                    existing.Rating = remote.Rating;
                    existing.Tags = remote.Tags;
                    existing.PhotoUrls = remote.PhotoUrls;
                    existing.TicketRequired = remote.TicketRequired;
                    existing.ForeignPrice = remote.ForeignPrice;
                    existing.StudentForeignPrice = remote.StudentForeignPrice;
                    existing.EgyptianPrice = remote.EgyptianPrice;
                    existing.StudentEgyptianPrice = remote.StudentEgyptianPrice;
                    existing.Days = remote.Days;
                    existing.OpenAt = remote.OpenAt;
                    existing.CloseAt = remote.CloseAt;
                    existing.Booking = remote.Booking;
                    existing.Location = remote.Location;
                    _context.Destinations.Update(existing);
                }
                else
                {
                    _context.Destinations.Add(remote);
                    _logger.LogInformation("ArcGIS pull-sync adding new Destination Id={Id}: {Name}", remote.Id, remote.Name);
                }
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("ArcGIS destinations pull-sync complete: {Added} added/updated, {Removed} removed",
                upserts.Count, toRemove.Count);

            return ArcGISSyncResult.Ok(added: upserts.Count, updated: 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS destinations pull-sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS destinations pull-sync failed: {ex.Message}");
        }
    }

    public async Task<ArcGISSyncResult> UpdateDestinationOnArcGISAsync(Destination destination, CancellationToken ct = default)
    {
        var result = await SyncDestinationsAsync(new[] { destination }, ct);
        return result.Success ? result : ArcGISSyncResult.Failed(result.Error ?? "ArcGIS update failed.");
    }

    public async Task<ArcGISSyncResult> DeleteDestinationFromArcGISAsync(int destinationId, CancellationToken ct = default)
    {
        var layerUrl = LayerUrl(DestinationsLayerUrl);
        var token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(layerUrl) || string.IsNullOrWhiteSpace(token)) return ArcGISSyncResult.Failed("ArcGIS destination layer is not configured.");
        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");
            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var idField = ResolveField(fieldMap, "Id") ?? "Id";
            var objectId = await QueryObjectIdAsync(client, layerUrl, destinationId, token, idField, ct);
            if (!objectId.HasValue)
            {
                _logger.LogWarning("ArcGIS destination delete target was not found for database Id={DestinationId}", destinationId);
                return ArcGISSyncResult.Failed($"No ArcGIS feature was found for destination Id={destinationId}.");
            }
            var fields = new Dictionary<string, string> { ["f"] = "json", ["deletes"] = objectId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), ["rollbackOnFailure"] = "true" };
            using var response = await client.PostAsync($"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}", new FormUrlEncodedContent(fields), ct);
            if (!response.IsSuccessStatusCode) return ArcGISSyncResult.Failed($"ArcGIS returned HTTP {(int)response.StatusCode}.");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("error", out var error)) return ArcGISSyncResult.Failed($"ArcGIS delete failed: {ExtractArcGISErrorMessage(error)}");
            if (doc.RootElement.TryGetProperty("deleteResults", out var results) && results.GetArrayLength() > 0 && results[0].TryGetProperty("success", out var success) && success.GetBoolean()) return ArcGISSyncResult.Ok(updated: 1);
            return ArcGISSyncResult.Failed("ArcGIS did not confirm the destination deletion.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS destination deletion failed for Id={DestinationId}", destinationId);
            return ArcGISSyncResult.Failed($"ArcGIS deletion failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error, int? CreatedObjectId, int? CreatedId)> AddDestinationToArcGISAsync(Destination destination, CancellationToken ct = default)
    {
        var layerUrl = LayerUrl(DestinationsLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl))
            return (false, "DestinationsLayerUrl is not configured.", null, null);

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS add destination failed: API Key is missing");
            return (false, "ArcGIS API Key is missing in server configuration.", null, null);
        }

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");

            var layerCapability = await EnsureLayerCanCreateAsync(client, layerUrl, token, ct);
            if (!layerCapability.CanCreate)
                return (false, layerCapability.Error, null, null);

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            if (fieldMap == null)
                return (false, "ArcGIS destination field metadata could not be loaded.", null, null);

            var idField = ResolveField(fieldMap, "Id") ?? "Id";
            var nextId = await GetNextDestinationIdAsync(client, layerUrl, token, idField, ct);

            var attrs = new Dictionary<string, object?>
            {
                [ResolveField(fieldMap, "Id") ?? "Id"] = nextId,
                [ResolveField(fieldMap, "English_Name") ?? "English_Name"] = destination.Name,
                [ResolveField(fieldMap, "Arabic_Name") ?? "Arabic_Name"] = destination.ArabicName ?? string.Empty,
                [ResolveField(fieldMap, "Governorate") ?? "Governorate"] = destination.City,
                [ResolveField(fieldMap, "Category") ?? "Category"] = destination.Category ?? string.Empty,
                [ResolveField(fieldMap, "Description") ?? "Description"] = destination.Description ?? string.Empty,
                [ResolveField(fieldMap, "Status") ?? "Status"] = string.IsNullOrWhiteSpace(destination.Status) ? "Active" : destination.Status,
                [ResolveField(fieldMap, "Visits") ?? "Visits"] = destination.Visits,
                [ResolveField(fieldMap, "Rating") ?? "Rating"] = destination.Rating.HasValue ? (int)Math.Round(destination.Rating.Value, MidpointRounding.AwayFromZero) : 0,
                [ResolveField(fieldMap, "Tags") ?? "Tags"] = destination.Tags ?? string.Empty,
                [ResolveField(fieldMap, "Images") ?? "Images"] = destination.PhotoUrls != null ? destination.PhotoUrls.Replace("\n", "|") : string.Empty,
                [ResolveField(fieldMap, "TicketRequired") ?? "TicketRequired"] = destination.TicketRequired ?? "No",
                [ResolveField(fieldMap, "ForeignPrice") ?? "ForeignPrice"] = destination.ForeignPrice ?? 0,
                [ResolveField(fieldMap, "StudentForeignPrice") ?? "StudentForeignPrice"] = destination.StudentForeignPrice ?? 0,
                [ResolveField(fieldMap, "EgyptianPrice") ?? "EgyptianPrice"] = destination.EgyptianPrice ?? 0,
                [ResolveField(fieldMap, "StudentEgyptianPrice") ?? "StudentEgyptianPrice"] = destination.StudentEgyptianPrice ?? 0,
                [ResolveField(fieldMap, "Days") ?? "Days"] = destination.Days ?? string.Empty,
                [ResolveField(fieldMap, "Open_at") ?? "Open_at"] = destination.OpenAt ?? 0,
                [ResolveField(fieldMap, "Close_at") ?? "Close_at"] = destination.CloseAt ?? 0,
                [ResolveField(fieldMap, "Booking") ?? "Booking"] = destination.Booking ?? string.Empty,
                [ResolveField(fieldMap, "Latitiude") ?? "Latitiude"] = destination.Location?.Y ?? 0.0,
                [ResolveField(fieldMap, "Longitude") ?? "Longitude"] = destination.Location?.X ?? 0.0
            };

            var geometry = new
            {
                x = WebMercatorX(destination.Location?.X ?? 0.0),
                y = WebMercatorY(destination.Location?.Y ?? 0.0),
                spatialReference = new { wkid = 102100 }
            };

            var feature = new { attributes = attrs, geometry = geometry };
            var adds = new[] { feature };

            var formFields = new Dictionary<string, string>
            {
                ["f"] = "json",
                ["adds"] = JsonSerializer.Serialize(adds, _jsonOptions)
            };

            var content = new FormUrlEncodedContent(formFields);
            var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
            var response = await client.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("ArcGIS add destination failed with HTTP {Status}: {Body}", response.StatusCode, errBody);
                return (false, $"ArcGIS returned HTTP {(int)response.StatusCode}: {errBody}", null, null);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("ArcGIS add destination applyEdits response: {Body}", body);

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var errMsg = ExtractArcGISErrorMessage(error);
                _logger.LogError("ArcGIS applyEdits returned error: {Error}", errMsg);
                return (false, $"ArcGIS error: {errMsg}", null, null);
            }

            if (doc.RootElement.TryGetProperty("addResults", out var addResults) && addResults.GetArrayLength() > 0)
            {
                var firstResult = addResults[0];
                if (firstResult.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                {
                    int? objectId = null;
                    if (firstResult.TryGetProperty("objectId", out var oidEl) && oidEl.ValueKind == JsonValueKind.Number)
                    {
                        objectId = oidEl.GetInt32();
                    }

                    return (true, null, objectId, nextId);
                }
                else
                {
                    var errMsg = ExtractArcGISErrorMessage(firstResult);
                    _logger.LogError("ArcGIS add destination result failed: {Error}", errMsg);
                    return (false, $"ArcGIS feature creation failed: {errMsg}", null, null);
                }
            }

            return (false, "ArcGIS did not return addResults.", null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ArcGIS add destination exception");
            return (false, $"ArcGIS request error: {ex.Message}", null, null);
        }
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
