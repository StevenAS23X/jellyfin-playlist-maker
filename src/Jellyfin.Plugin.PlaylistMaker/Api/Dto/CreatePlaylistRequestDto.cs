using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for creating a new playlist.
/// </summary>
public class CreatePlaylistRequestDto
{
    /// <summary>
    /// Gets or sets the playlist name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the user the playlist is created for.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the ordered list of item ids to add to the new playlist.
    /// </summary>
    public IReadOnlyList<Guid> ItemIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Gets or sets a value indicating whether the playlist should be public.
    /// </summary>
    public bool Public { get; set; }
}
