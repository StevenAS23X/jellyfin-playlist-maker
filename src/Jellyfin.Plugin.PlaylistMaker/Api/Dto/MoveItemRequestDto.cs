using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for moving a track to a new position within an existing playlist.
/// </summary>
public class MoveItemRequestDto
{
    /// <summary>
    /// Gets or sets the requesting user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the playlist entry id to move (from the track's own <c>PlaylistEntryId</c>).
    /// </summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the 0-based index to move it to.
    /// </summary>
    public int NewIndex { get; set; }
}
