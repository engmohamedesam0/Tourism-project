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

namespace Tourist_Project_MVC.Services;

public interface IArcGISSyncService
{
    Task<ArcGISSyncResult> SyncDestinationsAsync(IEnumerable<Destination> destinations, CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncBranchesAsync(IEnumerable<Branch> branches, CancellationToken ct = default);
    Task<ArcGISSyncResult> SyncDestinationsFromArcGIS(CancellationToken ct = default);
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
                    dest.Rating = (decimal)ratingEl.GetDouble();

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

            var toRemove = dbDestinations.Where(d => !remoteIds.Contains(d.Id)).ToList();
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

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
