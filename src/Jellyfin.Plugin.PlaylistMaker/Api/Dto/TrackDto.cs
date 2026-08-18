using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A lightweight representation of an audio track used by the playlist builder UI.
/// </summary>
public class TrackDto
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the album name, if any.
    /// </summary>
    public string? Album { get; set; }

    /// <summary>
    /// Gets or sets the track artists.
    /// </summary>
    public IReadOnlyList<string> Artists { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the track genres.
    /// </summary>
    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the production year, if known.
    /// </summary>
    public int? ProductionYear { get; set; }

    /// <summary>
    /// Gets or sets the runtime in ticks.
    /// </summary>
    public long? RunTimeTicks { get; set; }

    /// <summary>
    /// Gets or sets a value indicating why this track was recommended (e.g. "Similar artist", "Similar genre").
    /// </summary>
    public string? MatchReason { get; set; }

    /// <summary>
    /// Gets or sets the id of the item (this track, or its album) whose Primary image should be used
    /// as cover art, or <see langword="null"/> if neither has one.
    /// </summary>
    public Guid? ImageItemId { get; set; }

    /// <summary>
    /// Gets or sets the playlist entry id, when this track is returned as part of an existing
    /// playlist's item listing. Required to remove this specific entry from that playlist, since a
    /// playlist can contain the same track more than once.
    /// </summary>
    public string? PlaylistEntryId { get; set; }
}
