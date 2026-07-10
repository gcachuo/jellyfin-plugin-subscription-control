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

namespace EasyMovie.Plugin.IntegrationTests.Workflows;

/// <summary>
/// Integration tests for complete library access synchronization workflow
/// </summary>
public class LibraryAccessSyncWorkflowTests
{
    /// <summary>
    /// Test ID: IT-WF-001
    /// Given: User with EnableAllFolders=true
    /// When: Policy is updated to restrict to specific folders
    /// Then: User policy is correctly updated with folder restrictions
    /// </summary>
    [Fact]
    public async Task SyncWorkflow_RestrictFromAllToSpecificFolders_UpdatesSuccessfully()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var user = CreateUser("restrictedUser");
        user.SetPermission(PermissionKind.EnableAllFolders, true);

        var folder1 = Guid.NewGuid();
        var folder2 = Guid.NewGuid();
        var folderIds = new[] { folder1.ToString(), folder2.ToString() };

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act
        var policy = await service.GetUserPolicyAsync(user);
        policy.Should().NotBeNull();

        var setResult = service.TrySetLibraryAccess(
            policy!,
            false,
            folderIds,
            out var previousEnableAll,
            out var previousFolders);

        var updateResult = await service.UpdateUserPolicyAsync(user, policy);

        // Assert
        setResult.Should().BeTrue();
        previousEnableAll.Should().BeTrue();
        previousFolders.Should().BeEmpty();

