using System.IO;
using System.Reflection;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace EasyMovie.Plugin.Configuration;

public class PluginConfigurationPage : IPluginConfigurationPage
{
    public string Name => "EasyMovieSubscription";

    public string DisplayName => "EasyMovie Subscription";

    public PluginPageType PageType => PluginPageType.Configuration;

    public Stream GetHtmlStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream("EasyMovie.Plugin.Configuration.configPage.html")
            ?? Stream.Null;
    }
}
