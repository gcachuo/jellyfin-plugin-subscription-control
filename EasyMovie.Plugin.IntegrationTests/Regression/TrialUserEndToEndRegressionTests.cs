using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Api;
using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Services;
using FluentAssertions;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyMovie.Plugin.IntegrationTests.Regression;

/// <summary>
/// End-to-end regression tests using REAL API
/// These tests verify the complete flow with production API
/// 
/// IMPORTANT: Set environment variable to run these tests:
/// export EASYMOVIE_API_URL="https://easymovie.lat/api/subscription.php"
/// 
/// These tests are SKIPPED if the environment variable is not set
/// </summary>
public class TrialUserEndToEndRegressionTests : IDisposable
{
    private readonly string? _apiUrl;
    private readonly IMemoryCache _cache;
    private readonly SubscriptionClient _subscriptionClient;
    private readonly UserPolicyService _policyService;
    private readonly Mock<IUserManager> _userManagerMock;

    public TrialUserEndToEndRegressionTests()
    {
        _apiUrl = Environment.GetEnvironmentVariable("EASYMOVIE_API_URL");
        _cache = new MemoryCache(new MemoryCacheOptions());
        
        var httpClientFactory = new RealHttpClientFactory();
        _subscriptionClient = new SubscriptionClient(
            _cache,
            httpClientFactory,
            NullLogger<SubscriptionClient>.Instance);

        _userManagerMock = new Mock<IUserManager>();
        _policyService = new UserPolicyService(
            _userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);
    }

