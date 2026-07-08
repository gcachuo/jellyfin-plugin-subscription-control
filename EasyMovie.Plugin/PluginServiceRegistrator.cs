using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Playback;
using EasyMovie.Plugin.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMovie.Plugin;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost serverApplicationHost)
    {
        Console.WriteLine("EasyMovie: Registering plugin services");
        services.AddMemoryCache();
        services.AddSingleton<SubscriptionClient>();
        services.AddSingleton<IIntroProvider, SubscriptionIntroProvider>();
        services.AddHostedService<PlaybackInterceptor>();
        services.AddHostedService<LiveTvPolicySyncService>();
        Console.WriteLine("EasyMovie: Plugin services registered successfully");
    }
}
