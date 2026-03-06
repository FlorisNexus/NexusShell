using System.IO;
using Xunit;
using NexusShell;
using NexusShell.Services;

namespace NexusShell.Tests
{
    public class CloudSyncServiceTests
    {
        [Fact]
        public void SyncToCloud_CopiesFilesSuccessfully()
        {
            // Arrange
            string tempRepos = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string conductorRoot = Path.Combine(tempRepos, "conductor");
            string cloudPath = Path.Combine(tempRepos, "CloudStorage_Mock");

            Directory.CreateDirectory(conductorRoot);
            File.WriteAllText(Path.Combine(conductorRoot, "test.md"), "test content");

            var settings = new NexusSettings(tempRepos, conductorRoot, "v1.0", "Admin", cloudPath);
            var service = new CloudSyncService(settings);

            // Act
            service.SyncToCloud();

            // Assert
            Assert.True(Directory.Exists(cloudPath));
            Assert.True(File.Exists(Path.Combine(cloudPath, "test.md")));
            Assert.Equal("test content", File.ReadAllText(Path.Combine(cloudPath, "test.md")));

            // Cleanup
            Directory.Delete(tempRepos, true);
        }

        [Fact]
        public void SyncFromCloud_CopiesFilesSuccessfully()
        {
            // Arrange
            string tempRepos = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string conductorRoot = Path.Combine(tempRepos, "conductor");
            string cloudPath = Path.Combine(tempRepos, "CloudStorage_Mock");

            Directory.CreateDirectory(cloudPath);
            File.WriteAllText(Path.Combine(cloudPath, "cloud_test.md"), "cloud content");

            var settings = new NexusSettings(tempRepos, conductorRoot, "v1.0", "Admin", cloudPath);
            var service = new CloudSyncService(settings);

            // Act
            service.SyncFromCloud();

            // Assert
            Assert.True(Directory.Exists(conductorRoot));
            Assert.True(File.Exists(Path.Combine(conductorRoot, "cloud_test.md")));
            Assert.Equal("cloud content", File.ReadAllText(Path.Combine(conductorRoot, "cloud_test.md")));

            // Cleanup
            Directory.Delete(tempRepos, true);
        }
    }
}