    /// <summary>
    /// Test ID: E2E-REG-001
    /// Given: Real API with trial user
    /// When: Complete sync flow is executed
    /// Then: Trial user has correct restrictions from real API
    /// 
    /// This is a CRITICAL end-to-end test that verifies the entire system
    /// works correctly with production data
    /// </summary>
    [Fact]
    public async Task E2E_TrialUser_RealAPI_HasCorrectRestrictions()
    {
        // Skip if API URL not configured
        if (string.IsNullOrEmpty(_apiUrl))
        {
            // This is expected in CI/CD - test is skipped
            return;
        }

        // Arrange
        var trialUser = CreateUser("trial");
        trialUser.SetPermission(PermissionKind.EnableAllFolders, true);
        trialUser.SetPermission(PermissionKind.EnableLiveTvAccess, true);

        var config = new PluginConfiguration
        {
            ApiUrl = _apiUrl,
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0, // No cache for regression tests
            ExpiringThresholdDays = 7,
            TrialMaxDurationDays = 14
        };

        UserPolicy? capturedPolicy = null;
        _userManagerMock
            .Setup(x => x.UpdatePolicyAsync(trialUser.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act - Execute complete flow with REAL API
        var status = await _subscriptionClient.GetStatusAsync(trialUser, config, CancellationToken.None);
        
        // Verify API returned data
        status.Should().NotBeNull("API should return subscription status");
        status.FailSafe.Should().BeFalse("API should be reachable");

        if (status.Plan == null)
        {
            // User has no plan - this is valid, skip rest of test
            return;
        }

        // Apply plan restrictions
        var policy = await _policyService.GetUserPolicyAsync(trialUser);
        if (policy is null)
        {
            throw new Xunit.Sdk.XunitException("User policy is null");
        }
        _policyService.TrySetLibraryAccess(
            policy,
            status.Plan.EnableAllFolders,
            status.Plan.EnabledFolderIds ?? Array.Empty<string>(),
            out _,
            out _);
        _policyService.SetLiveTvAccess(policy, status.Plan.AllowLiveTv);
        await _policyService.UpdateUserPolicyAsync(trialUser, policy);

        // Assert - Verify REAL restrictions from production API
        capturedPolicy.Should().NotBeNull();
        
        // CRITICAL: Trial user from real API should have restrictions
        capturedPolicy!.EnableAllFolders.Should().BeFalse(
            "trial user from real API must not have access to all folders");
        
        capturedPolicy.EnabledFolders.Should().NotBeEmpty(
            "trial user should have at least some folders enabled");
        
        capturedPolicy.EnableLiveTvAccess.Should().BeFalse(
            "trial user from real API must not have Live TV access");
    }

    /// <summary>
    /// Test ID: E2E-REG-002
    /// Given: Real API endpoint
    /// When: Trial user subscription is fetched
    /// Then: API returns valid plan structure
    /// 
    /// Verifies API contract hasn't changed
    /// </summary>
    [Fact]
    public async Task E2E_TrialUser_RealAPI_ReturnsValidPlanStructure()
    {
        // Skip if API URL not configured
        if (string.IsNullOrEmpty(_apiUrl))
        {
            return;
        }

        // Arrange
        var trialUser = CreateUser("trial");
        var config = new PluginConfiguration
        {
            ApiUrl = _apiUrl,
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        // Act
        var status = await _subscriptionClient.GetStatusAsync(trialUser, config, CancellationToken.None);

        // Assert - Verify API contract
        status.Should().NotBeNull();
        status.Status.Should().NotBeNullOrEmpty("API must return status");
        
        if (status.Plan != null)
        {
            // If plan exists, verify structure
            status.Plan.Id.Should().NotBeNullOrEmpty("plan must have ID");
            status.Plan.Name.Should().NotBeNullOrEmpty("plan must have name");
            status.Plan.EnabledFolderIds.Should().NotBeNull("plan must have folder IDs array");
            
            // Verify boolean flags have values (they are bool, not bool?)
            // Just access them to ensure they exist
            var _ = status.Plan.EnableAllFolders;
            var __ = status.Plan.AllowLiveTv;
        }
    }

    /// <summary>
    /// Test ID: E2E-REG-003
    /// Given: Real API with trial user
    /// When: Multiple requests are made
    /// Then: API responses are consistent
    /// 
    /// Verifies API stability and consistency
    /// </summary>
    [Fact]
    public async Task E2E_TrialUser_RealAPI_ConsistentResponses()
    {
        // Skip if API URL not configured
        if (string.IsNullOrEmpty(_apiUrl))
        {
            return;
        }

        // Arrange
        var trialUser = CreateUser("trial");
        var config = new PluginConfiguration
        {
            ApiUrl = _apiUrl,
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0 // No cache to test API directly
        };

        // Act - Make multiple requests
        var status1 = await _subscriptionClient.GetStatusAsync(trialUser, config, CancellationToken.None);
        await Task.Delay(100); // Small delay between requests
        var status2 = await _subscriptionClient.GetStatusAsync(trialUser, config, CancellationToken.None);

        // Assert - Responses should be consistent
        status1.Should().NotBeNull();
        status2.Should().NotBeNull();
        
        status1.Status.Should().Be(status2.Status, "API should return consistent status");
        
        if (status1.Plan != null && status2.Plan != null)
        {
            status1.Plan.Id.Should().Be(status2.Plan.Id, "plan ID should be consistent");
            status1.Plan.AllowLiveTv.Should().Be(status2.Plan.AllowLiveTv, "Live TV setting should be consistent");
            status1.Plan.EnableAllFolders.Should().Be(status2.Plan.EnableAllFolders, "folder setting should be consistent");
        }
    }

    /// <summary>
    /// Test ID: E2E-REG-004
    /// Given: Real API
    /// When: Request times out or fails
    /// Then: Fail-safe mode activates correctly
    /// 
    /// Verifies error handling with real network conditions
    /// </summary>
    [Fact]
    public async Task E2E_RealAPI_Timeout_ActivatesFailSafe()
    {
        // Skip if API URL not configured
        if (string.IsNullOrEmpty(_apiUrl))
        {
            return;
        }

        // Arrange - Very short timeout to force failure
        var trialUser = CreateUser("trial");
        var config = new PluginConfiguration
        {
            ApiUrl = _apiUrl,
            ApiTimeoutSeconds = 1, // Very short timeout
            CacheDurationMinutes = 0
        };

        // Act
        var status = await _subscriptionClient.GetStatusAsync(trialUser, config, CancellationToken.None);

        // Assert - Should activate fail-safe (or succeed if API is very fast)
        status.Should().NotBeNull();
        
        if (status.FailSafe)
        {
            // Fail-safe activated - this is expected with short timeout
            status.Status.Should().Be("active", "fail-safe should default to active");
            status.Error.Should().NotBeNullOrEmpty("fail-safe should include error message");
        }
        else
        {
            // API responded within 1 second - also valid
            status.Status.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// Test ID: E2E-REG-005
    /// Given: Production API
    /// When: Subscription status is fetched
    /// Then: TestMode is FALSE (production must not be in test mode)
    /// 
    /// CRITICAL: Prevents releasing with test mode enabled
    /// This would allow unauthorized users to bypass restrictions
    /// </summary>
    [Fact]
    public async Task E2E_ProductionAPI_MustNotBeInTestMode()
    {
        // Skip if API URL not configured
        if (string.IsNullOrEmpty(_apiUrl))
        {
            return;
        }

        // Arrange
        var trialUser = CreateUser("trial");
        var config = new PluginConfiguration
        {
            ApiUrl = _apiUrl,
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        // Act
        var status = await _subscriptionClient.GetStatusAsync(trialUser, config, CancellationToken.None);

        // Assert - CRITICAL SECURITY CHECK
        status.Should().NotBeNull();
        
        // CRITICAL: API must be reachable when testing production
        status.FailSafe.Should().BeFalse(
            "CRITICAL: Cannot verify testMode because API is not reachable! " +
            "Fail-safe mode is active. Check API URL and network connectivity. " +
            "This test MUST connect to real API to verify testMode is disabled.");
        
        // CRITICAL: TestMode must be false in production
        status.TestMode.Should().BeFalse(
            "CRITICAL: Production API must NOT be in test mode! " +
            "Test mode allows unauthorized users to bypass subscription restrictions. " +
            "This must be disabled before release.");
        
        // Additional verification: if test mode is somehow true, verify test users list
        if (status.TestMode)
        {
            // This should never happen in production
            status.TestUsers.Should().BeEmpty(
                "If test mode is enabled (which it shouldn't be), at least test users list should be empty");
        }
    }

    /// <summary>
    /// Test ID: E2E-REG-006
    /// Given: Real API with trial user having basic plan
    /// When: Folder IDs are retrieved from API
    /// Then: Folder IDs are valid GUIDs
    /// 
    /// Verifies data quality from production API
    /// </summary>
    [Fact]
    public async Task E2E_TrialUser_RealAPI_FolderIdsAreValidGuids()
    {
        // Skip if API URL not configured
        if (string.IsNullOrEmpty(_apiUrl))
        {
            return;
        }

        // Arrange
        var trialUser = CreateUser("trial");
        var config = new PluginConfiguration
        {
            ApiUrl = _apiUrl,
            ApiTimeoutSeconds = 30,
            CacheDurationMinutes = 0
        };

        // Act
        var status = await _subscriptionClient.GetStatusAsync(trialUser, config, CancellationToken.None);

        // Assert
        if (status.Plan?.EnabledFolderIds != null)
        {
            foreach (var folderId in status.Plan.EnabledFolderIds)
            {
                Guid.TryParse(folderId, out var guid).Should().BeTrue(
                    $"folder ID '{folderId}' from real API should be a valid GUID");
                guid.Should().NotBeEmpty("parsed GUID should not be empty");
            }
        }
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    private static User CreateUser(string username)
    {
        return new User(username, "Trial", "User");
    }

    private class RealHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
