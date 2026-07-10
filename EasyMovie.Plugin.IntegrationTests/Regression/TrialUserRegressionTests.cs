using System;
using System.Linq;
using System.Threading.Tasks;
using EasyMovie.Plugin.Services;
using FluentAssertions;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyMovie.Plugin.IntegrationTests.Regression;

/// <summary>
/// Regression tests to ensure trial user restrictions are maintained
/// These tests verify critical business rules that must never break
/// </summary>
public class TrialUserRegressionTests
{
    /// <summary>
    /// Test ID: REG-001
    /// Given: Trial user with basic plan (restricted folders, no Live TV)
    /// When: User policy is synced
    /// Then: User has ONLY access to specified folders and NO Live TV
    /// 
    /// Regression: Ensures trial users cannot access all content
    /// </summary>
    [Fact]
    public async Task TrialUser_BasicPlan_HasRestrictedAccess()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var trialUser = CreateUser("trial");
        
        // Trial user initially has full access (before sync)
        trialUser.SetPermission(PermissionKind.EnableAllFolders, true);
        trialUser.SetPermission(PermissionKind.EnableLiveTvAccess, true);

        // Basic plan: only 2 specific folders, no Live TV
        var allowedFolder1 = Guid.Parse("ed2a25286c558a96e1424971742ca250"); // Películas
        var allowedFolder2 = Guid.Parse("c63030aa0004c37f5a156a12ede98fc0"); // Series en Español
        var folderIds = new[] { allowedFolder1.ToString(), allowedFolder2.ToString() };

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(trialUser.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act - Simulate plan sync
        var policy = await service.GetUserPolicyAsync(trialUser);
        service.TrySetLibraryAccess(policy!, false, folderIds, out _, out _);
        service.SetLiveTvAccess(policy!, false);
        await service.UpdateUserPolicyAsync(trialUser, policy!);

        // Assert - Critical restrictions
        capturedPolicy.Should().NotBeNull();
        
        // CRITICAL: Must NOT have access to all folders
        capturedPolicy!.EnableAllFolders.Should().BeFalse(
            "trial users must be restricted to specific folders");
        
        // CRITICAL: Must have EXACTLY 2 folders
        capturedPolicy.EnabledFolders.Should().HaveCount(2,
            "trial users should only access Películas and Series en Español");
        capturedPolicy.EnabledFolders.Should().Contain(allowedFolder1);
        capturedPolicy.EnabledFolders.Should().Contain(allowedFolder2);
        
        // CRITICAL: Must NOT have Live TV access
        capturedPolicy.EnableLiveTvAccess.Should().BeFalse(
            "trial users must not have Live TV access");
        capturedPolicy.EnableLiveTvManagement.Should().BeFalse(
            "trial users must not manage Live TV");
    }

    /// <summary>
    /// Test ID: REG-002
    /// Given: Trial user attempts to access restricted folder
    /// When: Policy is checked
    /// Then: Folder is NOT in enabled folders list
    /// 
    /// Regression: Prevents trial users from accessing premium content
    /// </summary>
    [Fact]
    public async Task TrialUser_CannotAccessRestrictedFolders()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var trialUser = CreateUser("trial");
        
        // Allowed folders for trial
        var allowedFolder1 = Guid.Parse("ed2a25286c558a96e1424971742ca250");
        var allowedFolder2 = Guid.Parse("c63030aa0004c37f5a156a12ede98fc0");
        
        // Restricted folders (premium content)
        var restrictedFolder1 = Guid.NewGuid(); // English content
        var restrictedFolder2 = Guid.NewGuid(); // 4K content
        var restrictedFolder3 = Guid.NewGuid(); // Anime

        var folderIds = new[] { allowedFolder1.ToString(), allowedFolder2.ToString() };

        // Act
        var policy = await service.GetUserPolicyAsync(trialUser);
        service.TrySetLibraryAccess(policy!, false, folderIds, out _, out _);

        // Assert - Restricted folders must NOT be accessible
        policy!.EnabledFolders.Should().NotContain(restrictedFolder1,
            "trial users cannot access English content");
        policy.EnabledFolders.Should().NotContain(restrictedFolder2,
            "trial users cannot access 4K content");
        policy.EnabledFolders.Should().NotContain(restrictedFolder3,
            "trial users cannot access Anime content");
        
