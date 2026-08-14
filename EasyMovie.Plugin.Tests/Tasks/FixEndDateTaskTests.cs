using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyMovie.Plugin.Tasks;
using FluentAssertions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EasyMovie.Plugin.Tests.Tasks;

public class FixEndDateTaskTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock;
    private readonly Mock<ILogger<FixEndDateTask>> _loggerMock;
    private readonly FixEndDateTask _task;

    public FixEndDateTaskTests()
    {
        _libraryManagerMock = new Mock<ILibraryManager>();
        _loggerMock = new Mock<ILogger<FixEndDateTask>>();
        _task = new FixEndDateTask(_libraryManagerMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Test ID: FED-001
    /// Given: Items with EndDate=NULL
    /// When: ExecuteAsync is called
    /// Then: EndDate is set to DateCreated for each item
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ItemsWithNullEndDate_SetsEndDateToDateCreated()
    {
        // Arrange
        var dateCreated = new DateTime(2026, 7, 16, 4, 37, 19, DateTimeKind.Utc);
        var items = new List<BaseItem>
        {
            new Video { Id = Guid.NewGuid(), Name = "Recording 1", DateCreated = dateCreated, EndDate = null },
            new Video { Id = Guid.NewGuid(), Name = "Recording 2", DateCreated = dateCreated.AddDays(1), EndDate = null }
        };

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>(), It.IsAny<bool>()))
            .Returns(items);

        // Act
        await _task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        // Assert
        items[0].EndDate.Should().Be(dateCreated);
        items[1].EndDate.Should().Be(dateCreated.AddDays(1));
        _libraryManagerMock.Verify(
            m => m.UpdateItemsAsync(
                It.Is<IReadOnlyList<BaseItem>>(l => l.Count == 2),
                It.IsAny<BaseItem>(),
                It.IsAny<ItemUpdateType>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Test ID: FED-002
    /// Given: Items that already have EndDate set
    /// When: ExecuteAsync is called
    /// Then: EndDate is not modified and UpdateItemsAsync is not called
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ItemsWithEndDate_DoesNotModify()
    {
        // Arrange
        var existingEndDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new List<BaseItem>
        {
            new Video { Id = Guid.NewGuid(), Name = "Movie 1", DateCreated = DateTime.UtcNow, EndDate = existingEndDate }
        };

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>(), It.IsAny<bool>()))
            .Returns(items);

        // Act
        await _task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        // Assert
        items[0].EndDate.Should().Be(existingEndDate);
        _libraryManagerMock.Verify(
            m => m.UpdateItemsAsync(
                It.IsAny<IReadOnlyList<BaseItem>>(),
                It.IsAny<BaseItem>(),
                It.IsAny<ItemUpdateType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Test ID: FED-003
    /// Given: Items with EndDate=9999-01-01 (sentinel for "no release date")
    /// When: ExecuteAsync is called
    /// Then: EndDate is not modified (sentinel is preserved)
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ItemsWithSentinelEndDate_DoesNotModify()
    {
        // Arrange
        var sentinel = new DateTime(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new List<BaseItem>
        {
            new Video { Id = Guid.NewGuid(), Name = "Unreleased", DateCreated = DateTime.UtcNow, EndDate = sentinel }
        };

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>(), It.IsAny<bool>()))
            .Returns(items);

        // Act
        await _task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        // Assert
        items[0].EndDate.Should().Be(sentinel);
        _libraryManagerMock.Verify(
            m => m.UpdateItemsAsync(
                It.IsAny<IReadOnlyList<BaseItem>>(),
                It.IsAny<BaseItem>(),
                It.IsAny<ItemUpdateType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Test ID: FED-004
    /// Given: A mix of items with EndDate=NULL and EndDate set
    /// When: ExecuteAsync is called
    /// Then: Only items with EndDate=NULL are fixed
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MixedItems_OnlyFixesNullEndDate()
    {
        // Arrange
        var dateCreated = new DateTime(2026, 7, 16, 4, 37, 19, DateTimeKind.Utc);
        var existingEndDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nullItem = new Video { Id = Guid.NewGuid(), Name = "Null", DateCreated = dateCreated, EndDate = null };
        var setItem = new Video { Id = Guid.NewGuid(), Name = "Set", DateCreated = dateCreated, EndDate = existingEndDate };

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>(), It.IsAny<bool>()))
            .Returns(new List<BaseItem> { nullItem, setItem });

        // Act
        await _task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        // Assert
        nullItem.EndDate.Should().Be(dateCreated);
        setItem.EndDate.Should().Be(existingEndDate);
        _libraryManagerMock.Verify(
            m => m.UpdateItemsAsync(
                It.Is<IReadOnlyList<BaseItem>>(l => l.Count == 1 && l.Contains(nullItem)),
                It.IsAny<BaseItem>(),
                It.IsAny<ItemUpdateType>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Test ID: FED-005
    /// Given: No items with EndDate=NULL
    /// When: ExecuteAsync is called
    /// Then: UpdateItemsAsync is not called
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NoItemsNeedFix_DoesNotCallUpdate()
    {
        // Arrange
        var items = new List<BaseItem>
        {
            new Video { Id = Guid.NewGuid(), Name = "OK", DateCreated = DateTime.UtcNow, EndDate = DateTime.UtcNow }
        };

        _libraryManagerMock
            .Setup(m => m.GetItemList(It.IsAny<InternalItemsQuery>(), It.IsAny<bool>()))
            .Returns(items);

        // Act
        await _task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        // Assert
        _libraryManagerMock.Verify(
            m => m.UpdateItemsAsync(
                It.IsAny<IReadOnlyList<BaseItem>>(),
                It.IsAny<BaseItem>(),
                It.IsAny<ItemUpdateType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Test ID: FED-006
    /// Given: Task metadata
    /// When: Properties are accessed
    /// Then: Returns expected values
    /// </summary>
    [Fact]
    public void Properties_ReturnExpectedValues()
    {
        _task.Name.Should().Be("EasyMovie: Fix EndDate en items sin fecha");
        _task.Key.Should().Be("EasyMovieFixEndDate");
        _task.Category.Should().Be("EasyMovie Subscription");
        _task.IsHidden.Should().BeFalse();
        _task.IsEnabled.Should().BeTrue();
        _task.IsLogged.Should().BeTrue();
    }

    /// <summary>
    /// Test ID: FED-007
    /// Given: GetDefaultTriggers is called
    /// When: Triggers are enumerated
    /// Then: Returns an interval trigger of 6 hours
    /// </summary>
    [Fact]
    public void GetDefaultTriggers_ReturnsSixHourInterval()
    {
        var triggers = _task.GetDefaultTriggers().ToList();

        triggers.Should().HaveCount(1);
        triggers[0].Type.Should().Be(TaskTriggerInfoType.IntervalTrigger);
        triggers[0].IntervalTicks.Should().Be(TimeSpan.FromHours(6).Ticks);
    }
}
