namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A Lidarr artist search result, for the "Request Music" search.
/// </summary>
public class LidarrArtistDto
{
    /// <summary>
    /// Gets or sets the MusicBrainz id Lidarr uses to identify this artist. Required to request it.
    /// </summary>
    public string ForeignArtistId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artist name.
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short disambiguation string (e.g. "US rock band"), if any.
    /// </summary>
    public string? Disambiguation { get; set; }

    /// <summary>
    /// Gets or sets a short overview/bio, if any.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets a remote (Lidarr-hosted metadata source) poster image URL, if any.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this artist already has at least one album in the
    /// requesting user's library.
    /// </summary>
    public bool IsOwned { get; set; }
}