        // Only allowed folders should be present
        policy.EnabledFolders.Should().HaveCount(2);
        policy.EnabledFolders.Should().OnlyContain(id => 
            id == allowedFolder1 || id == allowedFolder2);
    }

    /// <summary>
    /// Test ID: REG-003
    /// Given: Trial user with Live TV initially enabled
    /// When: Basic plan is applied
    /// Then: Live TV access is removed
    /// 
    /// Regression: Ensures Live TV restriction is enforced for trial users
    /// </summary>
    [Fact]
    public async Task TrialUser_LiveTvAccessRemoved_WhenBasicPlanApplied()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var trialUser = CreateUser("trial");
        trialUser.SetPermission(PermissionKind.EnableLiveTvAccess, true);
        trialUser.SetPermission(PermissionKind.EnableLiveTvManagement, true);

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(trialUser.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act
        var policy = await service.GetUserPolicyAsync(trialUser);
        
        // Verify initial state
        policy!.EnableLiveTvAccess.Should().BeTrue("initially enabled");
        
        // Apply basic plan restriction
        service.SetLiveTvAccess(policy, false);
        await service.UpdateUserPolicyAsync(trialUser, policy);

        // Assert
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.EnableLiveTvAccess.Should().BeFalse(
            "Live TV access must be removed for trial users");
        capturedPolicy.EnableLiveTvManagement.Should().BeFalse(
            "Live TV management must be removed for trial users");
    }

    /// <summary>
    /// Test ID: REG-004
    /// Given: Trial user policy update fails
    /// When: UpdateUserPolicyAsync is called
    /// Then: Returns false and logs error
    /// 
    /// Regression: Ensures errors don't silently fail for trial users
    /// </summary>
    [Fact]
    public async Task TrialUser_PolicyUpdateFailure_ReturnsfalseAndLogs()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var trialUser = CreateUser("trial");
        var policy = new UserPolicy();

        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(trialUser.Id, It.IsAny<UserPolicy>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await service.UpdateUserPolicyAsync(trialUser, policy);

        // Assert
        result.Should().BeFalse(
            "policy update failure must be reported for trial users");
    }

    /// <summary>
    /// Test ID: REG-005
    /// Given: Trial user with EnableAllFolders = true
    /// When: Basic plan sync occurs
    /// Then: EnableAllFolders is set to false
    /// 
    /// Regression: Critical - prevents trial users from bypassing folder restrictions
    /// </summary>
    [Fact]
    public async Task TrialUser_EnableAllFolders_MustBeSetToFalse()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var trialUser = CreateUser("trial");
        trialUser.SetPermission(PermissionKind.EnableAllFolders, true);

        var allowedFolders = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(trialUser.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act
        var policy = await service.GetUserPolicyAsync(trialUser);
        
        // Verify dangerous initial state
        policy!.EnableAllFolders.Should().BeTrue("dangerous initial state");
        
        // Apply restriction
        service.TrySetLibraryAccess(policy, false, allowedFolders, out var previousValue, out _);
        await service.UpdateUserPolicyAsync(trialUser, policy);

        // Assert
        previousValue.Should().BeTrue("captured previous dangerous state");
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.EnableAllFolders.Should().BeFalse(
            "CRITICAL: EnableAllFolders must be false for trial users to prevent unrestricted access");
    }

    /// <summary>
    /// Test ID: REG-006
    /// Given: Trial user with empty folder list
    /// When: Policy is applied
    /// Then: User has no folder access (effectively blocked)
    /// 
    /// Regression: Ensures trial users without folders cannot access content
    /// </summary>
    [Fact]
    public async Task TrialUser_EmptyFolderList_HasNoAccess()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var trialUser = CreateUser("trial");
        var emptyFolders = Array.Empty<string>();

        // Act
        var policy = await service.GetUserPolicyAsync(trialUser);
        service.TrySetLibraryAccess(policy!, false, emptyFolders, out _, out _);

        // Assert
        policy!.EnableAllFolders.Should().BeFalse();
        policy.EnabledFolders.Should().BeEmpty(
            "trial user with no folders should have no content access");
    }

    private static User CreateUser(string username)
    {
        return new User(username, "Trial", "User");
    }
}
