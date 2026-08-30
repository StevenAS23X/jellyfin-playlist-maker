using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A lightweight representation of a music album used by the artist album/single browser.
/// </summary>
public class AlbumDto
{
    /// <summary>
    /// Gets or sets the album item id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the album name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release year, if known.
    /// </summary>
    public int? ProductionYear { get; set; }

    /// <summary>
    /// Gets or sets the number of tracks from this album that are actually in the user's library.
    /// </summary>
    public int TrackCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this release is short enough to be treated as a
    /// single/EP rather than a full album, for grouping in the artist browser.
    /// </summary>
    public bool IsSingle { get; set; }

    /// <summary>
    /// Gets or sets the id of the item whose Primary image should be used as cover art, or
    /// <see langword="null"/> if it has none.
    /// </summary>
    public Guid? ImageItemId { get; set; }
}
