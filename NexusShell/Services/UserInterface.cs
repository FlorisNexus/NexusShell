using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Smooth implementation of the Nexus Hub using an Asynchronous Data Engine.
    /// Decouples heavy I/O (Git/Disk scanning) from the UI input loop to ensure fluid navigation.
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
        IRegistryService registryService,
        ILayoutService layoutService) : IUserInterface
    {
        private int _selectedIndex = 0;
        private List<string> _flatMenu = new();
        private List<ProjectInfo> _currentProjects = new();
        private List<HistoryEvent> _recentEvents = new();
        private volatile bool _needsRedraw = true;
        private readonly object _dataLock = new();

        /// <inheritdoc />
        public void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "FLORISNEXUS AI-OS (Async Engine)";
            Console.CursorVisible = false;

            // Start Background Data Sync (Heavy Git/Disk I/O)
            Task.Run(BackgroundDataLoop);

            // UI Input Loop (Instant Response)
            while (true)
            {
                try {
                    if (_needsRedraw)
                    {
                        RenderDashboard();
                        _needsRedraw = false;
                    }

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        HandleInput(key);
                    }
                    else
                    {
                        Thread.Sleep(10); // Ultra-low latency for navigation
                    }
                } catch (Exception ex) {
                    AnsiConsole.WriteException(ex);
                    Console.WriteLine("System Error. Press any key to reboot...");
                    Console.ReadKey();
                    _needsRedraw = true;
                }
            }
        }

        /// <summary>
        /// Background task that performs heavy Git and History operations without blocking the UI.
        /// </summary>
        private async Task BackgroundDataLoop()
        {
            while (true)
            {
                try {
                    // 1. Fetch data from disk/git
                    var projects = projectService.GetProjects();
                    var events = historyService.GetRecentEvents();

                    // 2. Update the Markdown Registry (Legacy sync-tracks logic)
                    registryService.UpdateRegistry(projects);

                    // 3. Safely update the cache
                    lock (_dataLock)
                    {
                        _currentProjects = projects;
                        _recentEvents = events;
                    }

                    // 4. Trigger redraw
                    _needsRedraw = true;
                } catch { /* Ignore background errors to prevent crash */ }

                await Task.Delay(5000); // Sync every 5s in background
            }
        }

        private void RenderDashboard()
        {
            List<ProjectInfo> projectsCopy;
            List<HistoryEvent> eventsCopy;
            lock (_dataLock)
            {
                projectsCopy = new List<ProjectInfo>(_currentProjects);
                eventsCopy = new List<HistoryEvent>(_recentEvents);
            }

            Console.Clear();
            layoutService.RefreshHeader();
            ShowHistory(eventsCopy);

            var coreMenu = new List<string> { 
                "⚡ META-WORKSPACE (UNIFIED)", 
                "📢 MARKETING ASSISTANT", 
                "📔 FOUNDER JOURNAL", 
                "🏗️ NEW PROJECT", 
                "🛠️ EVOLVE NEXUS HUB",
                "📖 HELP & DOCUMENTATION",
                "⚙️ SYSTEM MAINTENANCE",
                "🔌 EXIT SHELL" 
            };

            var saasProjects = projectsCopy.Where(p => p.Track == "SAAS").ToList();
            var localProjects = projectsCopy.Where(p => p.Track == "LOCAL").ToList();
            var otherProjects = projectsCopy.Where(p => p.Track == "OTHER").ToList();

            var newFlatMenu = new List<string>(coreMenu);
            foreach(var p in saasProjects) newFlatMenu.Add("PROJ:" + p.Name);
            foreach(var p in localProjects) newFlatMenu.Add("PROJ:" + p.Name);
            foreach(var p in otherProjects) newFlatMenu.Add("PROJ:" + p.Name);
            _flatMenu = newFlatMenu;

            if (_selectedIndex >= _flatMenu.Count) _selectedIndex = 0;

            AnsiConsole.MarkupLine("[bold grey]» SELECT OPERATION OR NEURAL TRACK (Live Dynamic Registry Active)[/]");
            
            for (int i = 0; i < coreMenu.Count; i++)
            {
                DrawMenuItem(coreMenu[i], i);
            }

            DrawGroup("🌍 GLOBAL SAAS TRACK", saasProjects, coreMenu.Count);
            DrawGroup("🏗️ LOCAL HERO TRACK", localProjects, coreMenu.Count + saasProjects.Count);
            DrawGroup("📂 OTHER TRACKS", otherProjects, coreMenu.Count + saasProjects.Count + localProjects.Count);

            // Show Active Tools if any are running
            if (sessionOrchestrator.IsSessionActive("MARKETING") || sessionOrchestrator.IsSessionActive("NEXUS HUB"))
            {
                AnsiConsole.MarkupLine("\n  [bold yellow]⚡ ACTIVE TOOLS[/]");
                if (sessionOrchestrator.IsSessionActive("MARKETING")) AnsiConsole.MarkupLine("      📢 MARKETING ASSISTANT [bold green]●[/]");
                if (sessionOrchestrator.IsSessionActive("NEXUS HUB")) AnsiConsole.MarkupLine("      🛠️ NEXUS HUB EVOLUTION [bold green]●[/]");
            }

            AnsiConsole.MarkupLine("\n[dim grey]Arrows: Navigate | Enter: Launch | Background sync is non-blocking.[/]");
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
            if (_selectedIndex < 0 || _selectedIndex >= _flatMenu.Count) return;
            string selection = _flatMenu[_selectedIndex];

            if (selection.StartsWith("PROJ:"))
            {
                string projectName = selection.Substring(5);
                ProjectInfo? project;
                lock(_dataLock) { project = _currentProjects.FirstOrDefault(p => p.Name == projectName); }
                
                if (project != null)
                {
                    historyService.RecordLaunch(project.Name);
                    historyService.AddEvent($"[green]Launched:[/] '{project.Name}' session.");
                    sessionOrchestrator.LaunchGemini(project.Name, project.Path);
                }
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
            List<string> allDirs;
            lock(_dataLock) { allDirs = _currentProjects.Select(p => p.Name).ToList(); }
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

        private void ShowHistory(List<HistoryEvent> events)
        {
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
            Console.CursorVisible = true;
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("MAINTENANCE").AddChoices("Force Sync Registry", "Clear History", "Back"));
            Console.CursorVisible = false;
            if (choice == "Back") return;
            if (choice == "Force Sync Registry") 
            { 
                var projects = projectService.GetProjects();
                registryService.UpdateRegistry(projects);
                AnsiConsole.MarkupLine("[green]✅ Tracks Registry updated.[/]");
                Thread.Sleep(1000);
            }
            if (choice == "Clear History") { historyService.ClearAll(); Thread.Sleep(500); }
        }

        private void ShowHelp()
        {
            Console.Clear();
            layoutService.DrawHeroHeader();
            AnsiConsole.MarkupLine("[bold cyan]NEXUS HELP SYSTEM[/] - Press any key to return.");
            AnsiConsole.MarkupLine("• Use Arrow Keys to navigate the dashboard.");
            AnsiConsole.MarkupLine("• Navigation is now ASYNCHRONOUS and fluid.");
            AnsiConsole.MarkupLine("• [yellow]Live Registry:[/] The hub automatically maintains 'tracks.md'.");
            Console.ReadKey();
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