        updateResult.Should().BeTrue();
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.EnableAllFolders.Should().BeFalse();
        capturedPolicy.EnabledFolders.Should().HaveCount(2);
        capturedPolicy.EnabledFolders.Should().Contain(folder1);
        capturedPolicy.EnabledFolders.Should().Contain(folder2);
    }

    /// <summary>
    /// Test ID: IT-WF-002
    /// Given: User with specific folder access
    /// When: Policy is updated to enable all folders
    /// Then: User gains access to all folders
    /// </summary>
    [Fact]
    public async Task SyncWorkflow_ExpandFromSpecificToAllFolders_UpdatesSuccessfully()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var user = CreateUser("expandedUser");
        user.SetPermission(PermissionKind.EnableAllFolders, false);
        user.SetPreference(PreferenceKind.EnabledFolders, new[] { Guid.NewGuid(), Guid.NewGuid() });

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act
        var policy = await service.GetUserPolicyAsync(user);
        policy.Should().NotBeNull();

        var setResult = service.TrySetLibraryAccess(
            policy!,
            true,
            Array.Empty<string>(),
            out var previousEnableAll,
            out var previousFolders);

        var updateResult = await service.UpdateUserPolicyAsync(user, policy);

        // Assert
        setResult.Should().BeTrue();
        previousEnableAll.Should().BeFalse();
        previousFolders.Should().HaveCount(2);

        updateResult.Should().BeTrue();
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.EnableAllFolders.Should().BeTrue();
        capturedPolicy.EnabledFolders.Should().BeEmpty();
    }

    /// <summary>
    /// Test ID: IT-WF-003
    /// Given: User with Live TV enabled
    /// When: Policy is updated to disable Live TV
    /// Then: User loses Live TV access and management
    /// </summary>
    [Fact]
    public async Task SyncWorkflow_DisableLiveTv_RemovesAllLiveTvPermissions()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var user = CreateUser("liveTvUser");
        user.SetPermission(PermissionKind.EnableLiveTvAccess, true);
        user.SetPermission(PermissionKind.EnableLiveTvManagement, true);

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act
        var policy = await service.GetUserPolicyAsync(user);
        policy.Should().NotBeNull();
        policy!.EnableLiveTvAccess.Should().BeTrue();

        service.SetLiveTvAccess(policy, false);
        var updateResult = await service.UpdateUserPolicyAsync(user, policy);

        // Assert
        updateResult.Should().BeTrue();
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.EnableLiveTvAccess.Should().BeFalse();
        capturedPolicy.EnableLiveTvManagement.Should().BeFalse();
    }

    /// <summary>
    /// Test ID: IT-WF-004
    /// Given: User with no Live TV access
    /// When: Policy is updated to enable Live TV
    /// Then: User gains both Live TV access and management
    /// </summary>
    [Fact]
    public async Task SyncWorkflow_EnableLiveTv_GrantsAllLiveTvPermissions()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var user = CreateUser("noLiveTvUser");
        user.SetPermission(PermissionKind.EnableLiveTvAccess, false);
        user.SetPermission(PermissionKind.EnableLiveTvManagement, false);

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act
        var policy = await service.GetUserPolicyAsync(user);
        policy.Should().NotBeNull();
        policy!.EnableLiveTvAccess.Should().BeFalse();

        service.SetLiveTvAccess(policy, true);
        var updateResult = await service.UpdateUserPolicyAsync(user, policy);

        // Assert
        updateResult.Should().BeTrue();
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.EnableLiveTvAccess.Should().BeTrue();
        capturedPolicy.EnableLiveTvManagement.Should().BeTrue();
    }

    /// <summary>
    /// Test ID: IT-WF-005
    /// Given: User with basic plan (restricted folders, no Live TV)
    /// When: Complete sync workflow is executed
    /// Then: All permissions are correctly applied
    /// </summary>
    [Fact]
    public async Task SyncWorkflow_CompleteBasicPlanSync_AppliesAllRestrictions()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var user = CreateUser("basicPlanUser");
        user.SetPermission(PermissionKind.EnableAllFolders, true);
        user.SetPermission(PermissionKind.EnableLiveTvAccess, true);

        var allowedFolder1 = Guid.NewGuid();
        var allowedFolder2 = Guid.NewGuid();
        var folderIds = new[] { allowedFolder1.ToString(), allowedFolder2.ToString() };

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act - Simulate complete plan sync
        var policy = await service.GetUserPolicyAsync(user);
        policy.Should().NotBeNull();

        // Apply library restrictions
        service.TrySetLibraryAccess(policy!, false, folderIds, out _, out _);
        
        // Apply Live TV restriction
        service.SetLiveTvAccess(policy!, false);

        // Persist changes
        var updateResult = await service.UpdateUserPolicyAsync(user, policy!);

        // Assert
        updateResult.Should().BeTrue();
        capturedPolicy.Should().NotBeNull();
        
        // Verify library restrictions
        capturedPolicy!.EnableAllFolders.Should().BeFalse();
        capturedPolicy.EnabledFolders.Should().HaveCount(2);
        capturedPolicy.EnabledFolders.Should().Contain(allowedFolder1);
        capturedPolicy.EnabledFolders.Should().Contain(allowedFolder2);
        
        // Verify Live TV restrictions
        capturedPolicy.EnableLiveTvAccess.Should().BeFalse();
        capturedPolicy.EnableLiveTvManagement.Should().BeFalse();
    }

    /// <summary>
    /// Test ID: IT-WF-006
    /// Given: Invalid GUID in folder list
    /// When: TrySetLibraryAccess is called
    /// Then: Invalid GUIDs are skipped, valid ones are processed
    /// </summary>
    [Fact]
    public async Task SyncWorkflow_MixedValidInvalidGuids_ProcessesOnlyValid()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var user = CreateUser("mixedGuidsUser");
        var validFolder = Guid.NewGuid();
        var folderIds = new[] 
        { 
            "invalid-guid-1",
            validFolder.ToString(),
            "not-a-guid",
            Guid.NewGuid().ToString()
        };

        // Act
        var policy = await service.GetUserPolicyAsync(user);
        var result = service.TrySetLibraryAccess(
            policy!,
            false,
            folderIds,
            out _,
            out _);

        // Assert
        result.Should().BeTrue();
        policy!.EnabledFolders.Should().HaveCount(2, "only 2 valid GUIDs should be processed");
        policy.EnabledFolders.Should().Contain(validFolder);
    }

    /// <summary>
    /// Test ID: IT-WF-007
    /// Given: User without plan (trial/new user)
    /// When: Sync is executed
    /// Then: User gets full access (EnableAllFolders=true, LiveTV=true)
    /// 
    /// Users without a plan should have full access to try the service.
    /// Restrictions only apply when they have a specific plan.
    /// </summary>
    [Fact]
    public async Task SyncWorkflow_UserWithoutPlan_GetsFullAccess()
    {
        // Arrange
        var userManagerMock = new Mock<IUserManager>();
        var service = new UserPolicyService(
            userManagerMock.Object,
            NullLogger<UserPolicyService>.Instance);

        var user = CreateUser("trialUser");
        // User currently has restricted access
        user.SetPermission(PermissionKind.EnableAllFolders, false);
        user.SetPermission(PermissionKind.EnableLiveTvAccess, false);

        UserPolicy? capturedPolicy = null;
        userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, It.IsAny<UserPolicy>()))
            .Callback<Guid, UserPolicy>((_, policy) => capturedPolicy = policy)
            .Returns(Task.CompletedTask);

        // Act - Simulate user without plan (trial mode)
        var policy = await service.GetUserPolicyAsync(user);
        
        // Grant full access (no plan = trial = full access)
        service.TrySetLibraryAccess(policy!, true, Array.Empty<string>(), out _, out _);
        service.SetLiveTvAccess(policy!, true);
        
        await service.UpdateUserPolicyAsync(user, policy!);

        // Assert
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.EnableAllFolders.Should().BeTrue("trial users should have access to all folders");
        capturedPolicy.EnabledFolders.Should().BeEmpty("when EnableAllFolders is true, specific folders list should be empty");
        capturedPolicy.EnableLiveTvAccess.Should().BeTrue("trial users should have Live TV access");
    }

    private static User CreateUser(string username)
    {
        return new User(username, "Test", "User");
    }
}
