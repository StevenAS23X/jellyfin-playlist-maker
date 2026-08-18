using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Builds <see cref="TrackDto"/> instances from library <see cref="Audio"/> items. Shared between
/// <see cref="Jellyfin.Plugin.PlaylistMaker.Services.RecommendationService"/> and the controller so
/// search results, recommendations, and existing-playlist listings all shape tracks the same way.
/// </summary>
public static class TrackDtoMapper
{
    private const string SongKeySeparator = "|";

    /// <summary>
    /// Maps an audio track to its lightweight DTO.
    /// </summary>
    /// <param name="track">The source track.</param>
    /// <param name="matchReason">Why this track was recommended, if applicable.</param>
    /// <param name="playlistEntryId">The playlist entry id, when listing an existing playlist's items.</param>
    /// <returns>The mapped DTO.</returns>
    public static TrackDto ToDto(Audio track, string? matchReason = null, string? playlistEntryId = null)
    {
        return new TrackDto
        {
            Id = track.Id,
            Name = track.Name,
            Album = track.AlbumEntity?.Name,
            Artists = GetArtistNames(track).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Genres = (track.Genres ?? Array.Empty<string>()).ToList(),
            ProductionYear = track.ProductionYear,
            RunTimeTicks = track.RunTimeTicks,
            MatchReason = matchReason,
            ImageItemId = ResolveImageItemId(track),
            PlaylistEntryId = playlistEntryId
        };
    }

    /// <summary>
    /// Gets the artist and album-artist names of a track, in that order (not de-duplicated).
    /// </summary>
    /// <param name="track">The track.</param>
    /// <returns>The artist names.</returns>
    public static IEnumerable<string> GetArtistNames(Audio track)
    {
        foreach (var artist in track.Artists ?? Array.Empty<string>())
        {
            yield return artist;
        }

        foreach (var artist in track.AlbumArtists ?? Array.Empty<string>())
        {
            yield return artist;
        }
    }

    /// <summary>
    /// Builds the de-duplication key for a track: same normalized title + artist set means the
    /// same song, even if the library has it as more than one distinct <see cref="Audio"/> item
    /// (duplicate imports, multiple file versions, etc).
    /// </summary>
    /// <param name="track">The track.</param>
    /// <returns>A case-insensitive key identifying the underlying song.</returns>
    public static string SongKey(Audio track)
    {
        var artists = string.Join(
            ',',
            GetArtistNames(track).Select(a => a.Trim().ToLowerInvariant()).OrderBy(a => a, StringComparer.Ordinal));
        return (track.Name ?? string.Empty).Trim().ToLowerInvariant() + SongKeySeparator + artists;
    }

    private static Guid? ResolveImageItemId(Audio track)
    {
        if (track.AlbumEntity is not null && track.AlbumEntity.HasImage(ImageType.Primary, 0))
        {
            return track.AlbumEntity.Id;
        }

        if (track.HasImage(ImageType.Primary, 0))
        {
            return track.Id;
        }

        return null;
    }
}
