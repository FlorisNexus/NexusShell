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
                .Start($"[yellow]Initializing Neural Link for {name}...[/]", _ => {
                    Thread.Sleep(400);
                });

            // Revert: We want to open a new terminal window for the session to keep CLI context advantages.
            bool inPlace = false;

            var psi = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -Command \"$Host.UI.RawUI.WindowTitle = '{name} Neural Link'; cd '{path}'; {cmdArgs}\"",
                UseShellExecute = true, // Force new window
                CreateNoWindow = false
            };
            
            try 
            {
                var process = Process.Start(psi);
                if (process != null)
                {
                    _activeSessions[name] = process;
                    
                    if (inPlace)
                    {
                        // In-place execution: Wait for Gemini to finish before returning control to the Hub UI.
                        process.WaitForExit();
                        _activeSessions.TryRemove(name, out _);
                        _historyService.AddEvent($"[grey]Closed:[/] '{name}' session.");
                    }
                    else
                    {
                        // Background execution: Return control immediately.
                        process.EnableRaisingEvents = true;
                        process.Exited += (s, e) => {
                            _activeSessions.TryRemove(name, out _);
                            _historyService.AddEvent($"[grey]Closed:[/] '{name}' session.");
                        };
                    }
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
