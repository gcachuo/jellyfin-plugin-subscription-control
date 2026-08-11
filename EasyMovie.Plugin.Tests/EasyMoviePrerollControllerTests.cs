using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Configuration;
using FluentAssertions;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EasyMovie.Plugin.Tests;

public class EasyMoviePrerollControllerTests
{
    [Fact]
    public async Task GetDecision_MissingUserClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(new PluginConfiguration());

        var result = await controller.GetDecision(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetDecision_MissingItem_ReturnsNone()
    {
        var userId = Guid.NewGuid();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(userId)).Returns(new User("test", "Test", "User"));
        var controller = CreateController(
            new PluginConfiguration(),
            userManager,
            new Mock<ILibraryManager>(),
            CreateUser(userId));

        var result = await controller.GetDecision(Guid.NewGuid(), CancellationToken.None);

        var decision = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<EasyMoviePrerollController.PrerollDecisionDto>().Subject;
        decision.Strategy.Should().Be("none");
    }

    [Fact]
    public async Task GetDecision_PluginVideo_ReturnsNoneWithoutSubscriptionRequest()
    {
        var userId = Guid.NewGuid();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(userId)).Returns(new User("test", "Test", "User"));
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById(It.IsAny<Guid>()))
            .Returns(new Video { Id = Guid.NewGuid(), Path = "/media/intro.mp4" });
        var controller = CreateController(
            new PluginConfiguration { Videos = new PluginConfiguration.VideoPaths { Active = "/media/intro.mp4" } },
            userManager,
            libraryManager,
            CreateUser(userId));

        var result = await controller.GetDecision(Guid.NewGuid(), CancellationToken.None);

        var decision = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<EasyMoviePrerollController.PrerollDecisionDto>().Subject;
        decision.Strategy.Should().Be("none");
    }

    private static EasyMoviePrerollController CreateController(
        PluginConfiguration configuration,
        Mock<IUserManager>? userManager = null,
        Mock<ILibraryManager>? libraryManager = null,
        ClaimsPrincipal? user = null)
    {
        var configurationProvider = new Mock<IPrerollConfigurationProvider>();
        configurationProvider.Setup(provider => provider.GetConfiguration()).Returns(configuration);
        return new EasyMoviePrerollController(
            null!,
            userManager?.Object ?? new Mock<IUserManager>().Object,
            libraryManager?.Object ?? new Mock<ILibraryManager>().Object,
            configurationProvider.Object,
            new Mock<ILogger<EasyMoviePrerollController>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal() } }
        };
    }

    private static ClaimsPrincipal CreateUser(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("Jellyfin-UserId", userId.ToString())
        ], "test"));
    }
}
