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
    private static readonly char[] GenreSeparators = { ',', ';' };

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
            Genres = GetGenreNames(track).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
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
    /// Gets a track's genres, splitting any tag that has multiple genres jammed into one string
    /// (a common side effect of bad NFO/scan metadata, e.g. "Rock, Folk, Live/1998") into
    /// separate values instead of treating the whole thing as one unreadable genre.
    /// </summary>
    /// <param name="track">The track.</param>
    /// <returns>The individual genre names.</returns>
    public static IEnumerable<string> GetGenreNames(Audio track)
    {
        foreach (var genre in track.Genres ?? Array.Empty<string>())
        {
            foreach (var piece in genre.Split(GenreSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = piece.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
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

    /// <summary>
    /// Strips everything but letters/digits and lowercases, so two strings that differ only in
    /// punctuation/casing (e.g. a Lidarr result vs. a library tag, or a CSV import row vs. a
    /// library track title) still compare equal. Shared by the controller (Lidarr owned-item
    /// matching) and import-row matching, which both need the exact same normalization.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    public static string NormalizeForMatch(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    /// <summary>
    /// Truncates a title at the earliest "extra info" marker - a colon subtitle ("Song: The
    /// Ballad of..."), a trailing " - " annotation ("Song - Remastered 2011"), or a parenthetical
    /// ("Song (feat. Someone)", "Song (Live)") - then normalizes what's left. Used as a fallback
    /// when an imported row's exact title doesn't match anything: streaming-service exports and
    /// library file tags frequently disagree on exactly this kind of trailing annotation for the
    /// same actual song, so a title that's an exact match up to one of these markers is treated as
    /// the same song rather than reported as missing from the library.
    /// </summary>
    /// <param name="title">The title to canonicalize.</param>
    /// <returns>The canonicalized, normalized title.</returns>
    public static string CanonicalizeTitleForMatch(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return string.Empty;
        }

        var cut = title.Length;

        var colonIndex = title.IndexOf(':');
        if (colonIndex >= 0)
        {
            cut = Math.Min(cut, colonIndex);
        }

        var dashIndex = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex >= 0)
        {
            cut = Math.Min(cut, dashIndex);
        }

        var parenIndex = title.IndexOf('(');
        if (parenIndex >= 0)
        {
            cut = Math.Min(cut, parenIndex);
        }

        return NormalizeForMatch(title.Substring(0, cut));
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
