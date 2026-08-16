using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for adding items to an existing playlist.
/// </summary>
public class AddItemsRequestDto
{
    /// <summary>
    /// Gets or sets the id of the user performing the update.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the item ids to append to the playlist.
    /// </summary>
    public IReadOnlyList<Guid> ItemIds { get; set; } = Array.Empty<Guid>();
}
