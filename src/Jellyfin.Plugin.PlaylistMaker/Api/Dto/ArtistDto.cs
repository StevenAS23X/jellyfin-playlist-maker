using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A lightweight representation of an artist, used by Search to surface a single artist "card"
/// (name + photo) instead of a flood of individual tracks when the query matches an artist.
/// </summary>
public class ArtistDto
{
    /// <summary>
    /// Gets or sets the artist name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the item whose Primary image should be used as the artist photo, or
    /// <see langword="null"/> if it has none.
    /// </summary>
    public Guid? ImageItemId { get; set; }
}
