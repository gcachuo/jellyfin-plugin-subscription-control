using System;
using System.Collections.Generic;
using System.Linq;
using EasyMovie.Plugin.Configuration;

namespace EasyMovie.Plugin.Playback;

public enum PrerollStrategy
{
    None,
    WebOverlay,
    NativeIntro
}

public sealed class PrerollStrategyResolver
{
    public PrerollStrategy Resolve(PluginConfiguration configuration, string? clientName)
    {
        if (Matches(configuration.WebOverlayClientNames, clientName) && configuration.EnableWebOverlay)
        {
            return PrerollStrategy.WebOverlay;
        }

        return Matches(configuration.NativePrerollClientNames, clientName)
            ? PrerollStrategy.NativeIntro
            : PrerollStrategy.None;
    }

    private static bool Matches(IEnumerable<string>? configuredClients, string? clientName)
    {
        return !string.IsNullOrWhiteSpace(clientName)
            && configuredClients?.Any(configuredClient => string.Equals(
                configuredClient?.Trim(),
                clientName.Trim(),
                StringComparison.OrdinalIgnoreCase)) == true;
    }
}
