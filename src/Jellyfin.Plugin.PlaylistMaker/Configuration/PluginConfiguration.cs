using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PlaylistMaker.Configuration;

/// <summary>
/// Configuration for the Playlist Maker plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of recommendations returned per request.
    /// </summary>
    public int MaxRecommendations { get; set; } = 30;

    /// <summary>
    /// Gets or sets the relative weight given to matching artists (vs. genres) when scoring recommendations.
    /// </summary>
    public double ArtistWeight { get; set; } = 1.6;

    /// <summary>
    /// Gets or sets the relative weight given to matching genres when scoring recommendations.
    /// </summary>
    public double GenreWeight { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets a value indicating whether previously played tracks should get a small popularity boost.
    /// </summary>
    public bool BoostByPlayCount { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether "Request Music" (search Lidarr and add an artist)
    /// is enabled. Off by default until an admin configures the connection.
    /// </summary>
    public bool LidarrEnabled { get; set; }

    /// <summary>
    /// Gets or sets the base URL of the Lidarr instance, e.g. "http://lidarr:8686" - no trailing slash.
    /// </summary>
    public string LidarrBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Lidarr API key (Settings &gt; General &gt; Security in Lidarr).
    /// </summary>
    public string LidarrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the root folder path new artists are added under, e.g. "/music". Must be one
    /// of Lidarr's own configured root folders.
    /// </summary>
    public string LidarrRootFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quality profile id new artists are added with.
    /// </summary>
    public int LidarrQualityProfileId { get; set; }

    /// <summary>
    /// Gets or sets the metadata profile id new artists are added with.
    /// </summary>
    public int LidarrMetadataProfileId { get; set; }
}
