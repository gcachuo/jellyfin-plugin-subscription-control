using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Configuration;
using FluentAssertions;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace EasyMovie.Plugin.IntegrationTests.Api;

/// <summary>
/// Integration tests for SubscriptionClient with real HTTP calls using WireMock
/// </summary>
public class SubscriptionClientIntegrationTests : IDisposable
{
    private readonly WireMockServer _mockServer;
    private readonly SubscriptionClient _client;
    private readonly IMemoryCache _cache;

    public SubscriptionClientIntegrationTests()
    {
        _mockServer = WireMockServer.Start();
        _cache = new MemoryCache(new MemoryCacheOptions());
        
        var httpClientFactory = new TestHttpClientFactory();
        _client = new SubscriptionClient(
            _cache,
            httpClientFactory,
            NullLogger<SubscriptionClient>.Instance);
    }

    /// <summary>
    /// Test ID: IT-SC-001
    /// Given: Mock API returns valid subscription with basic plan
    /// When: GetStatusAsync is called
    /// Then: Returns parsed subscription status with plan details
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ValidApiResponse_ParsesCorrectly()
    {
        // Arrange
        var user = new User("testuser", "Test", "User");
        var config = new PluginConfiguration
        {
            ApiUrl = _mockServer.Url + "/api/subscription",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        _mockServer
            .Given(Request.Create().WithPath("/api/subscription").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""status"": ""active"",
                    ""expirationDate"": ""2026-12-31"",
                    ""daysUntilExpiration"": 180,
                    ""email"": ""test@example.com"",
                    ""isTrial"": false,
                    ""subscriptionDuration"": 365,
                    ""failsafe"": false,
                    ""plan"": {
                        ""id"": ""basic"",
                        ""name"": ""Basic Plan"",
                        ""enableAllFolders"": false,
                        ""enabledFolderIds"": [""folder1"", ""folder2""],
                        ""allowLiveTv"": false
                    },
                    ""testMode"": false,
                    ""testUsers"": []
                }"));

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("active");
        result.FailSafe.Should().BeFalse();
        result.Plan.Should().NotBeNull();
        result.Plan!.Id.Should().Be("basic");
        result.Plan.Name.Should().Be("Basic Plan");
        result.Plan.EnableAllFolders.Should().BeFalse();
        result.Plan.EnabledFolderIds.Should().HaveCount(2);
        result.Plan.AllowLiveTv.Should().BeFalse();
    }

    /// <summary>
    /// Test ID: IT-SC-002
    /// Given: Mock API returns subscription without plan (null)
    /// When: GetStatusAsync is called
    /// Then: Returns status with null plan
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_NoPlan_ReturnsNullPlan()
    {
        // Arrange
        var user = new User("noPlanUser", "No", "Plan");
        var config = new PluginConfiguration
        {
            ApiUrl = _mockServer.Url + "/api/subscription",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        _mockServer
            .Given(Request.Create().WithPath("/api/subscription").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""status"": ""active"",
                    ""failsafe"": false,
                    ""plan"": null,
                    ""testMode"": false,
                    ""testUsers"": []
                }"));

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("active");
        result.Plan.Should().BeNull();
    }

    /// <summary>
    /// Test ID: IT-SC-003
    /// Given: Mock API returns expired status
    /// When: GetStatusAsync is called
    /// Then: Returns expired status and IsExpired is true
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ExpiredSubscription_ReturnsExpiredStatus()
    {
        // Arrange
        var user = new User("expiredUser", "Expired", "User");
        var config = new PluginConfiguration
        {
            ApiUrl = _mockServer.Url + "/api/subscription",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        _mockServer
            .Given(Request.Create().WithPath("/api/subscription").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""status"": ""expired"",
                    ""expirationDate"": ""2026-01-01"",
                    ""daysUntilExpiration"": -180,
                    ""failsafe"": false,
                    ""plan"": null,
                    ""testMode"": false,
                    ""testUsers"": []
                }"));

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("expired");
        result.IsExpired.Should().BeTrue();
        result.DaysUntilExpiration.Should().Be(-180);
    }

    /// <summary>
    /// Test ID: IT-SC-004
    /// Given: Mock API returns 500 error
    /// When: GetStatusAsync is called
    /// Then: Returns fail-safe status
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_ServerError_ReturnsFailSafe()
    {
        // Arrange
        var user = new User("testuser", "Test", "User");
        var config = new PluginConfiguration
        {
            ApiUrl = _mockServer.Url + "/api/subscription",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        _mockServer
            .Given(Request.Create().WithPath("/api/subscription").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError));

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FailSafe.Should().BeTrue();
        result.Status.Should().Be("active");
        result.Error.Should().Contain("500");
    }

    /// <summary>
    /// Test ID: IT-SC-005
    /// Given: Cache enabled and first call made
    /// When: Second call is made within cache duration
    /// Then: Returns cached value without calling API
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_CacheEnabled_UsesCachedValue()
    {
        // Arrange - Create fresh cache and client for this test
        using var freshCache = new MemoryCache(new MemoryCacheOptions());
        var freshClient = new SubscriptionClient(
            freshCache,
            new TestHttpClientFactory(),
            NullLogger<SubscriptionClient>.Instance);

        var user = new User("cachedUser", "Cached", "User");
        var config = new PluginConfiguration
        {
            ApiUrl = _mockServer.Url + "/api/subscription",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 10
        };

        _mockServer.ResetMappings();
        _mockServer
            .Given(Request.Create().WithPath("/api/subscription").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""status"": ""active"",
                    ""failsafe"": false,
                    ""plan"": null,
                    ""testMode"": false,
                    ""testUsers"": []
                }"));

        // Act
        var result1 = await freshClient.GetStatusAsync(user, config, CancellationToken.None);
        var result2 = await freshClient.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result1.Should().NotBeNull();
        result1.Status.Should().Be("active");
        
        result2.Should().NotBeNull();
        result2.Cached.Should().BeTrue("second call should return cached value");
        result2.Status.Should().Be("active");
        
        // Verify only one request was made to the server
        _mockServer.LogEntries.Should().HaveCount(1, "API should only be called once due to caching");
    }

    /// <summary>
    /// Test ID: IT-SC-006
    /// Given: Mock API returns test mode configuration
    /// When: GetStatusAsync is called
    /// Then: Returns test mode settings correctly
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_TestMode_ParsesTestConfiguration()
    {
        // Arrange
        var user = new User("trial", "Trial", "User");
        var config = new PluginConfiguration
        {
            ApiUrl = _mockServer.Url + "/api/subscription",
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        _mockServer
            .Given(Request.Create().WithPath("/api/subscription").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(@"{
                    ""status"": ""active"",
                    ""failsafe"": false,
                    ""plan"": {
                        ""id"": ""basic"",
                        ""name"": ""Basic Plan"",
                        ""enableAllFolders"": false,
                        ""enabledFolderIds"": [],
                        ""allowLiveTv"": false
                    },
                    ""testMode"": true,
                    ""testUsers"": [""trial"", ""test""]
                }"));

        // Act
        var result = await _client.GetStatusAsync(user, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TestMode.Should().BeTrue();
        result.TestUsers.Should().HaveCount(2);
        result.TestUsers.Should().Contain("trial");
        result.TestUsers.Should().Contain("test");
    }

    public void Dispose()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
        _cache?.Dispose();
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
