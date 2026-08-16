using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Result of a playlist create/update operation.
/// </summary>
public class PlaylistResultDto
{
    /// <summary>
    /// Gets or sets the id of the playlist.
    /// </summary>
    public Guid Id { get; set; }
}
