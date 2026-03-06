using System;
using System.IO;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    public class CloudSyncService(NexusSettings settings) : ICloudSyncService
    {
        private readonly string _localPath = settings.ConductorRoot;
        private readonly string _cloudPath = settings.CloudSyncPath;

        public void SyncToCloud()
        {
            if (!Directory.Exists(_localPath)) return;
            CopyDirectory(_localPath, _cloudPath);
        }

        public void SyncFromCloud()
        {
            if (!Directory.Exists(_cloudPath)) return;
            CopyDirectory(_cloudPath, _localPath);
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string targetPath = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, targetPath, true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string targetDir = Path.Combine(destinationDir, Path.GetFileName(directory));
                CopyDirectory(directory, targetDir);
            }
        }
    }
}
