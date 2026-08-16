using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;
using Jellyfin.Plugin.PlaylistMaker.Services;
using MediaBrowser.Controller.Playlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistMakerController"/> class.
    /// </summary>
    /// <param name="recommendationService">Instance of the <see cref="IRecommendationService"/> interface.</param>
    /// <param name="playlistManager">Instance of the <see cref="IPlaylistManager"/> interface.</param>
    public PlaylistMakerController(IRecommendationService recommendationService, IPlaylistManager playlistManager)
    {
        _recommendationService = recommendationService;
        _playlistManager = playlistManager;
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
            MediaType = MediaBrowser.Model.Entities.MediaType.Audio,
            Public = request.Public
        };

        var result = await _playlistManager.CreatePlaylist(creationRequest).ConfigureAwait(false);

        return Ok(new PlaylistResultDto { Id = result.Id });
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
        await _playlistManager.AddToPlaylistAsync(playlistId, request.ItemIds.ToList(), request.UserId)
            .ConfigureAwait(false);

        return NoContent();
    }
}
