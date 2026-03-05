using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Spectre.Console;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    /// <summary>
    /// Service for launching and managing external process sessions.
    /// Tracks active windows and logs their closure.
    /// </summary>
    public class SessionOrchestrator(IHistoryService historyService) : ISessionOrchestrator
    {
        private readonly IHistoryService _historyService = historyService;
        private readonly ConcurrentDictionary<string, Process> _activeSessions = new();

        /// <inheritdoc />
        public void LaunchGemini(string name, string path, string args = "")
        {
            string cmdArgs = string.IsNullOrEmpty(args) ? "gemini" : $"gemini {args}";
            
            AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .Start($"[yellow]Spawning Neural Link for {name}...[/]", _ => {
                    Thread.Sleep(400);
                });

            // Launch PowerShell directly to get a reliable process handle
            var psi = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -Command \"$Host.UI.RawUI.WindowTitle = '{name} Neural Link'; Write-Host '--- NEXUS SESSION: {name} ---' -ForegroundColor Cyan; cd '{path}'; {cmdArgs}\"",
                UseShellExecute = true,
                CreateNoWindow = false
            };
            
            try 
            {
                var process = Process.Start(psi);
                if (process != null)
                {
                    _activeSessions[name] = process;
                    process.EnableRaisingEvents = true;
                    process.Exited += (s, e) => {
                        _activeSessions.TryRemove(name, out _);
                        _historyService.AddEvent($"[grey]Closed:[/] '{name}' session.");
                    };
                }
            }
            catch (Exception ex)
            {
                _historyService.AddEvent($"[red]Error:[/] Failed to launch '{name}': {ex.Message}");
            }
        }

        /// <inheritdoc />
        public bool IsSessionActive(string name)
        {
            if (_activeSessions.TryGetValue(name, out var process))
            {
                try {
                    return !process.HasExited;
                } catch {
                    _activeSessions.TryRemove(name, out _);
                    return false;
                }
            }
            return false;
        }
    }
}
