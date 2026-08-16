using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PlaylistMaker.Api.Dto;

namespace Jellyfin.Plugin.PlaylistMaker.Services;

/// <summary>
/// Provides library search and content-based ("more like this") track recommendations.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Searches the user's music library by name, artist, or album.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="query">Free-text search query.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <returns>Matching tracks.</returns>
    IReadOnlyList<TrackDto> Search(Guid userId, string query, int limit);

    /// <summary>
    /// Gets the distinct genres present in the user's music library.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>Sorted list of genre names.</returns>
    IReadOnlyList<string> GetGenres(Guid userId);

    /// <summary>
    /// Gets the distinct artists present in the user's music library.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>Sorted list of artist names.</returns>
    IReadOnlyList<string> GetArtists(Guid userId);

    /// <summary>
    /// Scores every eligible track in the library against the given seed tracks / genres / artists
    /// and returns the highest scoring matches, similar to a streaming service's "you might also like"
    /// panel while building a playlist. Falls back to popularity / recency when no seed is supplied.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="seedItemIds">Ids of tracks already in the playlist draft, used to infer taste.</param>
    /// <param name="seedGenres">Genres explicitly picked by the user (cold start / filtering).</param>
    /// <param name="seedArtists">Artists explicitly picked by the user (cold start / filtering).</param>
    /// <param name="excludeItemIds">Ids that must never be recommended (e.g. already in the draft).</param>
    /// <param name="limit">Maximum number of recommendations to return.</param>
    /// <returns>Ranked recommended tracks.</returns>
    IReadOnlyList<TrackDto> GetRecommendations(
        Guid userId,
        IReadOnlyList<Guid> seedItemIds,
        IReadOnlyList<string> seedGenres,
        IReadOnlyList<string> seedArtists,
        IReadOnlyList<Guid> excludeItemIds,
        int limit);
}
