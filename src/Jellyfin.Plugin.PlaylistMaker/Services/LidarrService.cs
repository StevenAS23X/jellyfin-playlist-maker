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
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <inheritdoc />
public class LidarrService : ILidarrService
{
    private const string ApiKeyHeader = "X-Api-Key";

    // Preference order for ResolveImageUrl's fallback below.
    private static readonly string[] PreferredImageTypes =
    {
        "poster", "cover", "fanart", "banner", "logo", "clearlogo", "screenshot", "headshot", "disc"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LidarrService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LidarrService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{LidarrService}"/> interface.</param>
    public LidarrService(IHttpClientFactory httpClientFactory, ILogger<LidarrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
                ImageUrl = ResolveImageUrl(r.RemotePoster, r.Images)
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LidarrAlbumDto>> SearchAlbums(string term, CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<LidarrAlbumDto>();
        }

        var results = await GetAsync<List<LidarrAlbumLookupResult>>(
            "/api/v1/album/lookup?term=" + Uri.EscapeDataString(term),
            cancellationToken).ConfigureAwait(false);

        return (results ?? new List<LidarrAlbumLookupResult>())
            .Where(r => !string.IsNullOrWhiteSpace(r.ForeignAlbumId)
                && r.Artist is not null
                && !string.IsNullOrWhiteSpace(r.Artist.ForeignArtistId))
            .Select(r => new LidarrAlbumDto
            {
                ForeignAlbumId = r.ForeignAlbumId,
                Title = r.Title ?? string.Empty,
                Disambiguation = r.Disambiguation,
                AlbumType = r.AlbumType,
                ArtistName = r.Artist!.ArtistName ?? string.Empty,
                ArtistForeignArtistId = r.Artist!.ForeignArtistId,
                ReleaseDate = r.ReleaseDate,
                ImageUrl = ResolveImageUrl(r.RemoteCover, r.Images)
            })
            .ToList();
    }

    // Lidarr only fills in RemotePoster/RemoteCover from a single specific image type, which many
    // search results (not yet added to Lidarr) simply don't have - most only have "fanart" or
    // "banner"/"cover" available from their metadata source. Fall back through other cover types
    // instead of leaving the row blank.
    private static string? ResolveImageUrl(string? primaryImageUrl, List<LidarrImageResult>? images)
    {
        if (!string.IsNullOrWhiteSpace(primaryImageUrl))
        {
            return primaryImageUrl;
        }

        if (images is null || images.Count == 0)
        {
            return null;
        }

        foreach (var preferredType in PreferredImageTypes)
        {
            var match = images.FirstOrDefault(i =>
                string.Equals(i.CoverType, preferredType, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(i.RemoteUrl));
            if (match is not null)
            {
                return match.RemoteUrl;
            }
        }

        return images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.RemoteUrl))?.RemoteUrl;
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
    public async Task RequestAlbum(
        string foreignAlbumId,
        string artistForeignArtistId,
        string artistName,
        string albumTitle,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Lidarr is not configured.");
        }

        var config = Config;

        // Unlike RequestArtist, the artist here is only added as a container for this one album -
        // Monitor "none" plus AlbumsToMonitor scoped to just this release means Lidarr won't pull
        // in or search for the rest of the artist's discography once it refreshes their metadata.
        //
        // Lidarr's own AddAlbumService never triggers the artist metadata refresh for a brand-new
        // artist+album combo add (it hardcodes doRefresh:false for that internal call), so the
        // "search on add" AddOptions flags below - which both only take effect once that refresh
        // completes and fires an internal scan event - never actually fire here. AlbumsToMonitor
        // still needs to be set for future/general monitoring to be scoped correctly, but the
        // actual search is instead triggered directly below, against the album Lidarr just
        // created, right after this call returns.
        var body = new LidarrAddAlbumRequest
        {
            ForeignAlbumId = foreignAlbumId,
            Monitored = true,
            AddOptions = new LidarrAddAlbumOptions
            {
                SearchForNewAlbum = false
            },
            Artist = new LidarrAddArtistRequest
            {
                ArtistName = artistName,
                ForeignArtistId = artistForeignArtistId,
                QualityProfileId = config.LidarrQualityProfileId,
                MetadataProfileId = config.LidarrMetadataProfileId,
                RootFolderPath = config.LidarrRootFolderPath,
                Monitored = true,
                MonitorNewItems = "none",
                AddOptions = new LidarrAddArtistOptions
                {
                    Monitor = "none",
                    AlbumsToMonitor = new List<string> { foreignAlbumId },
                    SearchForMissingAlbums = false
                }
            }
        };

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(config.LidarrBaseUrl, "/api/v1/album"))
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Add(ApiKeyHeader, config.LidarrApiKey);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Lidarr rejected the request for \"{albumTitle}\" ({(int)response.StatusCode}): {errorBody}");
        }

        var created = await response.Content.ReadFromJsonAsync<LidarrAlbumAddResult>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (created is not null && created.Id > 0)
        {
            await TriggerSearchCommand(config, "AlbumSearch", albumIds: new List<int> { created.Id }, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // Best-effort: the album/artist was already added successfully by this point, so a failure to
    // kick off the immediate search shouldn't fail the whole request - Lidarr's own scheduled
    // tasks will eventually pick it up regardless.
    private async Task TriggerSearchCommand(
        PluginConfiguration config,
        string name,
        List<int>? albumIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(config.LidarrBaseUrl, "/api/v1/command"))
            {
                Content = JsonContent.Create(new LidarrCommandRequest { Name = name, AlbumIds = albumIds }, options: JsonOptions)
            };
            request.Headers.Add(ApiKeyHeader, config.LidarrApiKey);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning(
                    "Lidarr rejected the {CommandName} search command ({StatusCode}): {ErrorBody}",
                    name,
                    (int)response.StatusCode,
                    errorBody);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not reach Lidarr to trigger the {CommandName} search command", name);
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

    private sealed class LidarrAlbumLookupResult
    {
        public string Title { get; set; } = string.Empty;

        public string ForeignAlbumId { get; set; } = string.Empty;

        public string? Disambiguation { get; set; }

        public string? AlbumType { get; set; }

        public DateTime? ReleaseDate { get; set; }

        public string? RemoteCover { get; set; }

        public List<LidarrImageResult>? Images { get; set; }

        public LidarrAlbumArtistResult? Artist { get; set; }
    }

    private sealed class LidarrAlbumArtistResult
    {
        public string ArtistName { get; set; } = string.Empty;

        public string ForeignArtistId { get; set; } = string.Empty;
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

        // Only set for a single-album request: restricts which of the artist's albums get
        // monitored once Lidarr refreshes their full discography, instead of Monitor alone
        // (which only supports library-wide rules like "all"/"none"/"future").
        public List<string>? AlbumsToMonitor { get; set; }

        public bool SearchForMissingAlbums { get; set; }
    }

    private sealed class LidarrAddAlbumRequest
    {
        public string ForeignAlbumId { get; set; } = string.Empty;

        public bool Monitored { get; set; }

        public LidarrAddAlbumOptions AddOptions { get; set; } = new();

        public LidarrAddArtistRequest Artist { get; set; } = new();
    }

    private sealed class LidarrAddAlbumOptions
    {
        public bool SearchForNewAlbum { get; set; }
    }

    private sealed class LidarrAlbumAddResult
    {
        public int Id { get; set; }
    }

    private sealed class LidarrCommandRequest
    {
        public string Name { get; set; } = string.Empty;

        public List<int>? AlbumIds { get; set; }
    }
}
