using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A stored record of a Lidarr artist/album request, as shown on the admin settings page.
/// </summary>
public class LidarrRequestRecordDto
{
    /// <summary>
    /// Gets or sets the record's unique id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user who made the request.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this was an "Artist" or "Album" request.
    /// </summary>
    public string RequestType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested artist's name.
    /// </summary>
    public string ArtistName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested album's title. Null for an artist request.
    /// </summary>
    public string? AlbumTitle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Lidarr accepted the request.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the error message, when <see cref="Succeeded"/> is <see langword="false"/>.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets when the request was made (UTC).
    /// </summary>
    public DateTime RequestedAt { get; set; }
}
