using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;
using Jellyfin.Plugin.PlaylistMaker.Configuration;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <inheritdoc />
public class LidarrService : ILidarrService
{
    private const string ApiKeyHeader = "X-Api-Key";

    // Lidarr only populates RemotePoster from a "poster"-typed image, which many artists in a
    // fresh lookup (not yet added to Lidarr) simply don't have - most only have "fanart" or
    // "banner" available from their metadata source. Fall back through other cover types instead
    // of leaving the row blank.
    private static readonly string[] PreferredImageTypes =
    {
        "poster", "cover", "fanart", "banner", "logo", "clearlogo", "screenshot", "headshot", "disc"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LidarrService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    public LidarrService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private static PluginConfiguration Config => Plugin.Instance!.Configuration;

    /// <inheritdoc />
    public bool IsConfigured
    {
        get
        {
            var config = Config;
            return config.LidarrEnabled
                && !string.IsNullOrWhiteSpace(config.LidarrBaseUrl)
                && !string.IsNullOrWhiteSpace(config.LidarrApiKey)
                && !string.IsNullOrWhiteSpace(config.LidarrRootFolderPath)
                && config.LidarrQualityProfileId > 0
                && config.LidarrMetadataProfileId > 0;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LidarrArtistDto>> SearchArtists(string term, CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<LidarrArtistDto>();
        }

        var results = await GetAsync<List<LidarrArtistLookupResult>>(
            "/api/v1/artist/lookup?term=" + Uri.EscapeDataString(term),
            cancellationToken).ConfigureAwait(false);

        return (results ?? new List<LidarrArtistLookupResult>())
            .Where(r => !string.IsNullOrWhiteSpace(r.ForeignArtistId))
            .Select(r => new LidarrArtistDto
            {
                ForeignArtistId = r.ForeignArtistId,
                ArtistName = r.ArtistName ?? string.Empty,
                Disambiguation = r.Disambiguation,
                Overview = r.Overview,
                ImageUrl = ResolveImageUrl(r)
            })
            .ToList();
    }

    private static string? ResolveImageUrl(LidarrArtistLookupResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.RemotePoster))
        {
            return result.RemotePoster;
        }

        if (result.Images is null || result.Images.Count == 0)
        {
            return null;
        }

        foreach (var preferredType in PreferredImageTypes)
        {
            var match = result.Images.FirstOrDefault(i =>
                string.Equals(i.CoverType, preferredType, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(i.RemoteUrl));
            if (match is not null)
            {
                return match.RemoteUrl;
            }
        }

        return result.Images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.RemoteUrl))?.RemoteUrl;
    }

    /// <inheritdoc />
    public async Task RequestArtist(string foreignArtistId, string artistName, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Lidarr is not configured.");
        }

        var config = Config;
        var body = new LidarrAddArtistRequest
        {
            ArtistName = artistName,
            ForeignArtistId = foreignArtistId,
            QualityProfileId = config.LidarrQualityProfileId,
            MetadataProfileId = config.LidarrMetadataProfileId,
            RootFolderPath = config.LidarrRootFolderPath,
            Monitored = true,
            MonitorNewItems = "all",
            AddOptions = new LidarrAddArtistOptions
            {
                Monitor = "all",
                SearchForMissingAlbums = true
            }
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(config.LidarrBaseUrl, "/api/v1/artist"))
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Add(ApiKeyHeader, config.LidarrApiKey);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Lidarr rejected the request for \"{artistName}\" ({(int)response.StatusCode}): {errorBody}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LidarrOptionDto>> GetRootFolders(CancellationToken cancellationToken)
    {
        var results = await GetAsync<List<LidarrRootFolderResult>>("/api/v1/rootfolder", cancellationToken).ConfigureAwait(false);
        return (results ?? new List<LidarrRootFolderResult>())
            .Select(r => new LidarrOptionDto { Value = r.Path ?? string.Empty, Label = r.Path ?? string.Empty })
            .Where(o => o.Value.Length > 0)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LidarrOptionDto>> GetQualityProfiles(CancellationToken cancellationToken)
    {
        var results = await GetAsync<List<LidarrProfileResult>>("/api/v1/qualityprofile", cancellationToken).ConfigureAwait(false);
        return MapProfiles(results);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LidarrOptionDto>> GetMetadataProfiles(CancellationToken cancellationToken)
    {
        var results = await GetAsync<List<LidarrProfileResult>>("/api/v1/metadataprofile", cancellationToken).ConfigureAwait(false);
        return MapProfiles(results);
    }

    private static IReadOnlyList<LidarrOptionDto> MapProfiles(List<LidarrProfileResult>? results)
    {
        return (results ?? new List<LidarrProfileResult>())
            .Select(r => new LidarrOptionDto { Value = r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), Label = r.Name ?? string.Empty })
            .ToList();
    }

    private async Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        var config = Config;
        if (string.IsNullOrWhiteSpace(config.LidarrBaseUrl) || string.IsNullOrWhiteSpace(config.LidarrApiKey))
        {
            return default;
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(config.LidarrBaseUrl, relativePath));
        request.Headers.Add(ApiKeyHeader, config.LidarrApiKey);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static Uri BuildUri(string baseUrl, string relativePath)
    {
        return new Uri(baseUrl.TrimEnd('/') + relativePath, UriKind.Absolute);
    }

    private sealed class LidarrArtistLookupResult
    {
        public string ArtistName { get; set; } = string.Empty;

        public string ForeignArtistId { get; set; } = string.Empty;

        public string? Disambiguation { get; set; }

        public string? Overview { get; set; }

        public string? RemotePoster { get; set; }

        public List<LidarrImageResult>? Images { get; set; }
    }

    private sealed class LidarrImageResult
    {
        public string? CoverType { get; set; }

        public string? RemoteUrl { get; set; }
    }

    private sealed class LidarrRootFolderResult
    {
        public string? Path { get; set; }
    }

    private sealed class LidarrProfileResult
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    private sealed class LidarrAddArtistRequest
    {
        public string ArtistName { get; set; } = string.Empty;

        public string ForeignArtistId { get; set; } = string.Empty;

        public int QualityProfileId { get; set; }

        public int MetadataProfileId { get; set; }

        public string RootFolderPath { get; set; } = string.Empty;

        public bool Monitored { get; set; }

        public string MonitorNewItems { get; set; } = "all";

        public LidarrAddArtistOptions AddOptions { get; set; } = new();
    }

    private sealed class LidarrAddArtistOptions
    {
        public string Monitor { get; set; } = "all";

        public bool SearchForMissingAlbums { get; set; }
    }
}
