namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// The result of matching one imported row against the library: either a matched track, or null
/// if nothing in the library matched (the row echoes back the original fields either way, so a
/// still-unmatched row can be re-submitted for matching later without the caller having to keep
/// its own copy around).
/// </summary>
public class ImportMatchResultDto : ImportRowDto
{
    /// <summary>
    /// Gets or sets the matched library track, or <see langword="null"/> if no match was found.
    /// </summary>
    public TrackDto? MatchedTrack { get; set; }
}
