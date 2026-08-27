using System;

namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A stored custom music request, as shown on the admin settings page.
/// </summary>
public class CustomRequestRecordDto
{
    /// <summary>
    /// Gets or sets the request's unique id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user who submitted the request.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the link describing what's being requested.
    /// </summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional note with extra context.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets when the request was submitted (UTC).
    /// </summary>
    public DateTime SubmittedAt { get; set; }
}
