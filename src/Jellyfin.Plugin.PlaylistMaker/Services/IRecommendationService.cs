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
    /// Searches for artists matching the query, for Search to surface a single artist "card"
    /// (name + photo, linking to the artist drill-down) instead of a flood of individual tracks
    /// when the query is really an artist name.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="query">Free-text search query.</param>
    /// <param name="limit">Maximum number of matching artists to return.</param>
    /// <returns>Matching artists.</returns>
    IReadOnlyList<ArtistDto> SearchArtists(Guid userId, string query, int limit);

    /// <summary>
    /// Browses the user's music library by genre and/or artist, with no free-text query - used
    /// when a genre/artist chip is picked directly instead of typed search.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="genres">Genres to match (a track matching any is included).</param>
    /// <param name="artists">Artists to match (a track matching any is included).</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <returns>Matching tracks.</returns>
    IReadOnlyList<TrackDto> Browse(Guid userId, IReadOnlyList<string> genres, IReadOnlyList<string> artists, int limit);

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
    /// Gets the distinct (artist, album title) pairs present in the user's music library, for
    /// filtering albums the user already has out of the "Request Music" search.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <returns>Artist/album title pairs already in the library.</returns>
    IReadOnlyList<(string Artist, string Title)> GetOwnedAlbums(Guid userId);

    /// <summary>
    /// Gets the albums (and singles/EPs) by the given artist that are present in the user's
    /// library, for the "click an artist to browse their discography" drill-down.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="artist">The artist name, matched exactly (case-insensitive) against a track's
    /// own artist/album-artist tags.</param>
    /// <returns>The artist's albums, newest first.</returns>
    IReadOnlyList<AlbumDto> GetArtistAlbums(Guid userId, string artist);

    /// <summary>
    /// Gets every track from a specific album that's present in the user's library, in track
    /// order, for the artist drill-down's album view.
    /// </summary>
    /// <param name="userId">The requesting user id.</param>
    /// <param name="albumId">The album item id.</param>
    /// <returns>The album's tracks, in disc/track order.</returns>
    IReadOnlyList<TrackDto> GetAlbumTracks(Guid userId, Guid albumId);

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
