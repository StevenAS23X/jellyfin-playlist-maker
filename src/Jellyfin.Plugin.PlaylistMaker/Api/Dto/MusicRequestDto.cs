using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for requesting an artist be added to Lidarr.
/// </summary>
public class MusicRequestDto
{
    /// <summary>
    /// Gets or sets the id of the user making the request, for per-user rate limiting.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the MusicBrainz id from a Lidarr search result.
    /// </summary>
    public string ForeignArtistId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artist name, for a clearer error message on failure.
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;
}
