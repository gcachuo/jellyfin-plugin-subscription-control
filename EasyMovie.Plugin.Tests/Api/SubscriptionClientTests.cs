using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Models;
using FluentAssertions;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace EasyMovie.Plugin.Tests.Api;

public class SubscriptionClientTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<SubscriptionClient>> _loggerMock;
    private readonly SubscriptionClient _client;

    public SubscriptionClientTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<SubscriptionClient>>();
        _client = new SubscriptionClient(_cacheMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetStatusAsync_EmptyApiUrl_ReturnsFailSafe()
    {
        // Arrange
        var user = CreateTestUser();
        var config = new PluginConfiguration { ApiUrl = "" };

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FailSafe.Should().BeTrue();
        result.Status.Should().Be("active");
        result.Error.Should().Contain("API URL not configured");
    }

    [Fact]
    public async Task GetStatusAsync_CachedValue_ReturnsCached()
    {
        // Arrange
        var user = CreateTestUser();
        var config = new PluginConfiguration
        {
            ApiUrl = "http://test.com",
            CacheDurationMinutes = 10
        };
        var cachedStatus = new SubscriptionStatus { Status = "active", Cached = false };
        
        object? cacheValue = cachedStatus;
        _cacheMock
            .Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(true);

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Cached.Should().BeTrue();
        result.Status.Should().Be("active");
    }

    [Fact]
    public async Task GetStatusAsync_ApiSuccess_ReturnsStatus()
    {
        // Arrange
        var user = CreateTestUser();
        var config = new PluginConfiguration
        {
            ApiUrl = "http://test.com/api",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        var expectedStatus = new SubscriptionStatus
        {
            Status = "active",
            Plan = new PlanInfo
            {
                Id = "basic",
                Name = "Basic Plan",
                EnableAllFolders = false,
                AllowLiveTv = false,
                EnabledFolderIds = new[] { Guid.NewGuid().ToString() }
            }
        };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedStatus))
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(NamedClient.Default))
            .Returns(httpClient);

        object? cacheValue = null;
        _cacheMock
            .Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(false);

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("active");
        result.Plan.Should().NotBeNull();
        result.Plan!.Id.Should().Be("basic");
    }

    [Fact]
    public async Task GetStatusAsync_ApiError_ReturnsFailSafe()
    {
        // Arrange
        var user = CreateTestUser();
        var config = new PluginConfiguration
        {
            ApiUrl = "http://test.com/api",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(NamedClient.Default))
            .Returns(httpClient);

        object? cacheValue = null;
        _cacheMock
            .Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(false);

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FailSafe.Should().BeTrue();
        result.Error.Should().Contain("API status 500");
    }

    [Fact]
    public async Task GetStatusAsync_HttpException_ReturnsFailSafe()
    {
        // Arrange
        var user = CreateTestUser();
        var config = new PluginConfiguration
        {
            ApiUrl = "http://test.com/api",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(NamedClient.Default))
            .Returns(httpClient);

        object? cacheValue = null;
        _cacheMock
            .Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
            .Returns(false);

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FailSafe.Should().BeTrue();
        result.Error.Should().Contain("Network error");
    }

    private static User CreateTestUser()
    {
        return new User("testuser", "Test", "User");
    }
}
