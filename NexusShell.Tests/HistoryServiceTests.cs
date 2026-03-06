using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using NexusShell.Models;
using NexusShell.Services;
using Xunit;

namespace NexusShell.Tests
{
    public class HistoryServiceTests : IDisposable
    {
        private readonly string _tempPath;

        public HistoryServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
        }

        [Fact]
        public void LoadStats_ShouldReturnEmptyDictionary_WhenFileDoesNotExist()
        {
            // Arrange
            var service = new HistoryService(_tempPath);

            // Act
            var stats = service.LoadStats();

            // Assert
            stats.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void RecordLaunch_ShouldCreateFileAndIncrementCount()
        {
            // Arrange
            var service = new HistoryService(_tempPath);
            var projectName = "TestProject";

            // Act
            service.RecordLaunch(projectName);
            var stats = service.LoadStats();

            // Assert
            stats.Should().ContainKey(projectName);
            stats[projectName].OpenCount.Should().Be(1);
            stats[projectName].LastOpened.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void RecordLaunch_ShouldPersistAcrossInstances()
        {
            // Arrange
            var service1 = new HistoryService(_tempPath);
            var projectName = "TestProject";
            service1.RecordLaunch(projectName);

            // Act
            var service2 = new HistoryService(_tempPath);
            var stats = service2.LoadStats();

            // Assert
            stats.Should().ContainKey(projectName);
            stats[projectName].OpenCount.Should().Be(1);
        }

        [Fact]
        public void ConversationTurn_ShouldRoundTripJson()
        {
            var turn = new ConversationTurn { Role = "user", Content = "hello", Timestamp = new DateTime(2026, 3, 5, 10, 0, 0) };
            var json = System.Text.Json.JsonSerializer.Serialize(turn);
            var result = System.Text.Json.JsonSerializer.Deserialize<ConversationTurn>(json);
            result.Should().NotBeNull();
            result!.Role.Should().Be("user");
            result.Content.Should().Be("hello");
            result.Timestamp.Should().Be(new DateTime(2026, 3, 5, 10, 0, 0));
        }
    }
}
