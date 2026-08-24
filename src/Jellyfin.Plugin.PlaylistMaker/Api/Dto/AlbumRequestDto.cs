using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for requesting a single album (rather than a whole artist) be added to Lidarr.
/// </summary>
public class AlbumRequestDto
{
    /// <summary>
    /// Gets or sets the id of the user making the request, for per-user rate limiting.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the MusicBrainz release-group id from an album search result.
    /// </summary>
    public string ForeignAlbumId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning artist's MusicBrainz id.
    /// </summary>
    public string ArtistForeignArtistId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artist name, for a clearer error message on failure.
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the album title, for a clearer error message on failure.
    /// </summary>
    public string AlbumTitle { get; set; } = string.Empty;
}
