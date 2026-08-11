using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Models;
using EasyMovie.Plugin.Playback;
using FluentAssertions;
using Xunit;

namespace EasyMovie.Plugin.Tests;

public class PrerollVideoSelectorTests
{
    private readonly PrerollVideoSelector _selector = new();

    [Fact]
    public void Select_TrialWithConfiguredVideo_ReturnsTrialVideo()
    {
        var videos = new PluginConfiguration.VideoPaths { Trial = "/videos/trial.mp4", Expiring = "/videos/expiring.mp4" };

        var path = _selector.Select(new SubscriptionStatus { IsTrial = true }, videos);

        path.Should().Be("/videos/trial.mp4");
    }

    [Fact]
    public void Select_TrialWithoutConfiguredVideo_ReturnsExpiringVideo()
    {
        var videos = new PluginConfiguration.VideoPaths { Expiring = "/videos/expiring.mp4" };

        var path = _selector.Select(new SubscriptionStatus { IsTrial = true }, videos);

        path.Should().Be("/videos/expiring.mp4");
    }

    [Fact]
    public void Select_CourtesyTrial_ReturnsCourtesyVideo()
    {
        var videos = new PluginConfiguration.VideoPaths { Courtesy = "/videos/courtesy.mp4", Trial = "/videos/trial.mp4" };

        var path = _selector.Select(new SubscriptionStatus { Status = "courtesy", IsTrial = true }, videos);

        path.Should().Be("/videos/courtesy.mp4");
    }
}
