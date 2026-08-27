using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A Lidarr album/single search result, for requesting just that release instead of an artist's
/// whole discography.
/// </summary>
public class LidarrAlbumDto
{
    /// <summary>
    /// Gets or sets the MusicBrainz release-group id Lidarr uses to identify this album. Required
    /// to request it.
    /// </summary>
    public string ForeignAlbumId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the album (or single) title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short disambiguation string, if any.
    /// </summary>
    public string? Disambiguation { get; set; }

    /// <summary>
    /// Gets or sets the release type (e.g. "Album", "Single", "EP"), if known.
    /// </summary>
    public string? AlbumType { get; set; }

    /// <summary>
    /// Gets or sets the owning artist's name.
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning artist's MusicBrainz id. Required (alongside
    /// <see cref="ForeignAlbumId"/>) to request this album.
    /// </summary>
    public string ArtistForeignArtistId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release date, if known.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets a remote cover image URL, if any.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this album is already in the requesting user's
    /// library. Only ever set when the caller chose to include owned albums instead of filtering
    /// them out (the per-artist album browse); a plain album title search still excludes them.
    /// </summary>
    public bool IsOwned { get; set; }
}
