using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Playback;
using FluentAssertions;
using Xunit;

namespace EasyMovie.Plugin.Tests;

public class PrerollStrategyResolverTests
{
    private readonly PrerollStrategyResolver _resolver = new();

    [Fact]
    public void Resolve_WebOverlayClient_ReturnsWebOverlay()
    {
        var configuration = new PluginConfiguration
        {
            EnableWebOverlay = true,
            WebOverlayClientNames = ["Jellyfin Web"]
        };

        var strategy = _resolver.Resolve(configuration, "jellyfin web");

        strategy.Should().Be(PrerollStrategy.WebOverlay);
    }

    [Fact]
    public void Resolve_ValidatedNativeClient_ReturnsNativeIntro()
    {
        var configuration = new PluginConfiguration
        {
            NativePrerollClientNames = ["Jellyfin Media Player"]
        };

        var strategy = _resolver.Resolve(configuration, "Jellyfin Media Player");

        strategy.Should().Be(PrerollStrategy.NativeIntro);
    }

    [Fact]
    public void Resolve_UnknownClient_ReturnsNone()
    {
        var strategy = _resolver.Resolve(new PluginConfiguration(), "Jellyfin for Roku");

        strategy.Should().Be(PrerollStrategy.None);
    }

    [Fact]
    public void Resolve_DefaultAndroidTvClient_ReturnsNativeIntro()
    {
        var strategy = _resolver.Resolve(new PluginConfiguration(), "Jellyfin Android TV");

        strategy.Should().Be(PrerollStrategy.NativeIntro);
    }

    [Fact]
    public void Resolve_DisabledOverlay_UsesNativeFallback()
    {
        var configuration = new PluginConfiguration
        {
            EnableWebOverlay = false,
            WebOverlayClientNames = ["Jellyfin Web"],
            NativePrerollClientNames = ["Jellyfin Web"]
        };

        var strategy = _resolver.Resolve(configuration, "Jellyfin Web");

        strategy.Should().Be(PrerollStrategy.NativeIntro);
    }
}
