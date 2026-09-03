namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// One row from an imported playlist (e.g. a CSV export), before it's been matched against the
/// library. Also the shape persisted for a playlist's still-missing rows between sessions.
/// </summary>
public class ImportRowDto
{
    /// <summary>
    /// Gets or sets the track/song title.
    /// </summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the (primary) artist name.
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the album name, if known - used to disambiguate when a track title matches
    /// more than one release by the same artist.
    /// </summary>
    public string? AlbumName { get; set; }

    /// <summary>
    /// Gets or sets the track duration in ticks, if known - used as a secondary tie-breaker
    /// alongside <see cref="AlbumName"/>.
    /// </summary>
    public long? DurationTicks { get; set; }
}
