using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for renaming an existing playlist.
/// </summary>
public class RenamePlaylistRequestDto
{
    /// <summary>
    /// Gets or sets the id of the user performing the update.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the new playlist name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
