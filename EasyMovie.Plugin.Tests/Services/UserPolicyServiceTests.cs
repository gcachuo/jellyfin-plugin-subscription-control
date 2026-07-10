using System;
using System.Threading.Tasks;
using EasyMovie.Plugin.Services;
using FluentAssertions;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EasyMovie.Plugin.Tests.Services;

public class UserPolicyServiceTests
{
    private readonly Mock<IUserManager> _userManagerMock;
    private readonly Mock<ILogger<UserPolicyService>> _loggerMock;
    private readonly UserPolicyService _service;

    public UserPolicyServiceTests()
    {
        _userManagerMock = new Mock<IUserManager>();
        _loggerMock = new Mock<ILogger<UserPolicyService>>();
        _service = new UserPolicyService(_userManagerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetUserPolicyAsync_ValidUser_ReturnsPolicy()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = await _service.GetUserPolicyAsync(user);

        // Assert
        result.Should().NotBeNull();
        result!.EnableAllFolders.Should().BeTrue();
        result.EnableLiveTvAccess.Should().BeFalse();
    }

    [Fact]
    public void TrySetLibraryAccess_ValidPolicy_UpdatesSuccessfully()
    {
        // Arrange
        var policy = new UserPolicy
        {
            EnableAllFolders = true,
            EnabledFolders = Array.Empty<Guid>()
        };
        var folderIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

        // Act
        var result = _service.TrySetLibraryAccess(
            policy,
            false,
            folderIds,
            out var currentEnableAll,
            out var currentFolders);

        // Assert
        result.Should().BeTrue();
        currentEnableAll.Should().BeTrue();
        policy.EnableAllFolders.Should().BeFalse();
        policy.EnabledFolders.Should().HaveCount(2);
    }

    [Fact]
    public void TrySetLibraryAccess_InvalidGuid_LogsWarning()
    {
        // Arrange
        var policy = new UserPolicy();
        var folderIds = new[] { "invalid-guid", Guid.NewGuid().ToString() };

        // Act
        var result = _service.TrySetLibraryAccess(
            policy,
            false,
            folderIds,
            out _,
            out _);

        // Assert
        result.Should().BeTrue();
        policy.EnabledFolders.Should().HaveCount(1); // Only valid GUID
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid folder ID format")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void SetLiveTvAccess_EnableTrue_SetsPermissions()
    {
        // Arrange
        var policy = new UserPolicy
        {
            EnableLiveTvAccess = false,
            EnableLiveTvManagement = false
        };

        // Act
        _service.SetLiveTvAccess(policy, true);

        // Assert
        policy.EnableLiveTvAccess.Should().BeTrue();
        policy.EnableLiveTvManagement.Should().BeTrue();
    }

    [Fact]
    public void SetLiveTvAccess_EnableFalse_RemovesPermissions()
    {
        // Arrange
        var policy = new UserPolicy
        {
            EnableLiveTvAccess = true,
            EnableLiveTvManagement = true
        };

        // Act
        _service.SetLiveTvAccess(policy, false);

        // Assert
        policy.EnableLiveTvAccess.Should().BeFalse();
        policy.EnableLiveTvManagement.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUserPolicyAsync_Success_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var policy = new UserPolicy();
        _userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, policy))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateUserPolicyAsync(user, policy);

        // Assert
        result.Should().BeTrue();
        _userManagerMock.Verify(x => x.UpdatePolicyAsync(user.Id, policy), Times.Once);
    }

    [Fact]
    public async Task UpdateUserPolicyAsync_ThrowsException_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var policy = new UserPolicy();
        _userManagerMock
            .Setup(x => x.UpdatePolicyAsync(user.Id, policy))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.UpdateUserPolicyAsync(user, policy);

        // Assert
        result.Should().BeFalse();
    }

    private static User CreateTestUser()
    {
        var user = new User("testuser", "Test", "User");
        user.SetPermission(PermissionKind.EnableAllFolders, true);
        user.SetPermission(PermissionKind.EnableLiveTvAccess, false);
        return user;
    }
}
