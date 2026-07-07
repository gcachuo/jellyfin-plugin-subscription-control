using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace EasyMovie.Plugin;

public class Plugin : BasePlugin<Configuration.PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "EasyMovie Subscription";

    public override string Description => "Integrates EasyMovie subscription status with Jellyfin playback.";

    public override Guid Id => new("a7a4d4ad-6d45-4dc9-9f5e-8d0a3b4bb21a");
}
