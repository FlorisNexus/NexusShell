using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NexusShell.Interfaces;
using NexusShell.Models;
using NexusShell.Services;
using Xunit;

namespace NexusShell.Tests
{
    public class ProjectServiceTests : IDisposable
    {
        private readonly string _tempRepos;
        private readonly Mock<IHistoryService> _historyMock;

        public ProjectServiceTests()
        {
            _tempRepos = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempRepos);
            _historyMock = new Mock<IHistoryService>();
            _historyMock.Setup(h => h.LoadStats()).Returns(new Dictionary<string, ProjectStats>());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRepos))
            {
                Directory.Delete(_tempRepos, true);
            }
        }

        [Fact]
        public void GetProjects_ShouldExcludeDotFolders()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_tempRepos, ".git"));
            Directory.CreateDirectory(Path.Combine(_tempRepos, "ProjectA"));
            Directory.CreateDirectory(Path.Combine(_tempRepos, "conductor"));
            var service = new ProjectService(_tempRepos, _historyMock.Object);

            // Act
            var projects = service.GetProjects();

            // Assert
            projects.Should().HaveCount(1);
            projects[0].Name.Should().Be("ProjectA");
        }

        [Fact]
        public void GetProjects_ShouldIdentifyMonoRepo()
        {
            // Arrange
            var projectDir = Path.Combine(_tempRepos, "ProjectA");
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(Path.Combine(projectDir, ".git"));
            var service = new ProjectService(_tempRepos, _historyMock.Object);

            // Act
            var projects = service.GetProjects();

            // Assert
            projects.Should().HaveCount(1);
            projects[0].Type.Should().Be("Mono");
        }

        [Fact]
        public void GetProjects_ShouldLoadStats()
        {
            // Arrange
            var projectName = "ProjectA";
            Directory.CreateDirectory(Path.Combine(_tempRepos, projectName));
            
            var stats = new Dictionary<string, ProjectStats> {
                { projectName, new ProjectStats { OpenCount = 42, LastOpened = DateTime.Now } }
            };
            _historyMock.Setup(h => h.LoadStats()).Returns(stats);
            
            var service = new ProjectService(_tempRepos, _historyMock.Object);

            // Act
            var projects = service.GetProjects();

            // Assert
            projects.Should().HaveCount(1);
            projects[0].OpenCount.Should().Be(42);
        }
    }
}
