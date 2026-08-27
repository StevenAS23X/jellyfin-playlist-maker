using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PlaylistMaker.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.PlaylistMaker;

/// <summary>
/// The Playlist Maker plugin entry point. Adds a "Playlist Maker" page under Dashboard &gt;
/// Plugins that lets users build playlists quickly, with live genre/artist based track
/// recommendations while they build.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Playlist Maker";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("f3b1a7f0-4b9a-4e9c-9c1e-8a2b6d5e7c11");

    /// <inheritdoc />
    public override string Description =>
        "Build playlists quickly with live, genre and artist based recommendations similar to Spotify or Apple Music.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "playlistmaker",
                EmbeddedResourcePath = string.Format("{0}.Web.playlistmaker.html", GetType().Namespace),
                DisplayName = "Playlist Maker: Music Requests",
                EnableInMainMenu = true,
                MenuIcon = "queue_music"
            }
        };
    }
}
