using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Spectre.Console;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Advanced implementation of the Nexus Command Center using a non-blocking Live UI engine.
    /// Supports real-time dynamic updates even when the user is idle.
    /// </summary>
    public class UserInterface(
        string reposRoot,
        string conductorRoot,
        IProjectService projectService,
        IHistoryService historyService,
        ISessionOrchestrator sessionOrchestrator,
        IMarketingService marketingService,
        IJournalService journalService,
        INewProjectService newProjectService,
        ILayoutService layoutService) : IUserInterface
    {
        private int _selectedIndex = 0;
        private List<string> _flatMenu = new();
        private List<ProjectInfo> _currentProjects = new();
        private bool _needsRedraw = true;
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(3);

        /// <inheritdoc />
        public void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "FLORISNEXUS AI-OS (Live Engine)";
            Console.CursorVisible = false;

            while (true)
            {
                try {
                    // 1. Check if we need to refresh due to time or external events
                    if (DateTime.Now - _lastRefresh > _refreshInterval)
                    {
                        _currentProjects = projectService.GetProjects();
                        _lastRefresh = DateTime.Now;
                        _needsRedraw = true;
                    }

                    // 2. Redraw UI if needed
                    if (_needsRedraw)
                    {
                        RenderDashboard();
                        _needsRedraw = false;
                    }

                    // 3. Non-blocking input check
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        HandleInput(key);
                    }
                    else
                    {
                        // Small sleep to prevent high CPU usage
                        Thread.Sleep(50);
                    }
                } catch (Exception ex) {
                    AnsiConsole.WriteException(ex);
                    Console.WriteLine("System Error. Press any key to reboot...");
                    Console.ReadKey();
                    _needsRedraw = true;
                }
            }
        }

        private void RenderDashboard()
        {
            Console.Clear();
            layoutService.RefreshHeader();
            ShowHistory();

            var menu = new List<string> { 
                "⚡ META-WORKSPACE (UNIFIED)", 
                "📢 MARKETING ASSISTANT", 
                "📔 FOUNDER JOURNAL", 
                "🏗️ NEW PROJECT", 
                "🛠️ EVOLVE NEXUS HUB",
                "📖 HELP & DOCUMENTATION",
                "⚙️ SYSTEM MAINTENANCE",
                "🔌 EXIT SHELL" 
            };

            var saasProjects = _currentProjects.Where(p => p.Track == "SAAS").ToList();
            var localProjects = _currentProjects.Where(p => p.Track == "LOCAL").ToList();
            var otherProjects = _currentProjects.Where(p => p.Track == "OTHER").ToList();

            // Build flat list for navigation
            _flatMenu = new List<string>(menu);
            foreach(var p in saasProjects) _flatMenu.Add("PROJ:" + p.Name);
            foreach(var p in localProjects) _flatMenu.Add("PROJ:" + p.Name);
            foreach(var p in otherProjects) _flatMenu.Add("PROJ:" + p.Name);

            // Ensure selection stays within bounds if projects changed
            if (_selectedIndex >= _flatMenu.Count) _selectedIndex = 0;

            // DRAW MENU
            AnsiConsole.MarkupLine("[bold grey]» SELECT OPERATION OR NEURAL TRACK (Live Update Active)[/]");
            
            // Core Operations
            for (int i = 0; i < menu.Count; i++)
            {
                DrawMenuItem(menu[i], i);
            }

            // Project Groups
            DrawGroup("🌍 GLOBAL SAAS TRACK", saasProjects, menu.Count);
            DrawGroup("🏗️ LOCAL HERO TRACK", localProjects, menu.Count + saasProjects.Count);
            DrawGroup("📂 OTHER TRACKS", otherProjects, menu.Count + saasProjects.Count + localProjects.Count);
            
            AnsiConsole.MarkupLine("\n[dim grey]Use Arrow Keys to navigate, Enter to launch. Auto-sync every 3s.[/]");
        }

        private void DrawMenuItem(string label, int index)
        {
            string color = label.Contains("META") ? "magenta" : 
                          label.Contains("EXIT") ? "red" : 
                          label.Contains("EVOLVE") ? "yellow" : "cyan";

            if (index == _selectedIndex)
                AnsiConsole.MarkupLine($"  [bold {color}]>[/] [{color} invert]{label}[/]");
            else
                AnsiConsole.MarkupLine($"    [{color}]{label}[/]");
        }

        private void DrawGroup(string header, List<ProjectInfo> projects, int startIndex)
        {
            if (!projects.Any()) return;
            AnsiConsole.MarkupLine($"\n  [bold white]{header}[/]");
            for (int i = 0; i < projects.Count; i++)
            {
                int globalIndex = startIndex + i;
                var p = projects[i];
                string displayName = GetProjectDisplayName(p);
                
                if (globalIndex == _selectedIndex)
                    AnsiConsole.MarkupLine($"    [green]>[/] [white invert]{displayName}[/]");
                else
                    AnsiConsole.MarkupLine($"      {displayName}");
            }
        }

        private void HandleInput(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _selectedIndex = (_selectedIndex - 1 + _flatMenu.Count) % _flatMenu.Count;
                    _needsRedraw = true;
                    break;
                case ConsoleKey.DownArrow:
                    _selectedIndex = (_selectedIndex + 1) % _flatMenu.Count;
                    _needsRedraw = true;
                    break;
                case ConsoleKey.Enter:
                    ExecuteSelection();
                    _needsRedraw = true;
                    break;
            }
        }

        private void ExecuteSelection()
        {
            string selection = _flatMenu[_selectedIndex];

            if (selection.StartsWith("PROJ:"))
            {
                string projectName = selection.Substring(5);
                var project = _currentProjects.First(p => p.Name == projectName);
                historyService.RecordLaunch(project.Name);
                historyService.AddEvent($"[green]Launched:[/] '{project.Name}' session.");
                sessionOrchestrator.LaunchGemini(project.Name, project.Path);
            }
            else if (selection.Contains("EXIT SHELL")) { Environment.Exit(0); }
            else if (selection.Contains("META-WORKSPACE")) { LaunchMeta(); }
            else if (selection.Contains("MARKETING ASSISTANT")) { marketingService.Execute(); }
            else if (selection.Contains("FOUNDER JOURNAL")) { journalService.Execute(); }
            else if (selection.Contains("NEW PROJECT")) { newProjectService.Execute(); }
            else if (selection.Contains("EVOLVE NEXUS HUB")) { EvolveHub(); }
            else if (selection.Contains("HELP & DOCUMENTATION")) { ShowHelp(); }
            else if (selection.Contains("SYSTEM MAINTENANCE")) { ShowMaintenance(); }
        }

        private void LaunchMeta()
        {
            var allDirs = _currentProjects.Select(p => p.Name).ToList();
            string includeArgs = "--include-directories " + string.Join(" ", allDirs.Select(d => $".\\{d}"));
            historyService.RecordLaunch("META-WORKSPACE");
            historyService.AddEvent("[green]Launched:[/] 'UNIFIED ECOSYSTEM' session.");
            sessionOrchestrator.LaunchGemini("UNIFIED ECOSYSTEM", reposRoot, includeArgs);
        }

        private void EvolveHub()
        {
            var hubPath = Path.Combine(conductorRoot, "NexusShell");
            historyService.RecordLaunch("NEXUS HUB");
            historyService.AddEvent("[green]Launched:[/] 'NEXUS HUB' evolution session.");
            sessionOrchestrator.LaunchGemini("NEXUS HUB", hubPath);
        }

        private void ShowHistory()
        {
            var events = historyService.GetRecentEvents();
            if (events.Count > 0)
            {
                var historyTable = new Table().Border(TableBorder.None).HideHeaders();
                historyTable.AddColumn("Time");
                historyTable.AddColumn("Event");
                foreach (var h in events.Take(3))
                {
                    historyTable.AddRow($"[grey]{h.Timestamp:HH:mm:ss.fff}[/]", h.Message);
                }
                AnsiConsole.Write(new Panel(historyTable).Header("[grey]RECENT ACTIVITY[/]").BorderColor(Color.Grey23));
            }
        }

        private void ShowMaintenance()
        {
            // Simple sub-menu
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("MAINTENANCE").AddChoices("Refresh Git", "Clear History", "Back"));
            if (choice == "Back") return;
            if (choice == "Refresh Git") RunScript("sync-tracks.ps1", true);
            if (choice == "Clear History") { historyService.ClearAll(); Thread.Sleep(500); }
        }

        private void ShowHelp()
        {
            AnsiConsole.MarkupLine("[bold cyan]NEXUS HELP SYSTEM[/] - Press any key to return.");
            AnsiConsole.MarkupLine("• Use Arrow Keys to navigate the dashboard.");
            AnsiConsole.MarkupLine("• The UI updates every 3 seconds to show active windows (●).");
            AnsiConsole.MarkupLine("• All launches and closures are logged in RECENT ACTIVITY.");
            Console.ReadKey();
        }

        private string GetProjectDisplayName(ProjectInfo p)
        {
            string icon = p.Type switch { "Mono" => "🐙", "Multi" => "📦", _ => "📁" };
            string branchInfo = p.Branch != "-" ? $" [grey][[{p.Branch}]][/]" : "";
            
            var statusParts = new List<string>();
            if (p.HasChanges) statusParts.Add("[yellow]![/]");
            if (!string.IsNullOrEmpty(p.RemoteStatus)) statusParts.Add($"[blue]{p.RemoteStatus}[/]");
            string status = statusParts.Count > 0 ? $" {string.Join(" ", statusParts)}" : "";

            string stats = p.OpenCount > 0 ? $" [blue]({p.OpenCount}x)[/] [dim grey]last: {(p.LastOpened.HasValue ? p.LastOpened.Value.ToString("MMM dd HH:mm") : "-")}[/]" : "";
            string active = sessionOrchestrator.IsSessionActive(p.Name) ? " [bold green]●[/]" : "";
            
            return $"{icon} {p.Name.PadRight(18)}{branchInfo}{status}{stats}{active}";
        }

        private void RunScript(string scriptName, bool wait)
        {
            var scriptPath = Path.Combine(conductorRoot, scriptName);
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"") { UseShellExecute = false };
            historyService.AddEvent($"[cyan]Running:[/] {scriptName}");
            var proc = Process.Start(psi);
            if (wait) { proc?.WaitForExit(); Console.ReadKey(); }
        }
    }
}
