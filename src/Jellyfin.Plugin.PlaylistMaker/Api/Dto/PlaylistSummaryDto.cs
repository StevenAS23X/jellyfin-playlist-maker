using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A lightweight representation of one of the user's existing playlists, for the "load an
/// existing playlist to edit" picker.
/// </summary>
public class PlaylistSummaryDto
{
    /// <summary>
    /// Gets or sets the playlist id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the playlist name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of tracks currently in the playlist.
    /// </summary>
    public int TrackCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the playlist is publicly visible to other users.
    /// </summary>
    public bool Public { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the requesting user can edit this playlist
    /// (owner, or shared with edit permission).
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets the id of the item whose Primary image should be used as cover art, or
    /// <see langword="null"/> if the playlist has none set.
    /// </summary>
    public Guid? ImageItemId { get; set; }
}
