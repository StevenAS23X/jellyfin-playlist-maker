using Jellyfin.Plugin.PlaylistMaker.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.PlaylistMaker;

/// <summary>
/// Registers Playlist Maker's services with Jellyfin's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IRecommendationService, RecommendationService>();
        serviceCollection.AddSingleton<ILidarrService, LidarrService>();
        serviceCollection.AddSingleton<IRequestRateLimiter, RequestRateLimiter>();
        serviceCollection.AddSingleton<ICustomRequestService, CustomRequestService>();
    }
}
