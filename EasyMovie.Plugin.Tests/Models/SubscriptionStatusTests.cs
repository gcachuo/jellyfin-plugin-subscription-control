using EasyMovie.Plugin.Models;
using FluentAssertions;
using Xunit;

namespace EasyMovie.Plugin.Tests.Models;

public class SubscriptionStatusTests
{
    /// <summary>
    /// Test ID: SS-001
    /// Given: SubscriptionStatus with various status values
    /// When: IsExpired property is accessed
    /// Then: Returns true only for "expired" (case-insensitive)
    /// </summary>
    [Theory]
    [InlineData("expired", true)]
    [InlineData("EXPIRED", true)]
    [InlineData("Expired", true)]
    [InlineData("active", false)]
    [InlineData("expiring", false)]
    public void IsExpired_VariousStatuses_ReturnsCorrectValue(string status, bool expected)
    {
        // Arrange
        var subscriptionStatus = new SubscriptionStatus { Status = status };

        // Act
        var result = subscriptionStatus.IsExpired;

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Test ID: SS-002
    /// Given: SubscriptionStatus with various status values
    /// When: IsExpiring property is accessed
    /// Then: Returns true only for "expiring" (case-insensitive)
    /// </summary>
    [Theory]
    [InlineData("expiring", true)]
    [InlineData("EXPIRING", true)]
    [InlineData("Expiring", true)]
    [InlineData("active", false)]
    [InlineData("expired", false)]
    public void IsExpiring_VariousStatuses_ReturnsCorrectValue(string status, bool expected)
    {
        // Arrange
        var subscriptionStatus = new SubscriptionStatus { Status = status };

        // Act
        var result = subscriptionStatus.IsExpiring;

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Test ID: SS-003
    /// Given: SubscriptionStatus with various status values
    /// When: IsCourtesy property is accessed
    /// Then: Returns true only for "courtesy" (case-insensitive)
    /// </summary>
    [Theory]
    [InlineData("courtesy", true)]
    [InlineData("COURTESY", true)]
    [InlineData("Courtesy", true)]
    [InlineData("active", false)]
    [InlineData("expired", false)]
    public void IsCourtesy_VariousStatuses_ReturnsCorrectValue(string status, bool expected)
    {
        // Arrange
        var subscriptionStatus = new SubscriptionStatus { Status = status };

        // Act
        var result = subscriptionStatus.IsCourtesy;

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Test ID: SS-004
    /// Given: PlanInfo with all properties set
    /// When: Properties are accessed
    /// Then: All values are correctly stored and retrieved
    /// </summary>
    [Fact]
    public void PlanInfo_AllProperties_SetCorrectly()
    {
        // Arrange & Act
        var planInfo = new PlanInfo
        {
            Id = "premium",
            Name = "Premium Plan",
            EnableAllFolders = true,
            EnabledFolderIds = new[] { "folder1", "folder2" },
            AllowLiveTv = true
        };

        // Assert
        planInfo.Id.Should().Be("premium");
        planInfo.Name.Should().Be("Premium Plan");
        planInfo.EnableAllFolders.Should().BeTrue();
        planInfo.EnabledFolderIds.Should().HaveCount(2);
        planInfo.AllowLiveTv.Should().BeTrue();
    }
}
