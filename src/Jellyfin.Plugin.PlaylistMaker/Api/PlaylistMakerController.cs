using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;
using Jellyfin.Plugin.PlaylistMaker.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Playlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlaylistMaker.Api;

/// <summary>
/// API surface for the Playlist Maker builder UI: library search, genre/artist
/// facets, "more like this" recommendations, and playlist create/append.
/// </summary>
[ApiController]
[Authorize]
[Route("PlaylistMaker")]
public class PlaylistMakerController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;
    private readonly IPlaylistManager _playlistManager;
    private readonly IProviderManager _providerManager;
    private readonly ILidarrService _lidarrService;
    private readonly IRequestRateLimiter _requestRateLimiter;
    private readonly ICustomRequestService _customRequestService;
    private readonly IPendingImportService _pendingImportService;
    private readonly IUserManager _userManager;
    private readonly ILogger<PlaylistMakerController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistMakerController"/> class.
    /// </summary>
    /// <param name="recommendationService">Instance of the <see cref="IRecommendationService"/> interface.</param>
    /// <param name="playlistManager">Instance of the <see cref="IPlaylistManager"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="lidarrService">Instance of the <see cref="ILidarrService"/> interface.</param>
    /// <param name="requestRateLimiter">Instance of the <see cref="IRequestRateLimiter"/> interface.</param>
    /// <param name="customRequestService">Instance of the <see cref="ICustomRequestService"/> interface.</param>
    /// <param name="pendingImportService">Instance of the <see cref="IPendingImportService"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{PlaylistMakerController}"/> interface.</param>
    public PlaylistMakerController(
        IRecommendationService recommendationService,
        IPlaylistManager playlistManager,
        IProviderManager providerManager,
        ILidarrService lidarrService,
        IRequestRateLimiter requestRateLimiter,
        ICustomRequestService customRequestService,
        IPendingImportService pendingImportService,
        IUserManager userManager,
        ILogger<PlaylistMakerController> logger)
    {
        _recommendationService = recommendationService;
        _playlistManager = playlistManager;
        _providerManager = providerManager;
        _lidarrService = lidarrService;
        _requestRateLimiter = requestRateLimiter;
        _customRequestService = customRequestService;
        _pendingImportService = pendingImportService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Serves the standalone Playlist Maker app: a self-contained page with its own login screen,
    /// reachable by any user (not just admins) since Jellyfin's Dashboard is admin-only.
    /// </summary>
    /// <returns>The app's HTML page.</returns>
    [HttpGet("App")]
    [AllowAnonymous]
    public ActionResult GetApp()
    {
        var assembly = GetType().Assembly;
        const string ResourceName = "Jellyfin.Plugin.PlaylistMaker.Web.app.html";

        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            _logger.LogError("Embedded resource {ResourceName} not found", ResourceName);
            return NotFound();
        }

        using var reader = new System.IO.StreamReader(stream);
        var html = reader.ReadToEnd();
        return Content(html, "text/html");
    }

    /// <summary>
    /// Searches the music library by track, artist, or album name.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="query">Free-text search query.</param>
    /// <param name="limit">Maximum number of results (default 25).</param>
    /// <returns>Matching tracks.</returns>
    [HttpGet("Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TrackDto>> Search(
        [FromQuery] Guid userId,
        [FromQuery] string query,
        [FromQuery] int limit = 25)
    {
        return Ok(_recommendationService.Search(userId, query, limit));
    }

    /// <summary>
    /// Searches for artists matching the query, so Search can surface a single artist card
    /// instead of a flood of individual tracks when the query is really an artist name.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="query">Free-text search query.</param>
    /// <param name="limit">Maximum number of matching artists to return.</param>
    /// <returns>Matching artists.</returns>
    [HttpGet("SearchArtists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ArtistDto>> SearchArtists(
        [FromQuery] Guid userId,
        [FromQuery] string query,
        [FromQuery] int limit = 5)
    {
        return Ok(_recommendationService.SearchArtists(userId, query, limit));
    }

    /// <summary>
    /// Browses the library by genre/artist chips, with no free-text query.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="genres">Genres to match.</param>
    /// <param name="artists">Artists to match.</param>
    /// <param name="limit">Maximum number of results (default 30).</param>
    /// <returns>Matching tracks.</returns>
    [HttpGet("Browse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TrackDto>> Browse(
        [FromQuery] Guid userId,
        [FromQuery] string[]? genres = null,
        [FromQuery] string[]? artists = null,
        [FromQuery] int limit = 30)
    {
        return Ok(_recommendationService.Browse(userId, genres ?? Array.Empty<string>(), artists ?? Array.Empty<string>(), limit));
    }

    /// <summary>
    /// Gets the distinct genres in the user's music library, for quick-pick chips.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>Sorted genre names.</returns>
    [HttpGet("Genres")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetGenres([FromQuery] Guid userId)
    {
        return Ok(_recommendationService.GetGenres(userId));
    }

    /// <summary>
    /// Gets the distinct artists in the user's music library, for quick-pick chips.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>Sorted artist names.</returns>
    [HttpGet("Artists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetArtists([FromQuery] Guid userId)
    {
        return Ok(_recommendationService.GetArtists(userId));
    }

    /// <summary>
    /// Gets an artist's albums/singles that are present in the user's library, for the "click an
    /// artist to browse their discography" drill-down.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="artist">The artist name.</param>
    /// <returns>The artist's albums, newest first.</returns>
    [HttpGet("ArtistAlbums")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AlbumDto>> GetArtistAlbums([FromQuery] Guid userId, [FromQuery] string artist)
    {
        return Ok(_recommendationService.GetArtistAlbums(userId, artist));
    }

    /// <summary>
    /// Gets an album's tracks that are present in the user's library, in track order, for the
    /// artist drill-down's album view.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="albumId">The album item id.</param>
    /// <returns>The album's tracks.</returns>
    [HttpGet("AlbumTracks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TrackDto>> GetAlbumTracks([FromQuery] Guid userId, [FromQuery] Guid albumId)
    {
        return Ok(_recommendationService.GetAlbumTracks(userId, albumId));
    }

    /// <summary>
    /// Gets tracks recommended for the current playlist draft, based on the genres/artists
    /// of the tracks already added (or explicitly picked genres/artists for a cold start).
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="seedItemIds">Ids of tracks already in the playlist draft.</param>
    /// <param name="seedGenres">Explicitly picked genres.</param>
    /// <param name="seedArtists">Explicitly picked artists.</param>
    /// <param name="excludeItemIds">Ids to never recommend.</param>
    /// <param name="limit">Maximum number of recommendations.</param>
    /// <returns>Ranked recommended tracks.</returns>
    [HttpGet("Recommendations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TrackDto>> GetRecommendations(
        [FromQuery] Guid userId,
        [FromQuery] Guid[]? seedItemIds = null,
        [FromQuery] string[]? seedGenres = null,
        [FromQuery] string[]? seedArtists = null,
        [FromQuery] Guid[]? excludeItemIds = null,
        [FromQuery] int limit = 20)
    {
        return Ok(_recommendationService.GetRecommendations(
            userId,
            seedItemIds ?? Array.Empty<Guid>(),
            seedGenres ?? Array.Empty<string>(),
            seedArtists ?? Array.Empty<string>(),
            excludeItemIds ?? Array.Empty<Guid>(),
            limit));
    }

    /// <summary>
    /// Gets the playlists the requesting user owns or has been shared, for the
    /// "load an existing playlist to edit" picker.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>The user's playlists.</returns>
    [HttpGet("Playlists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PlaylistSummaryDto>> GetPlaylists([FromQuery] Guid userId)
    {
        var playlists = _playlistManager.GetPlaylists(userId)
            .Where(p => p.PlaylistMediaType == Jellyfin.Data.Enums.MediaType.Audio || p.PlaylistMediaType == Jellyfin.Data.Enums.MediaType.Unknown)
            .Select(p => new PlaylistSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                TrackCount = p.LinkedChildren.Length,
                Public = p.OpenAccess,
                CanEdit = CanEdit(p, userId),
                ImageItemId = p.HasImage(ImageType.Primary, 0) ? p.Id : (Guid?)null
            })
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(playlists);
    }

    /// <summary>
    /// Gets the tracks in an existing playlist, so it can be loaded into the builder for editing.
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>The playlist's tracks, in order.</returns>
    [HttpGet("Playlists/{playlistId}/Items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<TrackDto>> GetPlaylistItems(
        [FromRoute] Guid playlistId,
        [FromQuery] Guid userId)
    {
        var playlist = _playlistManager.GetPlaylistForUser(playlistId, userId);
        if (playlist is null)
        {
            return NotFound();
        }

        var isPermitted = playlist.OpenAccess
            || playlist.OwnerUserId.Equals(userId)
            || playlist.Shares.Any(s => s.UserId.Equals(userId));
        if (!isPermitted)
        {
            return Forbid();
        }

        var tracks = playlist.GetManageableItems()
            .Select(entry => new { entry.Item1, Track = entry.Item2 as Audio })
            .Where(x => x.Track is not null)
            .Select(x => TrackDtoMapper.ToDto(x.Track!, playlistEntryId: x.Item1.ItemId?.ToString("N")))
            .ToList();

        return Ok(tracks);
    }

    /// <summary>
    /// Removes tracks from an existing playlist (e.g. dropped while editing).
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="entryIds">The playlist entry ids to remove (from each track's <c>PlaylistEntryId</c>).</param>
    /// <returns>No content.</returns>
    [HttpDelete("Playlists/{playlistId}/Items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemovePlaylistItems(
        [FromRoute] Guid playlistId,
        [FromQuery] Guid userId,
        [FromQuery] string[] entryIds)
    {
        var playlist = _playlistManager.GetPlaylistForUser(playlistId, userId);
        if (playlist is null)
        {
            return NotFound();
        }

        if (!CanEdit(playlist, userId))
        {
            return Forbid();
        }

        await _playlistManager.RemoveItemFromPlaylistAsync(playlistId.ToString(), entryIds).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Renames an existing playlist.
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="request">The requesting user id and the new name.</param>
    /// <returns>No content.</returns>
    [HttpPost("Playlists/{playlistId}/Name")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RenamePlaylist([FromRoute] Guid playlistId, [FromBody] RenamePlaylistRequestDto request)
    {
        var playlist = _playlistManager.GetPlaylistForUser(playlistId, request.UserId);
        if (playlist is null)
        {
            return NotFound();
        }

        if (!CanEdit(playlist, request.UserId))
        {
            return Forbid();
        }

        // Ids must be left null here - passing a non-null (even empty) list makes
        // IPlaylistManager.UpdatePlaylist wipe every existing item and rebuild the playlist from
        // just that list, which is not what a rename should do.
        await _playlistManager.UpdatePlaylist(new PlaylistUpdateRequest
        {
            Id = playlistId,
            UserId = request.UserId,
            Name = request.Name
        }).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Sets a playlist's cover art from an uploaded image. Jellyfin's own image endpoint requires
    /// admin elevation for any item; this lets the playlist's own owner (or an editor) set the
    /// image for a playlist they're allowed to manage.
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>No content.</returns>
    [HttpPost("Playlists/{playlistId}/Image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetPlaylistImage([FromRoute] Guid playlistId, [FromQuery] Guid userId)
    {
        var playlist = _playlistManager.GetPlaylistForUser(playlistId, userId);
        if (playlist is null)
        {
            return NotFound();
        }

        if (!CanEdit(playlist, userId))
        {
            return Forbid();
        }

        var contentType = Request.ContentType;
        if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Expected an image/* request body.");
        }

        await _providerManager.SaveImage(playlist, Request.Body, contentType, ImageType.Primary, null, CancellationToken.None)
            .ConfigureAwait(false);
        await playlist.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Creates a new playlist from the current draft.
    /// </summary>
    /// <param name="request">The playlist name, owner, and ordered track ids.</param>
    /// <returns>The id of the created playlist.</returns>
    [HttpPost("Playlists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlaylistResultDto>> CreatePlaylist([FromBody] CreatePlaylistRequestDto request)
    {
        var creationRequest = new PlaylistCreationRequest
        {
            Name = request.Name,
            UserId = request.UserId,
            ItemIdList = request.ItemIds.ToList(),
            MediaType = Jellyfin.Data.Enums.MediaType.Audio,
            Public = request.Public
        };

        var result = await _playlistManager.CreatePlaylist(creationRequest).ConfigureAwait(false);

        return Ok(new PlaylistResultDto { Id = Guid.Parse(result.Id) });
    }

    /// <summary>
    /// Appends tracks to an existing playlist (e.g. adding another recommended track later).
    /// </summary>
    /// <param name="playlistId">The playlist to update.</param>
    /// <param name="request">The user id and track ids to append.</param>
    /// <returns>No content.</returns>
    [HttpPost("Playlists/{playlistId}/Items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AddItems([FromRoute] Guid playlistId, [FromBody] AddItemsRequestDto request)
    {
        await _playlistManager.AddItemToPlaylistAsync(playlistId, request.ItemIds.ToList(), request.UserId)
            .ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Matches a batch of imported rows (e.g. from a CSV playlist export) against the library, so
    /// the importer can add whatever's already owned and show the rest as placeholders.
    /// </summary>
    /// <param name="request">The user id and rows to match.</param>
    /// <returns>One result per row, in the same order.</returns>
    [HttpPost("ImportMatch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ImportMatchResultDto>> MatchImport([FromBody] ImportMatchRequestDto request)
    {
        var matches = _recommendationService.MatchImportRows(request.UserId, request.Rows);

        var results = request.Rows.Zip(matches, (row, matchedTrack) => new ImportMatchResultDto
        {
            TrackName = row.TrackName,
            ArtistName = row.ArtistName,
            AlbumName = row.AlbumName,
            DurationTicks = row.DurationTicks,
            MatchedTrack = matchedTrack
        }).ToList();

        return Ok(results);
    }

    /// <summary>
    /// Gets a playlist's still-missing imported rows (placeholders shown last time it was saved),
    /// so they can be re-checked against the library and shown again while it's being edited.
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>The playlist's pending rows.</returns>
    [HttpGet("PendingImports/{playlistId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<ImportRowDto>> GetPendingImports([FromRoute] Guid playlistId, [FromQuery] Guid userId)
    {
        var playlist = _playlistManager.GetPlaylistForUser(playlistId, userId);
        if (playlist is null)
        {
            return NotFound();
        }

        var isPermitted = playlist.OpenAccess
            || playlist.OwnerUserId.Equals(userId)
            || playlist.Shares.Any(s => s.UserId.Equals(userId));
        if (!isPermitted)
        {
            return Forbid();
        }

        return Ok(_pendingImportService.Get(playlistId));
    }

    /// <summary>
    /// Replaces a playlist's still-missing imported rows - called after every save so the stored
    /// list always matches exactly which placeholders are still outstanding in the draft.
    /// </summary>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="request">The requesting user id and the rows still missing.</param>
    /// <returns>No content.</returns>
    [HttpPost("PendingImports/{playlistId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult SetPendingImports([FromRoute] Guid playlistId, [FromBody] ImportMatchRequestDto request)
    {
        var playlist = _playlistManager.GetPlaylistForUser(playlistId, request.UserId);
        if (playlist is null)
        {
            return NotFound();
        }

        if (!CanEdit(playlist, request.UserId))
        {
            return Forbid();
        }

        _pendingImportService.Set(playlistId, request.Rows);
        return NoContent();
    }

    /// <summary>
    /// Gets whether "Request Music" is available (Lidarr is configured), so the UI can hide the
    /// whole feature when it isn't set up.
    /// </summary>
    /// <returns><see langword="true"/> if requests can be made.</returns>
    [HttpGet("MusicRequests/Enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<bool> GetMusicRequestsEnabled()
    {
        return Ok(_lidarrService.IsConfigured);
    }

    /// <summary>
    /// Searches Lidarr for artists matching the given name, to request one be added or to browse
    /// their catalog for individual albums. Artists already present in the user's library are
    /// still shown (flagged via <see cref="LidarrArtistDto.IsOwned"/>) rather than hidden, since a
    /// partially-owned artist is exactly who someone browsing for more albums needs to find.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="term">The search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching artists.</returns>
    [HttpGet("MusicRequests/Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LidarrArtistDto>>> SearchMusicRequests(
        [FromQuery] Guid userId,
        [FromQuery] string term,
        CancellationToken cancellationToken)
    {
        var results = await _lidarrService.SearchArtists(term, cancellationToken).ConfigureAwait(false);
        var ownedArtists = new HashSet<string>(
            _recommendationService.GetArtists(userId).Select(TrackDtoMapper.NormalizeForMatch),
            StringComparer.Ordinal);

        foreach (var result in results)
        {
            result.IsOwned = ownedArtists.Contains(TrackDtoMapper.NormalizeForMatch(result.ArtistName));
        }

        return Ok(results);
    }

    /// <summary>
    /// Gets a specific artist's individual albums/singles, so a request can be scoped to just one
    /// release instead of their entire discography. Lidarr has no direct "list this not-yet-added
    /// artist's catalog" endpoint, so this searches albums by the artist's name and filters down to
    /// ones actually credited to their MusicBrainz id. Albums already present in the user's library
    /// are still included (flagged via <see cref="LidarrAlbumDto.IsOwned"/>) so browsing an
    /// artist's catalog shows the full picture of what's owned vs. missing.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="artistName">The artist's name, used as the album search term.</param>
    /// <param name="foreignArtistId">The artist's MusicBrainz id, to filter results down to just them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artist's matching albums/singles.</returns>
    [HttpGet("MusicRequests/Artists/Albums")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LidarrAlbumDto>>> SearchArtistAlbumRequests(
        [FromQuery] Guid userId,
        [FromQuery] string artistName,
        [FromQuery] string foreignArtistId,
        CancellationToken cancellationToken)
    {
        var results = await _lidarrService.SearchAlbums(artistName, cancellationToken).ConfigureAwait(false);
        var ownedAlbums = new HashSet<string>(
            _recommendationService.GetOwnedAlbums(userId).Select(a => OwnedAlbumKey(a.Artist, a.Title)),
            StringComparer.Ordinal);

        var artistAlbums = results
            .Where(r => string.Equals(r.ArtistForeignArtistId, foreignArtistId, StringComparison.Ordinal))
            .OrderByDescending(r => r.ReleaseDate)
            .ToList();

        foreach (var album in artistAlbums)
        {
            album.IsOwned = ownedAlbums.Contains(OwnedAlbumKey(album.ArtistName, album.Title));
        }

        return Ok(artistAlbums);
    }

    /// <summary>
    /// Requests that an artist be added to Lidarr (monitored, searching for missing albums).
    /// </summary>
    /// <param name="request">The artist to request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("MusicRequests")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> RequestMusic(
        [FromBody] MusicRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_requestRateLimiter.TryRecordRequest(request.UserId, out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
            Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                $"You've hit the request limit. Try again in about {minutes} minute{(minutes == 1 ? string.Empty : "s")}.");
        }

        try
        {
            await _lidarrService.RequestArtist(request.ForeignArtistId, request.ArtistName, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Music request failed for {ArtistName}", request.ArtistName);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Searches Lidarr for albums/singles matching the given title, to request just that release
    /// be added rather than an artist's whole discography. Albums already present in the user's
    /// library are left out.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="term">The search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching albums.</returns>
    [HttpGet("MusicRequests/Albums/Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LidarrAlbumDto>>> SearchAlbumRequests(
        [FromQuery] Guid userId,
        [FromQuery] string term,
        CancellationToken cancellationToken)
    {
        var results = await _lidarrService.SearchAlbums(term, cancellationToken).ConfigureAwait(false);
        var ownedAlbums = new HashSet<string>(
            _recommendationService.GetOwnedAlbums(userId).Select(a => OwnedAlbumKey(a.Artist, a.Title)),
            StringComparer.Ordinal);

        return Ok(results.Where(r => !ownedAlbums.Contains(OwnedAlbumKey(r.ArtistName, r.Title))).ToList());
    }

    /// <summary>
    /// Requests that a single album be added to Lidarr (monitored, searching immediately), without
    /// monitoring or searching for the rest of that artist's discography.
    /// </summary>
    /// <param name="request">The album to request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("MusicRequests/Albums")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> RequestAlbum(
        [FromBody] AlbumRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_requestRateLimiter.TryRecordRequest(request.UserId, out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
            Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                $"You've hit the request limit. Try again in about {minutes} minute{(minutes == 1 ? string.Empty : "s")}.");
        }

        try
        {
            await _lidarrService.RequestAlbum(
                request.ForeignAlbumId,
                request.ArtistForeignArtistId,
                request.ArtistName,
                request.AlbumTitle,
                cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Album request failed for {AlbumTitle}", request.AlbumTitle);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Submits a custom music request: a link (and optional note) for something outside what the
    /// Lidarr artist/album search can resolve on its own, for an admin to review manually on the
    /// settings page. Shares the same per-user rate limit as artist/album requests.
    /// </summary>
    /// <param name="request">The link, optional note, and submitting user.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("CustomRequests")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult SubmitCustomRequest([FromBody] CustomRequestDto request)
    {
        var link = request.Link?.Trim() ?? string.Empty;
        if (link.Length == 0)
        {
            return BadRequest("Enter a link first.");
        }

        if (!_requestRateLimiter.TryRecordRequest(request.UserId, out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
            Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                $"You've hit the request limit. Try again in about {minutes} minute{(minutes == 1 ? string.Empty : "s")}.");
        }

        const int MaxLinkLength = 2000;
        const int MaxNoteLength = 1000;

        var userName = _userManager.GetUserById(request.UserId)?.Username ?? "Unknown user";
        var note = request.Note?.Trim();

        _customRequestService.Add(
            userName,
            link.Length > MaxLinkLength ? link[..MaxLinkLength] : link,
            string.IsNullOrEmpty(note) ? null : (note.Length > MaxNoteLength ? note[..MaxNoteLength] : note));

        return NoContent();
    }

    /// <summary>
    /// Gets every submitted custom request, for the admin settings page.
    /// </summary>
    /// <returns>All stored custom requests, newest first.</returns>
    [HttpGet("CustomRequests")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CustomRequestRecordDto>> GetCustomRequests()
    {
        return Ok(_customRequestService.GetAll());
    }

    /// <summary>
    /// Deletes a custom request once an admin has handled it.
    /// </summary>
    /// <param name="id">The request's id.</param>
    /// <returns>No content.</returns>
    [HttpDelete("CustomRequests/{id}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteCustomRequest([FromRoute] Guid id)
    {
        return _customRequestService.Remove(id) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Gets Lidarr's configured root folders, for the admin settings page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Root folder options.</returns>
    [HttpGet("MusicRequests/RootFolders")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LidarrOptionDto>>> GetLidarrRootFolders(CancellationToken cancellationToken)
    {
        return Ok(await _lidarrService.GetRootFolders(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets Lidarr's configured quality profiles, for the admin settings page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Quality profile options.</returns>
    [HttpGet("MusicRequests/QualityProfiles")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LidarrOptionDto>>> GetLidarrQualityProfiles(CancellationToken cancellationToken)
    {
        return Ok(await _lidarrService.GetQualityProfiles(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets Lidarr's configured metadata profiles, for the admin settings page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata profile options.</returns>
    [HttpGet("MusicRequests/MetadataProfiles")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LidarrOptionDto>>> GetLidarrMetadataProfiles(CancellationToken cancellationToken)
    {
        return Ok(await _lidarrService.GetMetadataProfiles(cancellationToken).ConfigureAwait(false));
    }

    private static bool CanEdit(Playlist playlist, Guid userId)
    {
        return playlist.OwnerUserId.Equals(userId)
            || playlist.Shares.Any(s => s.CanEdit && s.UserId.Equals(userId));
    }

    private static string OwnedAlbumKey(string artist, string title)
    {
        return TrackDtoMapper.NormalizeForMatch(artist) + "|" + TrackDtoMapper.NormalizeForMatch(title);
    }
}
