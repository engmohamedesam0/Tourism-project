using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services;

public interface IArcGISSyncService
{
    Task<ArcGISSyncResult> SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncTouristsTableAsync(CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncTouristNationalityLayerAsync(CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncRedemptionsAsync(CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncDestinationsFromArcGIS(CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncBranchesFromArcGIS(CancellationToken ct = default);
    Task<(bool Success, string? Error, int? CreatedObjectId, int? CreatedId)> AddDestinationToArcGISAsync(Destination destination, CancellationToken ct = default);
    Task<ArcGISSyncResult> DeleteDestinationFromArcGISAsync(int destinationId, CancellationToken ct = default);
    Task<ArcGISSyncResult> UpdateDestinationOnArcGISAsync(Destination destination, CancellationToken ct = default);
    Task<ArcGISDestinationSnapshot> GetDestinationSnapshotAsync(int? databaseId = null, CancellationToken ct = default);
}

public record ArcGISSyncResult(
    bool Success,
    string? Error,
    int AddedCount,
    int UpdatedCount,
    int DeletedCount = 0,
    int FailedCount = 0,
    double DurationSeconds = 0,
    DateTime? Timestamp = null)
{
    public static ArcGISSyncResult Ok(int added = 0, int updated = 0, int deleted = 0, int failed = 0, double duration = 0)
        => new(true, null, added, updated, deleted, failed, duration, DateTime.UtcNow);

    public static ArcGISSyncResult Failed(string error, int added = 0, int updated = 0, int deleted = 0, int failed = 0, double duration = 0)
        => new(false, error, added, updated, deleted, failed, duration, DateTime.UtcNow);
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
    private string? TouristsTableUrl => _config["ArcGIS:TouristsTableUrl"];
    private string? TouristNationalityLayerUrl => _config["ArcGIS:TouristNationalityLayerUrl"];
    private string? RedemptionsTableUrl => _config["ArcGIS:RedemptionsTableUrl"];

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

    /// <summary>
    /// Queries all remote ObjectIds and stable IDs in batches to avoid N+1 queries.
    /// Returns a dictionary mapping stable Id -> OBJECTID.
    /// </summary>
    private async Task<Dictionary<int, int>> QueryAllIdToObjectIdMapAsync(HttpClient client, string layerUrl, string token, string idFieldName, CancellationToken ct)
    {
        var map = new Dictionary<int, int>();
        int offset = 0;
        const int pageSize = 1000;

        while (true)
        {
            var queryUrl = $"{layerUrl}/query?where=1%3D1&f=json&token={Uri.EscapeDataString(token)}&outFields=ObjectId,{Uri.EscapeDataString(idFieldName)}&returnGeometry=false&resultOffset={offset}&resultRecordCount={pageSize}";
            using var response = await client.GetAsync(queryUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ArcGIS query for IdToObjectId map failed with HTTP {Status}", response.StatusCode);
                break;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                _logger.LogWarning("ArcGIS query error: {Error}", ExtractArcGISErrorMessage(error));
                break;
            }

            if (!doc.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
            {
                break;
            }

            int countInPage = 0;
            foreach (var feature in features.EnumerateArray())
            {
                countInPage++;
                if (!feature.TryGetProperty("attributes", out var attrs)) continue;

                int objectId = 0;
                int stableId = 0;

                foreach (var prop in attrs.EnumerateObject())
                {
                    if (prop.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        objectId = prop.Value.GetInt32();
                    }
                    else if (prop.Name.Equals(idFieldName, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        stableId = prop.Value.GetInt32();
                    }
                }

                if (objectId > 0 && stableId > 0)
                {
                    map[stableId] = objectId;
                }
            }

            var exceeded = doc.RootElement.TryGetProperty("exceededTransferLimit", out var excEl) && excEl.ValueKind == JsonValueKind.True;
            if (!exceeded && countInPage < pageSize)
            {
                break;
            }

            offset += countInPage;
        }

        return map;
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

    // =========================================================================
    // 1. SYNC DESTINATIONS: Database -> ArcGIS Feature Layer
    // =========================================================================
    public async Task<ArcGISSyncResult> SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var layerUrl = LayerUrl(DestinationsLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        var list = destinations.Where(x => x.Location != null).ToList();
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

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var idField = ResolveField(fieldMap, "Id") ?? "Id";

            // Query existing features in ONE batch call to avoid N+1 requests
            var remoteIdToOidMap = await QueryAllIdToObjectIdMapAsync(client, layerUrl, token, idField, ct);

            var adds = new List<object>();
            var updates = new List<object>();
            var addsTargetIds = new List<int>();
            var updatesTargetOids = new List<int>();

            foreach (var d in list)
            {
                var attrs = new Dictionary<string, object>
                {
                    [ResolveField(fieldMap, "Id") ?? "Id"] = d.Id,
                    [ResolveField(fieldMap, "English_Name") ?? "English_Name"] = d.Name,
                    [ResolveField(fieldMap, "Arabic_Name") ?? "Arabic_Name"] = d.ArabicName ?? "",
                    [ResolveField(fieldMap, "Governorate") ?? "Governorate"] = d.City,
                    [ResolveField(fieldMap, "Category") ?? "Category"] = d.Category ?? "",
                    [ResolveField(fieldMap, "Description") ?? "Description"] = d.Description ?? "",
                    [ResolveField(fieldMap, "Status") ?? "Status"] = string.IsNullOrWhiteSpace(d.Status) ? "Active" : d.Status,
                    [ResolveField(fieldMap, "Visits") ?? "Visits"] = d.Visits,
                    [ResolveField(fieldMap, "Rating") ?? "Rating"] = d.Rating ?? 0m,
                    [ResolveField(fieldMap, "Tags") ?? "Tags"] = d.Tags ?? "",
                    [ResolveField(fieldMap, "Images") ?? "Images"] = d.PhotoUrls?.Replace("\r\n", "|").Replace("\n", "|") ?? "",
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

                if (remoteIdToOidMap.TryGetValue(d.Id, out var existingOid))
                {
                    var updateAttrs = new Dictionary<string, object>(attrs)
                    {
                        ["OBJECTID"] = existingOid
                    };
                    updates.Add(new { attributes = updateAttrs, geometry = geometry });
                    updatesTargetOids.Add(existingOid);
                }
                else
                {
                    adds.Add(new { attributes = attrs, geometry = geometry });
                    addsTargetIds.Add(d.Id);
                }
            }

            if (adds.Count == 0 && updates.Count == 0)
            {
                sw.Stop();
                return ArcGISSyncResult.Ok(added: 0, updated: 0, duration: sw.Elapsed.TotalSeconds);
            }

            int totalAdded = 0;
            int totalUpdated = 0;
            int totalFailed = 0;
            const int batchSize = 100;

            // Send adds in batches
            for (int i = 0; i < adds.Count; i += batchSize)
            {
                var batchAdds = adds.Skip(i).Take(batchSize).ToList();
                var batchTargetIds = addsTargetIds.Skip(i).Take(batchSize).ToList();

                var formFields = new Dictionary<string, string>
                {
                    ["f"] = "json",
                    ["adds"] = JsonSerializer.Serialize(batchAdds, _jsonOptions)
                };

                var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
                using var response = await client.PostAsync(url, new FormUrlEncodedContent(formFields), ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("ArcGIS destinations adds batch failed with HTTP {Status}: {Body}", response.StatusCode, errBody);
                    totalFailed += batchAdds.Count;
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("addResults", out var addResults))
                {
                    int resIdx = 0;
                    foreach (var result in addResults.EnumerateArray())
                    {
                        if (result.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                        {
                            totalAdded++;
                        }
                        else
                        {
                            totalFailed++;
                            var targetId = resIdx < batchTargetIds.Count ? batchTargetIds[resIdx] : -1;
                            _logger.LogError("ArcGIS destination add failed for Id={TargetId}: {Error}", targetId, ExtractArcGISErrorMessage(result));
                        }
                        resIdx++;
                    }
                }
            }

            // Send updates in batches
            for (int i = 0; i < updates.Count; i += batchSize)
            {
                var batchUpdates = updates.Skip(i).Take(batchSize).ToList();
                var batchTargetOids = updatesTargetOids.Skip(i).Take(batchSize).ToList();

                var formFields = new Dictionary<string, string>
                {
                    ["f"] = "json",
                    ["updates"] = JsonSerializer.Serialize(batchUpdates, _jsonOptions)
                };

                var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
                using var response = await client.PostAsync(url, new FormUrlEncodedContent(formFields), ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("ArcGIS destinations updates batch failed with HTTP {Status}: {Body}", response.StatusCode, errBody);
                    totalFailed += batchUpdates.Count;
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("updateResults", out var updateResults))
                {
                    int resIdx = 0;
                    foreach (var result in updateResults.EnumerateArray())
                    {
                        if (result.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                        {
                            totalUpdated++;
                        }
                        else
                        {
                            totalFailed++;
                            var targetOid = resIdx < batchTargetOids.Count ? batchTargetOids[resIdx] : -1;
                            _logger.LogError("ArcGIS destination update failed for OBJECTID={TargetOid}: {Error}", targetOid, ExtractArcGISErrorMessage(result));
                        }
                        resIdx++;
                    }
                }
            }

            sw.Stop();
            _logger.LogInformation("ArcGIS destinations sync completed in {Duration:F2}s: {Added} added, {Updated} updated, {Failed} failed",
                sw.Elapsed.TotalSeconds, totalAdded, totalUpdated, totalFailed);

            return totalFailed > 0 && totalAdded == 0 && totalUpdated == 0
                ? ArcGISSyncResult.Failed("ArcGIS applyEdits had failures on all items.", totalAdded, totalUpdated, deleted: 0, failed: totalFailed, duration: sw.Elapsed.TotalSeconds)
                : ArcGISSyncResult.Ok(added: totalAdded, updated: totalUpdated, deleted: 0, failed: totalFailed, duration: sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ArcGIS destinations sync exception");
            return ArcGISSyncResult.Failed($"ArcGIS destinations sync failed: {ex.Message}", duration: sw.Elapsed.TotalSeconds);
        }
    }

    // =========================================================================
    // 2. PULL DESTINATIONS: ArcGIS Feature Layer -> Website Database (UPSERT ONLY)
    // =========================================================================
    public async Task<ArcGISSyncResult> SyncDestinationsFromArcGIS(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
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

            // Read ALL features from ArcGIS via pagination loop so no records are missed
            var remoteFeatures = new List<JsonElement>();
            int offset = 0;
            const int pageSize = 1000;

            while (true)
            {
                var queryUrl = $"{layerUrl}/query?where=1%3D1&outFields=*&returnGeometry=true&resultOffset={offset}&resultRecordCount={pageSize}&f=json&token={Uri.EscapeDataString(token)}";
                using var response = await client.GetAsync(queryUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("ArcGIS destinations query failed with HTTP {Status}: {Body}", response.StatusCode, errBody);
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
                    break;
                }

                int countInPage = 0;
                foreach (var f in features.EnumerateArray())
                {
                    remoteFeatures.Add(f.Clone());
                    countInPage++;
                }

                var exceeded = doc.RootElement.TryGetProperty("exceededTransferLimit", out var excEl) && excEl.ValueKind == JsonValueKind.True;
                if (!exceeded && countInPage < pageSize)
                {
                    break;
                }

                offset += countInPage;
            }

            if (!remoteFeatures.Any())
            {
                sw.Stop();
                _logger.LogInformation("ArcGIS destinations pull-sync: zero features found on remote layer. Local database preserved.");
                return ArcGISSyncResult.Ok(added: 0, updated: 0, duration: sw.Elapsed.TotalSeconds);
            }

            // Load existing destinations from local DB into dictionary by Id
            var dbDestinations = await _context.Destinations.ToListAsync(ct);
            var dbMap = dbDestinations.ToDictionary(d => d.Id);
            int maxId = dbMap.Keys.DefaultIfEmpty(0).Max();

            int addedCount = 0;
            int updatedCount = 0;
            var arcgisFeaturesNeedingIdUpdate = new List<(int ObjectId, int AssignedId)>();

            foreach (var feature in remoteFeatures)
            {
                if (!feature.TryGetProperty("attributes", out var attrs)) continue;

                int objectId = 0;
                if (attrs.TryGetProperty("ObjectId", out var oidEl) && oidEl.ValueKind == JsonValueKind.Number)
                    objectId = oidEl.GetInt32();
                else if (attrs.TryGetProperty("OBJECTID", out var oidEl2) && oidEl2.ValueKind == JsonValueKind.Number)
                    objectId = oidEl2.GetInt32();

                int id = 0;
                if (attrs.TryGetProperty("Id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                    id = idEl.GetInt32();

                // If feature created in ArcGIS directly without an Id, allocate the next local Id
                bool isNewlyAllocatedId = false;
                if (id <= 0)
                {
                    id = ++maxId;
                    isNewlyAllocatedId = true;
                    if (objectId > 0)
                    {
                        arcgisFeaturesNeedingIdUpdate.Add((objectId, id));
                    }
                }

                string name = attrs.TryGetProperty("English_Name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = attrs.TryGetProperty("Name", out var altNameEl) && altNameEl.ValueKind == JsonValueKind.String ? altNameEl.GetString() ?? $"Destination {id}" : $"Destination {id}";
                }

                string? arabicName = attrs.TryGetProperty("Arabic_Name", out var arEl) && arEl.ValueKind == JsonValueKind.String ? arEl.GetString() : null;
                string city = attrs.TryGetProperty("Governorate", out var govEl) && govEl.ValueKind == JsonValueKind.String ? govEl.GetString() ?? "Egypt" : "Egypt";
                string? category = attrs.TryGetProperty("Category", out var catEl) && catEl.ValueKind == JsonValueKind.String ? catEl.GetString() : null;

                string? description = null;
                if (attrs.TryGetProperty("Description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                {
                    var raw = descEl.GetString();
                    description = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                string status = "Active";
                if (attrs.TryGetProperty("Status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String)
                {
                    var rawStatus = statusEl.GetString();
                    status = rawStatus is null or "N/A" or "" or "n/a" ? "Active" : rawStatus;
                }

                int visits = 0;
                if (attrs.TryGetProperty("Visits", out var visitsEl) && visitsEl.ValueKind == JsonValueKind.Number)
                    visits = visitsEl.GetInt32();

                decimal rating = 0m;
                if (attrs.TryGetProperty("Rating", out var ratingEl) && ratingEl.ValueKind == JsonValueKind.Number)
                    rating = Math.Round((decimal)ratingEl.GetDouble(), 0, MidpointRounding.AwayFromZero);

                string? tags = null;
                if (attrs.TryGetProperty("Tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.String)
                {
                    var raw = tagsEl.GetString();
                    tags = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                string? photoUrls = null;
                if (attrs.TryGetProperty("Images", out var imagesEl) && imagesEl.ValueKind == JsonValueKind.String)
                {
                    var rawImages = imagesEl.GetString();
                    photoUrls = string.IsNullOrWhiteSpace(rawImages) ? null : rawImages.Replace("|", "\n");
                }

                string? ticketRequired = null;
                if (attrs.TryGetProperty("TicketRequired", out var ticketReqEl) && ticketReqEl.ValueKind == JsonValueKind.String)
                {
                    var raw = ticketReqEl.GetString();
                    ticketRequired = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                int? foreignPrice = attrs.TryGetProperty("ForeignPrice", out var fpEl) && fpEl.ValueKind == JsonValueKind.Number ? fpEl.GetInt32() : null;
                int? studentForeignPrice = attrs.TryGetProperty("StudentForeignPrice", out var sfpEl) && sfpEl.ValueKind == JsonValueKind.Number ? sfpEl.GetInt32() : null;
                int? egyptianPrice = attrs.TryGetProperty("EgyptianPrice", out var epEl) && epEl.ValueKind == JsonValueKind.Number ? epEl.GetInt32() : null;
                int? studentEgyptianPrice = attrs.TryGetProperty("StudentEgyptianPrice", out var sepEl) && sepEl.ValueKind == JsonValueKind.Number ? sepEl.GetInt32() : null;

                string? days = null;
                if (attrs.TryGetProperty("Days", out var daysEl) && daysEl.ValueKind == JsonValueKind.String)
                {
                    var raw = daysEl.GetString();
                    days = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                int? openAt = attrs.TryGetProperty("Open_at", out var openEl) && openEl.ValueKind == JsonValueKind.Number ? openEl.GetInt32() : null;
                int? closeAt = attrs.TryGetProperty("Close_at", out var closeEl) && closeEl.ValueKind == JsonValueKind.Number ? closeEl.GetInt32() : null;

                string? booking = null;
                if (attrs.TryGetProperty("Booking", out var bookingEl) && bookingEl.ValueKind == JsonValueKind.String)
                {
                    var raw = bookingEl.GetString();
                    booking = raw is null or "N/A" or "n/a" or "" ? null : raw;
                }

                double lat = 0, lng = 0;
                bool hasLat = (attrs.TryGetProperty("Latitiude", out var latEl) || attrs.TryGetProperty("Latitude", out latEl)) && latEl.ValueKind == JsonValueKind.Number;
                bool hasLng = attrs.TryGetProperty("Longitude", out var lngEl) && lngEl.ValueKind == JsonValueKind.Number;
                if (hasLat) lat = latEl.GetDouble();
                if (hasLng) lng = lngEl.GetDouble();

                if ((lat == 0 && lng == 0) && feature.TryGetProperty("geometry", out var geom))
                {
                    if (geom.TryGetProperty("x", out var xEl) && geom.TryGetProperty("y", out var yEl))
                    {
                        var rawX = xEl.GetDouble();
                        var rawY = yEl.GetDouble();
                        if (Math.Abs(rawX) > 180 || Math.Abs(rawY) > 90)
                        {
                            lng = WebMercatorToLongitude(rawX);
                            lat = WebMercatorToLatitude(rawY);
                        }
                        else
                        {
                            lng = rawX;
                            lat = rawY;
                        }
                    }
                }

                Point location = (lat != 0 || lng != 0)
                    ? new Point(lng, lat) { SRID = 4326 }
                    : new Point(31.2357, 30.0444) { SRID = 4326 };

                if (dbMap.TryGetValue(id, out var existing))
                {
                    // UPDATE existing destination
                    existing.Name = name;
                    existing.ArabicName = arabicName;
                    existing.City = city;
                    existing.Category = category;
                    existing.Description = description;
                    existing.Status = status;
                    if (visits > 0) existing.Visits = visits;
                    if (rating > 0) existing.Rating = rating;
                    existing.Tags = tags;
                    existing.PhotoUrls = photoUrls;
                    existing.TicketRequired = ticketRequired;
                    existing.ForeignPrice = foreignPrice;
                    existing.StudentForeignPrice = studentForeignPrice;
                    existing.EgyptianPrice = egyptianPrice;
                    existing.StudentEgyptianPrice = studentEgyptianPrice;
                    existing.Days = days;
                    existing.OpenAt = openAt;
                    existing.CloseAt = closeAt;
                    existing.Booking = booking;
                    existing.Location = location;
                    _context.Destinations.Update(existing);
                    updatedCount++;
                }
                else
                {
                    // INSERT new destination (never delete existing ones!)
                    var newDest = new Destination
                    {
                        Id = id,
                        Name = name,
                        ArabicName = arabicName,
                        City = city,
                        Category = category,
                        Description = description,
                        Status = status,
                        Visits = visits,
                        Rating = rating,
                        Tags = tags,
                        PhotoUrls = photoUrls,
                        TicketRequired = ticketRequired,
                        ForeignPrice = foreignPrice,
                        StudentForeignPrice = studentForeignPrice,
                        EgyptianPrice = egyptianPrice,
                        StudentEgyptianPrice = studentEgyptianPrice,
                        Days = days,
                        OpenAt = openAt,
                        CloseAt = closeAt,
                        Booking = booking,
                        Location = location
                    };
                    _context.Destinations.Add(newDest);
                    dbMap[id] = newDest;
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync(ct);

            // Update PostgreSQL sequence so future inserts won't conflict
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT setval(pg_get_serial_sequence('\"Destinations\"', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM \"Destinations\"), 1))", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("PostgreSQL sequence setval warning: {Message}", ex.Message);
            }

            // If any remote features were missing an Id, write back their newly assigned Id
            if (arcgisFeaturesNeedingIdUpdate.Any())
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var updates = arcgisFeaturesNeedingIdUpdate.Select(item => new
                        {
                            attributes = new Dictionary<string, object>
                            {
                                ["OBJECTID"] = item.ObjectId,
                                [ResolveField(fieldMap, "Id") ?? "Id"] = item.AssignedId
                            }
                        }).ToList();

                        var form = new Dictionary<string, string>
                        {
                            ["f"] = "json",
                            ["updates"] = JsonSerializer.Serialize(updates, _jsonOptions)
                        };
                        using var backClient = _clientFactory.CreateClient();
                        backClient.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");
                        var backUrl = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
                        await backClient.PostAsync(backUrl, new FormUrlEncodedContent(form));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to back-populate assigned Id to ArcGIS features");
                    }
                }, ct);
            }

            sw.Stop();
            _logger.LogInformation("ArcGIS destinations pull-sync complete in {Duration:F2}s: {Added} added, {Updated} updated, 0 deleted",
                sw.Elapsed.TotalSeconds, addedCount, updatedCount);

            return ArcGISSyncResult.Ok(added: addedCount, updated: updatedCount, deleted: 0, failed: 0, duration: sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ArcGIS destinations pull-sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS destinations pull-sync failed: {ex.Message}", duration: sw.Elapsed.TotalSeconds);
        }
    }

    // =========================================================================
    // 3. SYNC BRANCHES: Database -> ArcGIS Feature Layer
    // =========================================================================
    public async Task<ArcGISSyncResult> SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var layerUrl = LayerUrl(BranchesLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        var list = branches.Where(x => x.Location != null).ToList();
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

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var idField = ResolveField(fieldMap, "Id") ?? "Id";
            var hasCategoryField = fieldMap?.ContainsKey("Category") == true;

            var remoteIdToOidMap = await QueryAllIdToObjectIdMapAsync(client, layerUrl, token, idField, ct);

            var adds = new List<object>();
            var updates = new List<object>();

            foreach (var b in list)
            {
                var geometry = new
                {
                    x = b.Location.X,
                    y = b.Location.Y,
                    spatialReference = new { wkid = 4326 }
                };

                var attrs = new Dictionary<string, object>
                {
                    [ResolveField(fieldMap, "Id") ?? "Id"] = b.Id,
                    [ResolveField(fieldMap, "SponsorId") ?? "SponsorId"] = b.SponsorId,
                    [ResolveField(fieldMap, "Name") ?? "Name"] = b.Name,
                    [ResolveField(fieldMap, "Address") ?? "Address"] = b.Address,
                    [ResolveField(fieldMap, "ContactNumber") ?? "ContactNumber"] = b.ContactNumber ?? 0,
                    [ResolveField(fieldMap, "latitude") ?? "latitude"] = b.Location.Y,
                    [ResolveField(fieldMap, "longitude") ?? "longitude"] = b.Location.X
                };
                if (hasCategoryField)
                    attrs[ResolveField(fieldMap, "Category") ?? "Category"] = b.Category ?? "";

                if (remoteIdToOidMap.TryGetValue(b.Id, out var existingOid))
                {
                    var updateAttrs = new Dictionary<string, object>(attrs)
                    {
                        ["OBJECTID"] = existingOid
                    };
                    updates.Add(new { attributes = updateAttrs, geometry = geometry });
                }
                else
                {
                    adds.Add(new { attributes = attrs, geometry = geometry });
                }
            }

            if (adds.Count == 0 && updates.Count == 0)
            {
                sw.Stop();
                return ArcGISSyncResult.Ok(added: 0, updated: 0, duration: sw.Elapsed.TotalSeconds);
            }

            int totalAdded = 0;
            int totalUpdated = 0;
            int totalFailed = 0;
            const int batchSize = 100;

            for (int i = 0; i < adds.Count; i += batchSize)
            {
                var batchAdds = adds.Skip(i).Take(batchSize).ToList();
                var formFields = new Dictionary<string, string>
                {
                    ["f"] = "json",
                    ["adds"] = JsonSerializer.Serialize(batchAdds, _jsonOptions)
                };
                using var response = await client.PostAsync($"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}", new FormUrlEncodedContent(formFields), ct);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("addResults", out var addResults))
                    {
                        foreach (var res in addResults.EnumerateArray())
                        {
                            if (res.TryGetProperty("success", out var s) && s.GetBoolean()) totalAdded++;
                            else totalFailed++;
                        }
                    }
                }
                else totalFailed += batchAdds.Count;
            }

            for (int i = 0; i < updates.Count; i += batchSize)
            {
                var batchUpdates = updates.Skip(i).Take(batchSize).ToList();
                var formFields = new Dictionary<string, string>
                {
                    ["f"] = "json",
                    ["updates"] = JsonSerializer.Serialize(batchUpdates, _jsonOptions)
                };
                using var response = await client.PostAsync($"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}", new FormUrlEncodedContent(formFields), ct);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("updateResults", out var updateResults))
                    {
                        foreach (var res in updateResults.EnumerateArray())
                        {
                            if (res.TryGetProperty("success", out var s) && s.GetBoolean()) totalUpdated++;
                            else totalFailed++;
                        }
                    }
                }
                else totalFailed += batchUpdates.Count;
            }

            sw.Stop();
            _logger.LogInformation("ArcGIS branches sync complete in {Duration:F2}s: {Added} added, {Updated} updated, {Failed} failed",
                sw.Elapsed.TotalSeconds, totalAdded, totalUpdated, totalFailed);

            return totalFailed > 0 && totalAdded == 0 && totalUpdated == 0
                ? ArcGISSyncResult.Failed("ArcGIS applyEdits had failures for branches.", totalAdded, totalUpdated, deleted: 0, failed: totalFailed, duration: sw.Elapsed.TotalSeconds)
                : ArcGISSyncResult.Ok(added: totalAdded, updated: totalUpdated, deleted: 0, failed: totalFailed, duration: sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ArcGIS branches sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS branches sync failed: {ex.Message}", duration: sw.Elapsed.TotalSeconds);
        }
    }

    // =========================================================================
    // 4. PULL BRANCHES: ArcGIS Feature Layer -> Website Database (UPSERT ONLY)
    // =========================================================================
    public async Task<ArcGISSyncResult> SyncBranchesFromArcGIS(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var layerUrl = LayerUrl(BranchesLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS branches pull-sync skipped: API Key is missing");
            return ArcGISSyncResult.Failed("API Key is missing.");
        }

        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");

            var remoteFeatures = new List<JsonElement>();
            int offset = 0;
            const int pageSize = 1000;

            while (true)
            {
                var queryUrl = $"{layerUrl}/query?where=1%3D1&outFields=*&returnGeometry=true&resultOffset={offset}&resultRecordCount={pageSize}&f=json&token={Uri.EscapeDataString(token)}";
                using var response = await client.GetAsync(queryUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("ArcGIS branches query failed with HTTP status {Status}: {Body}", response.StatusCode, errBody);
                    return ArcGISSyncResult.Failed($"ArcGIS query returned HTTP {(int)response.StatusCode}: {errBody}");
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    var errMsg = ExtractArcGISErrorMessage(error);
                    _logger.LogError("ArcGIS branches query returned error: {Error}", errMsg);
                    return ArcGISSyncResult.Failed($"ArcGIS query error: {errMsg}");
                }

                if (!doc.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                {
                    break;
                }

                int countInPage = 0;
                foreach (var f in features.EnumerateArray())
                {
                    remoteFeatures.Add(f.Clone());
                    countInPage++;
                }

                var exceeded = doc.RootElement.TryGetProperty("exceededTransferLimit", out var excEl) && excEl.ValueKind == JsonValueKind.True;
                if (!exceeded && countInPage < pageSize)
                {
                    break;
                }

                offset += countInPage;
            }

            var existingMap = await _context.Branches.ToDictionaryAsync(b => b.Id, ct);
            int maxBranchId = existingMap.Keys.DefaultIfEmpty(0).Max();
            int addedCount = 0;
            int updatedCount = 0;

            foreach (var feature in remoteFeatures)
            {
                if (!feature.TryGetProperty("attributes", out var attrs)) continue;

                int id = 0;
                if (attrs.TryGetProperty("Id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                    id = idEl.GetInt32();
                else if (attrs.TryGetProperty("ObjectId", out var oidEl) && oidEl.ValueKind == JsonValueKind.Number)
                    id = oidEl.GetInt32();
                else if (attrs.TryGetProperty("OBJECTID", out var oidEl2) && oidEl2.ValueKind == JsonValueKind.Number)
                    id = oidEl2.GetInt32();

                if (id <= 0) id = ++maxBranchId;

                int sponsorId = 1;
                if (attrs.TryGetProperty("SponsorId", out var spEl) && spEl.ValueKind == JsonValueKind.Number)
                    sponsorId = spEl.GetInt32();

                string name = attrs.TryGetProperty("Name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString() ?? "Branch" : "Branch";
                string address = attrs.TryGetProperty("Address", out var addrEl) && addrEl.ValueKind == JsonValueKind.String ? addrEl.GetString() ?? "Egypt" : "Egypt";
                string category = attrs.TryGetProperty("Category", out var catEl) && catEl.ValueKind == JsonValueKind.String ? catEl.GetString() ?? "General" : "General";

                int contact = 20000000;
                if (attrs.TryGetProperty("ContactNumber", out var contactEl) && contactEl.ValueKind == JsonValueKind.Number)
                    contact = contactEl.GetInt32();

                double lat = 30.0;
                double lng = 31.0;
                if (attrs.TryGetProperty("latitude", out var latEl) && latEl.ValueKind == JsonValueKind.Number)
                    lat = latEl.GetDouble();
                if (attrs.TryGetProperty("longitude", out var lngEl) && lngEl.ValueKind == JsonValueKind.Number)
                    lng = lngEl.GetDouble();

                if ((lat == 30.0 && lng == 31.0) && feature.TryGetProperty("geometry", out var geom))
                {
                    if (geom.TryGetProperty("x", out var xEl) && geom.TryGetProperty("y", out var yEl))
                    {
                        var rx = xEl.GetDouble();
                        var ry = yEl.GetDouble();
                        if (Math.Abs(rx) > 180 || Math.Abs(ry) > 90)
                        {
                            lng = WebMercatorToLongitude(rx);
                            lat = WebMercatorToLatitude(ry);
                        }
                        else
                        {
                            lng = rx;
                            lat = ry;
                        }
                    }
                }

                if (existingMap.TryGetValue(id, out var branch))
                {
                    branch.SponsorId = sponsorId;
                    branch.Name = name;
                    branch.Address = address;
                    branch.Category = category;
                    branch.ContactNumber = contact;
                    branch.Location = new Point(lng, lat) { SRID = 4326 };
                    updatedCount++;
                }
                else
                {
                    var newBranch = new Branch
                    {
                        Id = id,
                        SponsorId = sponsorId,
                        Name = name,
                        Address = address,
                        Category = category,
                        ContactNumber = contact,
                        Location = new Point(lng, lat) { SRID = 4326 }
                    };
                    _context.Branches.Add(newBranch);
                    existingMap[id] = newBranch;
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync(ct);

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT setval(pg_get_serial_sequence('\"Branches\"', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM \"Branches\"), 1))", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Branches sequence setval warning: {Message}", ex.Message);
            }

            sw.Stop();
            _logger.LogInformation("ArcGIS branches pull-sync completed in {Duration:F2}s: {Added} added, {Updated} updated.",
                sw.Elapsed.TotalSeconds, addedCount, updatedCount);
            return ArcGISSyncResult.Ok(added: addedCount, updated: updatedCount, deleted: 0, failed: 0, duration: sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ArcGIS branches pull-sync failed.");
            return ArcGISSyncResult.Failed(ex.Message, duration: sw.Elapsed.TotalSeconds);
        }
    }

    // =========================================================================
    // 5. SYNC TOURISTS TABLE
    // =========================================================================
    public async Task<ArcGISSyncResult> SyncTouristsTableAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var layerUrl = LayerUrl(TouristsTableUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS tourists table sync skipped: API Key is missing");
            return ArcGISSyncResult.Failed("API Key is missing.");
        }

        try
        {
            var tourists = await (
                from t in _context.Tourists
                join u in _context.Users on t.ApplicationUserId equals u.Id
                select new { Tourist = t, User = u }
            ).ToListAsync(ct);

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var idField = ResolveField(fieldMap, "TouristId") ?? "TouristId";

            var remoteFeatures = await QueryAllTableIdsAsync(client, layerUrl, token, idField, ct);

            var dbIds = new HashSet<int>();
            var adds = new List<object>();
            var updates = new List<object>();
            var deletes = new List<int>();

            foreach (var item in tourists)
            {
                var t = item.Tourist;
                var u = item.User;
                dbIds.Add(t.Id);

                var attrs = new Dictionary<string, object>
                {
                    [ResolveField(fieldMap, "TouristId") ?? "TouristId"] = t.Id,
                    [ResolveField(fieldMap, "UserId") ?? "UserId"] = u.Id,
                    [ResolveField(fieldMap, "Email") ?? "Email"] = u.Email ?? string.Empty,
                    [ResolveField(fieldMap, "FirstName") ?? "FirstName"] = u.FirstName,
                    [ResolveField(fieldMap, "LastName") ?? "LastName"] = u.LastName,
                    [ResolveField(fieldMap, "FullName") ?? "FullName"] = $"{u.FirstName} {u.LastName}".Trim(),
                    [ResolveField(fieldMap, "Nationality") ?? "Nationality"] = u.Nationality ?? string.Empty,
                    [ResolveField(fieldMap, "PhoneNumber") ?? "PhoneNumber"] = u.PhoneNumber ?? string.Empty,
                    [ResolveField(fieldMap, "IdNumber") ?? "IdNumber"] = t.IdNumber ?? string.Empty,
                    [ResolveField(fieldMap, "Passport") ?? "Passport"] = t.Passport ?? string.Empty,
                    [ResolveField(fieldMap, "PointBalance") ?? "PointBalance"] = t.point_Balance,
                    [ResolveField(fieldMap, "RegisterDate") ?? "RegisterDate"] = t.RegisterDate.ToString("yyyy-MM-dd"),
                    [ResolveField(fieldMap, "Status") ?? "Status"] = t.Status ?? "Active"
                };

                var existing = remoteFeatures.FirstOrDefault(f => f.TouristId == t.Id);
                if (existing.ObjectId > 0)
                {
                    var updAttrs = new Dictionary<string, object> { ["OBJECTID"] = existing.ObjectId };
                    foreach (var kv in attrs) updAttrs[kv.Key] = kv.Value;
                    updates.Add(new { attributes = updAttrs });
                }
                else
                {
                    adds.Add(new { attributes = attrs });
                }
            }

            foreach (var remote in remoteFeatures)
            {
                if (!dbIds.Contains(remote.TouristId))
                {
                    deletes.Add(remote.ObjectId);
                }
            }

            if (adds.Count == 0 && updates.Count == 0 && deletes.Count == 0)
            {
                sw.Stop();
                return ArcGISSyncResult.Ok(duration: sw.Elapsed.TotalSeconds);
            }

            var formFields = new Dictionary<string, string> { ["f"] = "json" };
            if (adds.Count > 0) formFields["adds"] = JsonSerializer.Serialize(adds, _jsonOptions);
            if (updates.Count > 0) formFields["updates"] = JsonSerializer.Serialize(updates, _jsonOptions);
            if (deletes.Count > 0) formFields["deletes"] = string.Join(",", deletes);

            var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
            using var response = await client.PostAsync(url, new FormUrlEncodedContent(formFields), ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                sw.Stop();
                return ArcGISSyncResult.Failed($"ArcGIS returned HTTP {(int)response.StatusCode}: {errBody}", duration: sw.Elapsed.TotalSeconds);
            }

            sw.Stop();
            _logger.LogInformation("ArcGIS tourists table sync complete in {Duration:F2}s: {Added} added, {Updated} updated, {Deleted} deleted",
                sw.Elapsed.TotalSeconds, adds.Count, updates.Count, deletes.Count);

            return ArcGISSyncResult.Ok(added: adds.Count, updated: updates.Count, deleted: deletes.Count, failed: 0, duration: sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ArcGIS tourists table sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS tourists table sync failed: {ex.Message}", duration: sw.Elapsed.TotalSeconds);
        }
    }

    private async Task<List<(int ObjectId, int TouristId)>> QueryAllTableIdsAsync(HttpClient client, string layerUrl, string token, string idField, CancellationToken ct)
    {
        var result = new List<(int ObjectId, int TouristId)>();
        var queryUrl = $"{layerUrl}/query?where=1%3D1&outFields=ObjectId,{Uri.EscapeDataString(idField)}&returnGeometry=false&resultRecordCount=1000&f=json&token={Uri.EscapeDataString(token)}";
        using var response = await client.GetAsync(queryUrl, ct);
        if (!response.IsSuccessStatusCode) return result;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("features", out var features))
        {
            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("attributes", out var attrs)) continue;
                int objectId = 0;
                int touristId = 0;
                foreach (var property in attrs.EnumerateObject())
                {
                    if (property.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number)
                        objectId = property.Value.GetInt32();
                    if (property.Name.Equals(idField, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number)
                        touristId = property.Value.GetInt32();
                }
                if (objectId > 0) result.Add((objectId, touristId));
            }
        }
        return result;
    }

    // =========================================================================
    // 6. SYNC TOURIST NATIONALITY LAYER
    // =========================================================================
    public async Task<ArcGISSyncResult> SyncTouristNationalityLayerAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var layerUrl = LayerUrl(TouristNationalityLayerUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS tourist nationality sync skipped: API Key is missing");
            return ArcGISSyncResult.Failed("API Key is missing.");
        }

        try
        {
            var aggregates = await (
                from t in _context.Tourists
                join u in _context.Users on t.ApplicationUserId equals u.Id
                where u.Nationality != null && u.Nationality != ""
                group t by u.Nationality into g
                select new { Nationality = g.Key, Count = g.Count() }
            ).ToListAsync(ct);

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var natField = ResolveField(fieldMap, "Nationality") ?? "Nationality";
            var countField = ResolveField(fieldMap, "TouristCount") ?? "TouristCount";
            var latField = ResolveField(fieldMap, "Latitude") ?? "Latitude";
            var lngField = ResolveField(fieldMap, "Longitude") ?? "Longitude";

            var remoteFeatures = await QueryAllNationalityFeaturesAsync(client, layerUrl, token, natField, ct);

            var newSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var adds = new List<object>();
            var updates = new List<object>();
            var deletes = new List<int>();

            foreach (var agg in aggregates)
            {
                var centroid = NationalityCentroids.Get(agg.Nationality);
                if (centroid == null)
                {
                    _logger.LogWarning("ArcGIS tourist nationality sync: no centroid for '{Nationality}' — skipped", agg.Nationality);
                    continue;
                }
                newSet.Add(agg.Nationality);

                var attrs = new Dictionary<string, object>
                {
                    [natField] = agg.Nationality,
                    [countField] = agg.Count,
                    [latField] = centroid.Value.Latitude,
                    [lngField] = centroid.Value.Longitude
                };
                var geometry = new
                {
                    x = centroid.Value.Longitude,
                    y = centroid.Value.Latitude,
                    spatialReference = new { wkid = 4326 }
                };

                var existing = remoteFeatures.FirstOrDefault(f =>
                    string.Equals(f.Nationality, agg.Nationality, StringComparison.OrdinalIgnoreCase));

                if (existing.ObjectId > 0)
                {
                    updates.Add(new
                    {
                        attributes = new Dictionary<string, object>
                        {
                            ["OBJECTID"] = existing.ObjectId,
                            [natField] = agg.Nationality,
                            [countField] = agg.Count,
                            [latField] = centroid.Value.Latitude,
                            [lngField] = centroid.Value.Longitude
                        },
                        geometry = geometry
                    });
                }
                else
                {
                    adds.Add(new { attributes = attrs, geometry = geometry });
                }
            }

            foreach (var remote in remoteFeatures)
            {
                if (!newSet.Contains(remote.Nationality))
                {
                    deletes.Add(remote.ObjectId);
                }
            }

            if (adds.Count == 0 && updates.Count == 0 && deletes.Count == 0)
            {
                sw.Stop();
                return ArcGISSyncResult.Ok(duration: sw.Elapsed.TotalSeconds);
            }

            var formFields = new Dictionary<string, string> { ["f"] = "json" };
            if (adds.Count > 0) formFields["adds"] = JsonSerializer.Serialize(adds, _jsonOptions);
            if (updates.Count > 0) formFields["updates"] = JsonSerializer.Serialize(updates, _jsonOptions);
            if (deletes.Count > 0) formFields["deletes"] = string.Join(",", deletes);

            var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
            using var response = await client.PostAsync(url, new FormUrlEncodedContent(formFields), ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                sw.Stop();
                return ArcGISSyncResult.Failed($"ArcGIS returned HTTP {(int)response.StatusCode}: {errBody}", duration: sw.Elapsed.TotalSeconds);
            }

            sw.Stop();
            _logger.LogInformation("ArcGIS tourist nationality sync complete in {Duration:F2}s: {Added} added, {Updated} updated, {Deleted} deleted",
                sw.Elapsed.TotalSeconds, adds.Count, updates.Count, deletes.Count);

            return ArcGISSyncResult.Ok(added: adds.Count, updated: updates.Count, deleted: deletes.Count, failed: 0, duration: sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ArcGIS tourist nationality sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS tourist nationality sync failed: {ex.Message}", duration: sw.Elapsed.TotalSeconds);
        }
    }

    private async Task<List<(int ObjectId, string Nationality)>> QueryAllNationalityFeaturesAsync(HttpClient client, string layerUrl, string token, string natField, CancellationToken ct)
    {
        var result = new List<(int ObjectId, string Nationality)>();
        var queryUrl = $"{layerUrl}/query?where=1%3D1&outFields=ObjectId,{Uri.EscapeDataString(natField)}&returnGeometry=false&resultRecordCount=1000&f=json&token={Uri.EscapeDataString(token)}";
        using var response = await client.GetAsync(queryUrl, ct);
        if (!response.IsSuccessStatusCode) return result;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("features", out var features))
        {
            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("attributes", out var attrs)) continue;
                int objectId = 0;
                string? nationality = null;
                foreach (var property in attrs.EnumerateObject())
                {
                    if (property.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number)
                        objectId = property.Value.GetInt32();
                    if (property.Name.Equals(natField, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                        nationality = property.Value.GetString();
                }
                if (objectId > 0) result.Add((objectId, nationality ?? string.Empty));
            }
        }
        return result;
    }

    // =========================================================================
    // 7. SYNC REDEMPTIONS
    // =========================================================================
    public async Task<ArcGISSyncResult> SyncRedemptionsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var layerUrl = LayerUrl(RedemptionsTableUrl);
        if (string.IsNullOrWhiteSpace(layerUrl)) return ArcGISSyncResult.Ok();

        string token = _config["ArcGIS:ApiKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError("ArcGIS redemptions table sync skipped: API Key is missing");
            return ArcGISSyncResult.Failed("API Key is missing.");
        }

        try
        {
            var redemptions = await _context.Redemptions
                .Include(r => r.Reward)
                .OrderBy(r => r.Id)
                .ToListAsync(ct);

            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");

            var fieldMap = await GetFieldMapAsync(client, layerUrl, token, ct);
            var idField = ResolveField(fieldMap, "RedemptionId") ?? "RedemptionId";

            var remoteFeatures = await QueryAllRedemptionIdsAsync(client, layerUrl, token, idField, ct);

            var dbIds = new HashSet<int>();
            var adds = new List<object>();
            var updates = new List<object>();
            var deletes = new List<int>();

            foreach (var r in redemptions)
            {
                dbIds.Add(r.Id);

                var attrs = new Dictionary<string, object>
                {
                    [ResolveField(fieldMap, "RedemptionId") ?? "RedemptionId"] = r.Id,
                    [ResolveField(fieldMap, "RewardTitle") ?? "RewardTitle"] = r.Reward?.Title ?? string.Empty,
                    [ResolveField(fieldMap, "TouristId") ?? "TouristId"] = r.TouristId,
                    [ResolveField(fieldMap, "BranchId") ?? "BranchId"] = r.BranchId,
                    [ResolveField(fieldMap, "PointsRedeemed") ?? "PointsRedeemed"] = r.PointsRedeemed,
                    [ResolveField(fieldMap, "RedemptionDate") ?? "RedemptionDate"] = r.RedemptionDate.ToString("yyyy-MM-dd"),
                    [ResolveField(fieldMap, "Status") ?? "Status"] = string.IsNullOrWhiteSpace(r.Status) ? "Active" : r.Status
                };

                var existing = remoteFeatures.FirstOrDefault(f => f.RedemptionId == r.Id);
                if (existing.ObjectId > 0)
                {
                    var updAttrs = new Dictionary<string, object> { ["OBJECTID"] = existing.ObjectId };
                    foreach (var kv in attrs) updAttrs[kv.Key] = kv.Value;
                    updates.Add(new { attributes = updAttrs });
                }
                else
                {
                    adds.Add(new { attributes = attrs });
                }
            }

            foreach (var remote in remoteFeatures)
            {
                if (!dbIds.Contains(remote.RedemptionId))
                {
                    deletes.Add(remote.ObjectId);
                }
            }

            if (adds.Count == 0 && updates.Count == 0 && deletes.Count == 0)
            {
                sw.Stop();
                return ArcGISSyncResult.Ok(duration: sw.Elapsed.TotalSeconds);
            }

            var formFields = new Dictionary<string, string> { ["f"] = "json" };
            if (adds.Count > 0) formFields["adds"] = JsonSerializer.Serialize(adds, _jsonOptions);
            if (updates.Count > 0) formFields["updates"] = JsonSerializer.Serialize(updates, _jsonOptions);
            if (deletes.Count > 0) formFields["deletes"] = string.Join(",", deletes);

            var url = $"{layerUrl}/applyEdits?token={Uri.EscapeDataString(token)}";
            using var response = await client.PostAsync(url, new FormUrlEncodedContent(formFields), ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                sw.Stop();
                return ArcGISSyncResult.Failed($"ArcGIS returned HTTP {(int)response.StatusCode}: {errBody}", duration: sw.Elapsed.TotalSeconds);
            }

            sw.Stop();
            _logger.LogInformation("ArcGIS redemptions table sync complete in {Duration:F2}s: {Added} added, {Updated} updated, {Deleted} deleted",
                sw.Elapsed.TotalSeconds, adds.Count, updates.Count, deletes.Count);

            return ArcGISSyncResult.Ok(added: adds.Count, updated: updates.Count, deleted: deletes.Count, failed: 0, duration: sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "ArcGIS redemptions table sync failed");
            return ArcGISSyncResult.Failed($"ArcGIS redemptions table sync failed: {ex.Message}", duration: sw.Elapsed.TotalSeconds);
        }
    }

    private async Task<List<(int ObjectId, int RedemptionId)>> QueryAllRedemptionIdsAsync(HttpClient client, string layerUrl, string token, string idField, CancellationToken ct)
    {
        var result = new List<(int ObjectId, int RedemptionId)>();
        var queryUrl = $"{layerUrl}/query?where=1%3D1&outFields=ObjectId,{Uri.EscapeDataString(idField)}&returnGeometry=false&resultRecordCount=1000&f=json&token={Uri.EscapeDataString(token)}";
        using var response = await client.GetAsync(queryUrl, ct);
        if (!response.IsSuccessStatusCode) return result;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("features", out var features))
        {
            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("attributes", out var attrs)) continue;
                int objectId = 0;
                int redemptionId = 0;
                foreach (var property in attrs.EnumerateObject())
                {
                    if (property.Name.Equals("ObjectId", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number)
                        objectId = property.Value.GetInt32();
                    if (property.Name.Equals(idField, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Number)
                        redemptionId = property.Value.GetInt32();
                }
                if (objectId > 0) result.Add((objectId, redemptionId));
            }
        }
        return result;
    }

    // =========================================================================
    // 8. SNAPSHOT FOR ADMIN EXPLORER
    // =========================================================================
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
                    int? objectId = attributes.TryGetValue("ObjectId", out var objectValue) ? ConvertNullableInt(objectValue) : (attributes.TryGetValue("OBJECTID", out var objectValue2) ? ConvertNullableInt(objectValue2) : null);
                    int? id = attributes.TryGetValue("Id", out var idValue) ? ConvertNullableInt(idValue) : null;
                    double? latitude = attributes.TryGetValue("Latitiude", out var latitudeValue) ? ConvertNullableDouble(latitudeValue) : (attributes.TryGetValue("Latitude", out var latitudeValue2) ? ConvertNullableDouble(latitudeValue2) : null);
                    double? longitude = attributes.TryGetValue("Longitude", out var longitudeValue) ? ConvertNullableDouble(longitudeValue) : null;
                    if ((!latitude.HasValue || !longitude.HasValue) && feature.TryGetProperty("geometry", out var geometry))
                    {
                        var x = geometry.TryGetProperty("x", out var xElement) ? xElement.GetDouble() : 0;
                        var y = geometry.TryGetProperty("y", out var yElement) ? yElement.GetDouble() : 0;
                        if (Math.Abs(x) > 180 || Math.Abs(y) > 90)
                        {
                            longitude = WebMercatorToLongitude(x);
                            latitude = WebMercatorToLatitude(y);
                        }
                        else
                        {
                            longitude = x;
                            latitude = y;
                        }
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

    // =========================================================================
    // 9. SINGLE ITEM CRUD HELPERS
    // =========================================================================
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
            var nextId = destination.Id > 0 ? destination.Id : await GetNextDestinationIdAsync(client, layerUrl, token, idField, ct);

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
                [ResolveField(fieldMap, "Images") ?? "Images"] = destination.PhotoUrls != null ? destination.PhotoUrls.Replace("\r\n", "|").Replace("\n", "|") : string.Empty,
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

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
