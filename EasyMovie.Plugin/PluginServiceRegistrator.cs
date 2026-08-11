using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Playback;
using EasyMovie.Plugin.Providers;
using EasyMovie.Plugin.Services;
using EasyMovie.Plugin.Tasks;
using EasyMovie.Plugin.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMovie.Plugin;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost serverApplicationHost)
    {
        Console.WriteLine("EasyMovie: Registering plugin services");
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddSingleton<SubscriptionClient>();
        services.AddSingleton<UserPolicyService>();
        services.AddSingleton<IPrerollConfigurationProvider, PrerollConfigurationProvider>();
        services.AddSingleton<PrerollStrategyResolver>();
        services.AddSingleton<IIntroProvider, SubscriptionIntroProvider>();
        services.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, SubscriptionCacheResetTask>();
        services.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, AutoEnableExpiredUsersTask>();
        services.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, LibraryAccessSyncTask>();
        services.AddHostedService<PlaybackInterceptor>();
        services.AddHostedService<JavaScriptInjectorRegistrationService>();
        Console.WriteLine("EasyMovie: Plugin services registered successfully");
    }
}
