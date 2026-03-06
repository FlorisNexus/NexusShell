using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Implementation of the repository project scanner.
    /// </summary>
    public class ProjectService(string reposRoot, IHistoryService historyService, IContextService contextService) : IProjectService
    {
        private readonly string _reposRoot = reposRoot;
        private readonly IHistoryService _historyService = historyService;
        private readonly IContextService _contextService = contextService;

        /// <inheritdoc />
        public List<ProjectInfo> GetProjects()
        {
            if (!Directory.Exists(_reposRoot)) return new List<ProjectInfo>();
            
            var stats = _historyService.LoadStats();
            var list = new List<ProjectInfo>();
            var dirs = new DirectoryInfo(_reposRoot).GetDirectories()
                .Where(d => d.Name != "conductor" && !d.Name.StartsWith("."))
                .ToList();

            foreach (var d in dirs)
            {
                var info = new ProjectInfo { 
                    Name = d.Name, 
                    Path = d.FullName,
                    Context = _contextService.LoadContext(d.FullName)
                };
                
                if (stats.TryGetValue(d.Name, out var s))
                {
                    info.OpenCount = s.OpenCount;
                    info.LastOpened = s.LastOpened;
                }

                IdentifyProjectDetails(d.FullName, info);
                list.Add(info);
            }

            return list.OrderBy(p => GetTrackSortOrder(p.Track)).ThenBy(p => p.Name).ToList();
        }

        private int GetTrackSortOrder(string track) => track switch {
            "SAAS" => 1,
            "LOCAL" => 2,
            _ => 3
        };

        private void IdentifyProjectDetails(string path, ProjectInfo info)
        {
            // 1. Determine Repository Type (Mono/Multi)
            if (Directory.Exists(Path.Combine(path, ".git")))
            {
                info.Type = "Mono";
                GetGitInfo(path, info);
            }
            else if (Directory.GetDirectories(path, ".git", SearchOption.AllDirectories).Any())
            {
                info.Type = "Multi";
            }
            else
            {
                info.Type = "Folder";
            }

            // 2. Determine Strategic Track
            // Priority 1: Check for LOCAL markers (Vite/React/Tailwind)
            // We check this first because SaaS projects might also have a 'src' folder
            if (File.Exists(Path.Combine(path, "package.json")) || 
                File.Exists(Path.Combine(path, "vite.config.js")) ||
                File.Exists(Path.Combine(path, "vite.config.ts")) ||
                File.Exists(Path.Combine(path, "tailwind.config.js")))
            {
                info.Track = "LOCAL";
            }
            // Priority 2: Check for SAAS markers (.NET Solution, slnx, or complex src structure)
            else if (Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories).Any() || 
                     Directory.GetFiles(path, "*.slnx", SearchOption.AllDirectories).Any() ||
                     Directory.Exists(Path.Combine(path, "src")))
            {
                info.Track = "SAAS";
            }
            else
            {
                info.Track = "OTHER";
            }
        }

        /// <inheritdoc />
        public void RefreshTracks()
        {
            // Logic handled by PowerShell sync-tracks for now
        }

        private void GetGitInfo(string path, ProjectInfo info)
        {
            try {
                info.Branch = RunGit(path, "rev-parse --abbrev-ref HEAD");
                info.HasChanges = !string.IsNullOrEmpty(RunGit(path, "status --short"));

                string upstream = RunGit(path, "rev-parse --abbrev-ref --symbolic-full-name @{u}");
                if (!string.IsNullOrEmpty(upstream))
                {
                    string stats = RunGit(path, "rev-list --left-right --count HEAD...@{u}");
                    if (!string.IsNullOrEmpty(stats))
                    {
                        var parts = stats.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            int ahead = int.Parse(parts[0]);
                            int behind = int.Parse(parts[1]);
                            
                            var statusParts = new List<string>();
                            if (ahead > 0) statusParts.Add($"↑{ahead}");
                            if (behind > 0) statusParts.Add($"↓{behind}");
                            
                            info.RemoteStatus = string.Join(" ", statusParts);
                        }
                    }
                }
            } catch {}
        }

        private string RunGit(string path, string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            return process?.StandardOutput.ReadToEnd().Trim() ?? "";
        }
    }
}
