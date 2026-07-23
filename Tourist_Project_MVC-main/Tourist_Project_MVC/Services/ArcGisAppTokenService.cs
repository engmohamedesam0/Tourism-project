using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Tourist_Project_MVC.Services;

public interface IArcGisAppTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}

public class ArcGisAppTokenService : IArcGisAppTokenService, IAsyncDisposable
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ArcGisAppTokenService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset? _cachedExpiresAt;

    public ArcGisAppTokenService(IHttpClientFactory clientFactory, IConfiguration config, ILogger<ArcGisAppTokenService> logger)
    {
        _clientFactory = clientFactory;
        _config = config;
        _logger = logger;
    }

    private string? ClientId => _config["ArcGIS:ClientId"];
    private string? ClientSecret => _config["ArcGIS:ClientSecret"];
    private string? TokenEndpoint => _config["ArcGIS:TokenEndpoint"] ?? "https://www.arcgis.com/sharing/rest/oauth2/token";

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken) && _cachedExpiresAt.HasValue && _cachedExpiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _cachedToken;
        }

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && _cachedExpiresAt.HasValue && _cachedExpiresAt.Value > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return _cachedToken;
            }

            if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
            {
                throw new InvalidOperationException("ArcGIS ClientId and ClientSecret are not configured.");
            }

            var client = _clientFactory.CreateClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["f"] = "json"
            });

            using var response = await client.PostAsync(TokenEndpoint, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"ArcGIS token endpoint returned {(int)response.StatusCode}: {errBody}");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"ArcGIS token error: {error.GetRawText()}");
            }

            var token = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("ArcGIS token response did not contain an access_token.");
            }

            _cachedToken = token;
            _cachedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            _logger.LogInformation("ArcGIS access token acquired, expires at {ExpiresAt}", _cachedExpiresAt.Value);

            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _refreshLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
