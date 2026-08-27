using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// Request body for submitting a custom music request: a link to something Lidarr's search
/// can't resolve on its own (a live recording, a bootleg, a YouTube rip, etc.), left for an
/// admin to action manually.
/// </summary>
public class CustomRequestDto
{
    /// <summary>
    /// Gets or sets the id of the user submitting the request, for per-user rate limiting and
    /// attribution.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the link describing what's being requested.
    /// </summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional note with extra context.
    /// </summary>
    public string? Note { get; set; }
}
