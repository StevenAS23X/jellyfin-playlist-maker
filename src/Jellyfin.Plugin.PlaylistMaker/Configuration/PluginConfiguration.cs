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
}
