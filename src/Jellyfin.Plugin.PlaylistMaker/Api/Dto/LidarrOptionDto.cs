namespace Jellyfin.Plugin.PlaylistMaker.Api.Dto;

/// <summary>
/// A simple id/label option from Lidarr (root folder, quality profile, or metadata profile), for
/// populating the admin settings page's dropdowns.
/// </summary>
public class LidarrOptionDto
{
    /// <summary>
    /// Gets or sets the value to store in plugin configuration - a numeric id for profiles, or
    /// the folder path itself for root folders (Lidarr's own add-artist API takes a path, not an id).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable label to show in the dropdown.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}
