using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Spectre.Console;
using Spectre.Console.Rendering;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Lead UI Orchestrator v16.2. Manages the side-by-side Hub, Wizards, and CLI Links.
    /// </summary>
    public class UserInterface(
        NexusSettings settings,
        IProjectService projectService,
        IHistoryService historyService,
        IRegistryService registryService,
        ILayoutService layoutService,
        ISessionOrchestrator sessionOrchestrator,
        IChatPersistenceService chatPersistence,
        ICliExecutionService cliExecutionService,
        ICloudSyncService cloudSyncService,
        IPlanService planService) : IUserInterface
    {
        private readonly ISessionOrchestrator _sessionOrchestrator = sessionOrchestrator;
        private readonly ICloudSyncService _cloudSyncService = cloudSyncService;
        private readonly IPlanService _planService = planService;
        private readonly NexusSettings _settings = settings;
        private int _selectedIndex = 0;
        private List<string> _flatMenu = new();
        private List<ProjectInfo> _currentProjects = new();
        private List<HistoryEvent> _recentEvents = new();
        private List<string> _activeWorkspaces = new() { "⚡ HUB" };
        private Dictionary<string, NeuralSession> _neuralSessions = new();
        private int _activeWorkspaceIndex = 0;
        private volatile bool _needsRedraw = true;
        private volatile bool _forceClear = true;
        private volatile bool _isModal = false;
        private readonly object _dataLock = new();
        private StringBuilder _inputBuffer = new();
        private int _historyScrollOffset = 0;

        public void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = $"FLORISNEXUS AI-OS (Neural Kernel {_settings.Version})";
            Console.CursorVisible = false;

            Task.Run(BackgroundDataLoop);

            Console.Clear();

            while (true)
            {
                try
                {
                    if (_isModal) { Thread.Sleep(50); continue; }

                    // Animation Redraw (10fps for processing tabs)
                    if (!_needsRedraw && _activeWorkspaceIndex > 0)
                    {
                        var workspaceName = _activeWorkspaces[_activeWorkspaceIndex];
                        if (_neuralSessions.TryGetValue(workspaceName, out var s) && s.IsProcessing)
                        {
                            _needsRedraw = true;
                            Thread.Sleep(100);
                        }
                    }

                    if (_needsRedraw)
                    {
                        if (_forceClear) { Console.Clear(); _forceClear = false; }

                        var dashboard = BuildDashboard();
                        Console.SetCursorPosition(0, 0);
                        AnsiConsole.Write(dashboard);

                        _needsRedraw = false;
                    }

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        HandleInput(key);
                    }
                    else { Thread.Sleep(10); }
                }
                catch (Exception ex)
                {
                    File.AppendAllText("nexus_crash.log", $"{DateTime.Now}: {ex}\n");
                    _isModal = true;
                    Console.Clear();
                    AnsiConsole.WriteException(ex);
                    Console.WriteLine("\nSystem Error. Rebooting UI in 3s...");
                    Thread.Sleep(3000);
                    _isModal = false;
                    _forceClear = true;
                    _needsRedraw = true;
                }
            }
        }

        private async Task BackgroundDataLoop()
        {
            while (true)
            {
                try {
                    if (!_isModal) {
                        var projects = projectService.GetProjects();
                        var events = historyService.GetRecentEvents();
                        registryService.UpdateRegistry(projects);
                        lock (_dataLock) { _currentProjects = projects; _recentEvents = events; }
                        if (_activeWorkspaceIndex == 0) _needsRedraw = true;
                    }
                } catch { }
                await Task.Delay(5000);
            }
        }

        private IRenderable BuildDashboard()
        {
            List<ProjectInfo> projectsCopy;
            List<HistoryEvent> eventsCopy;
            lock (_dataLock) { projectsCopy = new List<ProjectInfo>(_currentProjects); eventsCopy = new List<HistoryEvent>(_recentEvents); }

            var masterTable = new Table().Border(TableBorder.None).HideHeaders().NoSafeBorder().Expand();
            masterTable.AddColumn("Main");

            // 1. Header
            masterTable.AddRow(layoutService.GetHeroHeader());

            // 2. Tabs
            masterTable.AddRow(layoutService.GetTabBar(_activeWorkspaces, _activeWorkspaceIndex));

            if (_activeWorkspaceIndex > 0)
            {
                string workspaceName = _activeWorkspaces[_activeWorkspaceIndex];
                var project = projectsCopy.FirstOrDefault(p => p.Name == workspaceName);

                if (project != null)
                {
                    var brief = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn(new GridColumn());
                    brief.AddRow("[cyan]Goal:[/]", project.Context.Objective);
                    brief.AddRow("[cyan]Last Action:[/]", project.Context.Resume.LastOrDefault() ?? "Session Start.");
                    masterTable.AddRow(new Panel(brief).Header("[bold grey] PROJECT BRIEFING [/]").BorderColor(Color.Grey23));
                }
                else masterTable.AddRow(new Rule().RuleStyle("grey dim"));

                masterTable.AddRow(GetWorkspaceHistoryContent(workspaceName, project));
            }
            else
            {
                // HUB View
                masterTable.AddRow(layoutService.GetStrategicFocus());

                var middleRow = new Table().Border(TableBorder.None).HideHeaders().Expand();
                middleRow.AddColumn(new TableColumn("").Width(40));
                middleRow.AddColumn(new TableColumn(""));

                var activityTable = new Table().Border(TableBorder.None).HideHeaders().AddColumn("E");
                foreach (var h in eventsCopy.Take(3)) activityTable.AddRow($"[grey]{h.Timestamp:HH:mm}[/] {h.Message}");
                if (!eventsCopy.Any()) activityTable.AddRow("[dim grey]No activity yet.[/]");

                middleRow.AddRow(new Panel(activityTable).Header("[grey]RECENT ACTIVITY[/]").BorderColor(Color.Grey23).Expand(), new Markup(""));
                masterTable.AddRow(middleRow);

                masterTable.AddRow(GetFleetViewPanel(projectsCopy));
            }

            // 5. Global Footer (Static)
            // To ensure the footer is locked at the very bottom and overwrites properly,
            // we will let Spectre.Console handle the expansion. The MasterTable is set to Expand().
            masterTable.AddRow(new Rule().RuleStyle("cyan dim"));
            masterTable.AddRow(new Markup($" [dim grey]Arrows: Navigate | Enter: Launch | Tab: Switch | F1-F12: Fast Jump | [bold cyan]{_settings.Version} CLI-Powered Neural OS[/][/]"));
            masterTable.AddRow(new Markup("")); // Safe margin for newline

            return masterTable;
        }

        private IRenderable GetFleetViewPanel(List<ProjectInfo> projects)
        {
            // Apply Role-Based Filtering
            if (_settings.Role.Contains("Branch1", StringComparison.OrdinalIgnoreCase))
            {
                projects = projects.Where(p => p.Track == "LOCAL" || p.Track == "OTHER").ToList();
            }
            else if (_settings.Role.Contains("Branch2", StringComparison.OrdinalIgnoreCase))
            {
                projects = projects.Where(p => p.Track == "SAAS" || p.Track == "OTHER").ToList();
            }

            _flatMenu.Clear();
            _flatMenu.Add("META-WORKSPACE");
            _flatMenu.Add("MARKETING");
            _flatMenu.Add("JOURNAL");
            _flatMenu.Add("NEW PROJECT");
            _flatMenu.Add("EVOLVE HUB");
            _flatMenu.Add("HELP");
            _flatMenu.Add("MAINTENANCE");
            _flatMenu.Add("EXIT");

            var meta = projects.Where(p => p.Track == "META").ToList();
            var local = projects.Where(p => p.Track == "LOCAL").ToList();
            var saas = projects.Where(p => p.Track == "SAAS").ToList();
            var other = projects.Where(p => p.Track == "OTHER").ToList();

            foreach (var p in meta) _flatMenu.Add($"PROJ:{p.Name}");
            foreach (var p in local) _flatMenu.Add($"PROJ:{p.Name}");
            foreach (var p in saas) _flatMenu.Add($"PROJ:{p.Name}");
            foreach (var p in other) _flatMenu.Add($"PROJ:{p.Name}");

            var grid = new Grid().AddColumn(new GridColumn().NoWrap());
            grid.AddRow("[bold cyan]COMMANDS[/]");
            grid.AddRow(GetMenuItemMarkup("ORCHESTRATE FLEET (META)", 0));
            grid.AddRow(GetMenuItemMarkup("MARKETING STRATEGIST", 1));
            grid.AddRow(GetMenuItemMarkup("FOUNDER'S JOURNAL", 2));
            grid.AddRow(GetMenuItemMarkup("NEW PROJECT SCAFFOLDER", 3));
            grid.AddRow(GetMenuItemMarkup("EVOLVE NEXUS HUB", 4));
            grid.AddRow(GetMenuItemMarkup("HELP & HOTKEYS", 5));
            grid.AddRow(GetMenuItemMarkup("MAINTENANCE", 6));
            grid.AddRow(GetMenuItemMarkup("EXIT AI-OS", 7));

            var fleetGrid = new Grid().AddColumn(new GridColumn());
            int startIdx = 9;
            AddGroupToGrid(fleetGrid, "SYSTEM CORE", meta, startIdx);
            startIdx += meta.Count;
            AddGroupToGrid(fleetGrid, "LOCAL HERO (TRACK 1)", local, startIdx);
            startIdx += local.Count;
            AddGroupToGrid(fleetGrid, "GLOBAL SAAS (TRACK 2)", saas, startIdx);
            startIdx += saas.Count;
            AddGroupToGrid(fleetGrid, "INCUBATOR & R&D", other, startIdx);

            var outer = new Table().Border(TableBorder.None).HideHeaders().Expand();
            outer.AddColumn(new TableColumn("").Width(40));
            outer.AddColumn(new TableColumn(""));
            outer.AddRow(new Panel(grid).Header("[grey]INTEL COMMANDS[/]").BorderColor(Color.Grey23).Expand(),
                         new Panel(fleetGrid).Header("[grey]ACTIVE FLEET[/]").BorderColor(Color.Grey23).Expand());

            return outer;
        }

        private IRenderable GetWorkspaceHistoryContent(string workspaceName, ProjectInfo? project)
        {
            if (!_neuralSessions.TryGetValue(workspaceName, out var session)) return new Markup("[red]Session Lost.[/]");

            var table = new Table().Border(TableBorder.None).HideHeaders().Expand();
            table.AddColumn("History");

            var historyItems = new List<string>();
            lock (session.Lock) { historyItems = new List<string>(session.History); }

            var scrollContainer = new List<string>();
            int visibleLines = Console.WindowHeight - 22;
            int totalLines = historyItems.Count;

            int skip = Math.Max(0, totalLines - visibleLines - _historyScrollOffset);
            var visibleItems = historyItems.Skip(skip).Take(visibleLines).ToList();

            foreach (var item in visibleItems) table.AddRow(item);

            var mainContent = new Panel(table)
                .Header($"[bold grey] {workspaceName} NEURAL STREAM [/]")
                .BorderColor(Color.Grey23)
                .Expand();

            var inputGrid = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn(new GridColumn());
            string status = session.IsProcessing ? "[bold yellow]PROCESSING...[/]" : "[bold green]READY[/]";
            inputGrid.AddRow(new Markup($" {status} [cyan]>[/] "), new Markup(session.InputBuffer.ToString() + "[blink]_[/]"));

            var container = new Table().Border(TableBorder.None).HideHeaders().Expand();
            container.AddColumn("");
            container.AddRow(mainContent);
            container.AddRow(inputGrid);
            return container;
        }

        private void HandleInput(ConsoleKeyInfo key)
        {
            if (key.Key >= ConsoleKey.F1 && key.Key <= ConsoleKey.F12) {
                int target = (int)key.Key - (int)ConsoleKey.F1;
                if (target < _activeWorkspaces.Count) { _activeWorkspaceIndex = target; _historyScrollOffset = 0; _needsRedraw = true; _forceClear = true; }
                return;
            }
            if (key.Key == ConsoleKey.Tab) { _activeWorkspaceIndex = (_activeWorkspaceIndex + 1) % _activeWorkspaces.Count; _historyScrollOffset = 0; _needsRedraw = true; _forceClear = true; return; }

            if (_activeWorkspaceIndex == 0) {
                switch (key.Key) {
                    case ConsoleKey.UpArrow: _selectedIndex = (_selectedIndex - 1 + _flatMenu.Count) % _flatMenu.Count; _needsRedraw = true; break;
                    case ConsoleKey.DownArrow: _selectedIndex = (_selectedIndex + 1) % _flatMenu.Count; _needsRedraw = true; break;
                    case ConsoleKey.Enter: ExecuteSelection(); _needsRedraw = true; break;
                    case ConsoleKey.C: TriggerCommitForSelectedProject(); break;
                    case ConsoleKey.D: TriggerDelegationForSelectedProject(); break;
                    case ConsoleKey.F: TriggerFocusModeForSelectedProject(); break;
                    case ConsoleKey.S: TriggerFleetSync(); break;
                    case ConsoleKey.M: TriggerMorningStandup(); break;
                    case ConsoleKey.U: TriggerCloudSyncUp(); break;
                    case ConsoleKey.L: TriggerCloudSyncDown(); break;
                    case ConsoleKey.P: TriggerPlanCreationWizard(); break;
                }
            } else {
                var session = _neuralSessions[_activeWorkspaces[_activeWorkspaceIndex]];
                if (key.Key == ConsoleKey.Escape) { _activeWorkspaceIndex = 0; _needsRedraw = true; _forceClear = true; }
                else if (key.Key == ConsoleKey.W && key.Modifiers.HasFlag(ConsoleModifiers.Control)) { CloseWorkspace(_activeWorkspaces[_activeWorkspaceIndex]); }
                else if (key.Key == ConsoleKey.UpArrow) { _historyScrollOffset++; _needsRedraw = true; }
                else if (key.Key == ConsoleKey.DownArrow) { _historyScrollOffset = Math.Max(0, _historyScrollOffset - 1); _needsRedraw = true; }
                else if (key.Key == ConsoleKey.Enter) {
                    string p = session.InputBuffer.ToString().Trim();
                    if (p.Equals("/close", StringComparison.OrdinalIgnoreCase) || p.Equals("exit", StringComparison.OrdinalIgnoreCase)) {
                        CloseWorkspace(_activeWorkspaces[_activeWorkspaceIndex]);
                    }
                    else if (p.Equals("/clear", StringComparison.OrdinalIgnoreCase)) {
                        lock (session.Lock) {
                            session.History.Clear();
                            session.Turns.Clear();
                            session.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]SYSTEM:[/] History cleared.");
                        }
                        chatPersistence.SaveHistory(session.ProjectPath, new List<ConversationTurn>());
                    }
                    else if (!string.IsNullOrEmpty(p) && !session.IsProcessing) {
                        if (session.WizardStep > 0) ProcessWizardStep(session, p); else SubmitUserPrompt(session, p);
                    }
                    session.InputBuffer.Clear(); _needsRedraw = true;
                }
                else if (key.Key == ConsoleKey.Backspace) { if (session.InputBuffer.Length > 0) session.InputBuffer.Remove(session.InputBuffer.Length - 1, 1); _needsRedraw = true; }
                else if (!char.IsControl(key.KeyChar)) { session.InputBuffer.Append(key.KeyChar); _needsRedraw = true; }
            }
        }

        private void CloseWorkspace(string name)
        {
            if (name == "⚡ HUB") return;
            int idx = _activeWorkspaces.IndexOf(name);
            if (idx > 0)
            {
                _activeWorkspaces.RemoveAt(idx);
                _neuralSessions.Remove(name);
                _activeWorkspaceIndex = Math.Max(0, idx - 1);
                _inputBuffer.Clear();
                _needsRedraw = true;
                _forceClear = true;
            }
        }

        private void TriggerFleetSync()
        {
            var p = GetSelectedProject(_currentProjects);
            _isModal = true;
            Console.Clear();
            AnsiConsole.Write(layoutService.GetHeroHeader());

            if (p != null)
            {
                AnsiConsole.MarkupLine($"[bold yellow]Syncing {p.Name}...[/]");
                projectService.SyncProject(p.Path);
                AnsiConsole.MarkupLine($"[bold green]✅ {p.Name} synced.[/]");
            }
            else if (_flatMenu[_selectedIndex].Contains("META-WORKSPACE"))
            {
                AnsiConsole.MarkupLine("[bold yellow]Initiating Fleet Sync...[/]");
                var syncableProjects = _currentProjects.Where(x => x.Type == "Mono" || x.Type == "Multi").ToList();
                foreach (var proj in syncableProjects)
                {
                    AnsiConsole.MarkupLine($"[grey]Syncing {proj.Name}...[/]");
                    projectService.SyncProject(proj.Path);
                }
                AnsiConsole.MarkupLine("[bold green]✅ Fleet synchronization complete.[/]");
            }

            Thread.Sleep(1500);
            _isModal = false;
            _forceClear = true;
            _needsRedraw = true;
        }

        private void TriggerCommitForSelectedProject()
        {
            var p = GetSelectedProject(_currentProjects);
            if (p != null && p.HasChanges)
            {
                InitializeWorkspace(p.Name, p.Path, triggerPrompt: "Run git status. Then, based on the changes, generate a concise, atomic git commit message and execute 'git commit -m \"<message>\"'.");
            }
        }

        private void TriggerDelegationForSelectedProject()
        {
            var p = GetSelectedProject(_currentProjects);
            if (p != null)
            {
                _isModal = true;
                Console.Clear();
                AnsiConsole.Write(layoutService.GetHeroHeader());
                string goal = AnsiConsole.Ask<string>($"[bold cyan]Delegate task for {p.Name}:[/]");
                _isModal = false;

                if (!string.IsNullOrWhiteSpace(goal))
                {
                    InitializeWorkspace(p.Name, p.Path, extraArgs: "--skill nexus-subagent-orchestrator", triggerPrompt: $"Execute this delegated task: {goal}");
                }
                else
                {
                    _forceClear = true;
                    _needsRedraw = true;
                }
            }
        }

        private void TriggerCloudSyncUp()
        {
            _isModal = true;
            Console.Clear();
            AnsiConsole.Write(layoutService.GetHeroHeader());
            AnsiConsole.MarkupLine("[bold yellow]Synchronizing Conductor memory TO Cloud...[/]");
            try {
                _cloudSyncService.SyncToCloud();
                AnsiConsole.MarkupLine("[bold green]✅ Upload complete.[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[bold red]✖ Failed:[/] {Markup.Escape(ex.Message)}");
            }
            Thread.Sleep(1500);
            _isModal = false;
            _forceClear = true;
            _needsRedraw = true;
        }

        private void TriggerCloudSyncDown()
        {
            _isModal = true;
            Console.Clear();
            AnsiConsole.Write(layoutService.GetHeroHeader());
            AnsiConsole.MarkupLine("[bold yellow]Synchronizing Conductor memory FROM Cloud...[/]");
            try {
                _cloudSyncService.SyncFromCloud();
                AnsiConsole.MarkupLine("[bold green]✅ Download complete.[/]");
                registryService.UpdateRegistry(projectService.GetProjects()); // refresh memory
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[bold red]✖ Failed:[/] {Markup.Escape(ex.Message)}");
            }
            Thread.Sleep(1500);
            _isModal = false;
            _forceClear = true;
            _needsRedraw = true;
        }

        private void TriggerMorningStandup()
        {
            _isModal = true;
            Console.Clear();
            AnsiConsole.Write(layoutService.GetHeroHeader());
            AnsiConsole.MarkupLine("[bold yellow]Gathering cross-project intel for Morning Standup...[/]");

            var sb = new StringBuilder();
            sb.AppendLine("Generate a 'Morning Standup' briefing for the founder. Here is the activity from the last 24 hours:\n");

            // Get Journal
            try {
                var journalDir = Path.Combine(_settings.ConductorRoot, "journal");
                if (Directory.Exists(journalDir)) {
                    var latestJournal = Directory.GetFiles(journalDir, "*.md").OrderByDescending(f => f).FirstOrDefault();
                    if (latestJournal != null) {
                        sb.AppendLine($"--- LATEST JOURNAL ENTRY ({Path.GetFileName(latestJournal)}) ---");
                        sb.AppendLine(File.ReadAllText(latestJournal));
                    }
                }
            } catch { }

            // Get Git Logs
            sb.AppendLine("\n--- RECENT GIT COMMITS (Last 24h) ---");
            foreach (var p in _currentProjects.Where(x => x.Type == "Mono" || x.Type == "Multi"))
            {
                try {
                    var psi = new ProcessStartInfo("git", "log --since=\"24 hours ago\" --oneline")
                    {
                        WorkingDirectory = p.Path,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    string log = proc?.StandardOutput.ReadToEnd().Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(log)) {
                        sb.AppendLine($"Project: {p.Name}");
                        sb.AppendLine(log);
                    }
                } catch { }
            }

            _isModal = false;

            string args = "--include-directories " + string.Join(" ", _currentProjects.Select(p => $".\\{p.Name}"));
            InitializeWorkspace("UNIFIED ECOSYSTEM", _settings.ReposRoot, args, triggerPrompt: sb.ToString());
        }

        private void TriggerFocusModeForSelectedProject()
        {
            var p = GetSelectedProject(_currentProjects);
            if (p != null)
            {
                _isModal = true;
                Console.Clear();
                AnsiConsole.Write(layoutService.GetHeroHeader());
                string task = AnsiConsole.Ask<string>($"[bold cyan]What is the current task for {p.Name}? (I will compact context):[/]");
                _isModal = false;

                if (!string.IsNullOrWhiteSpace(task))
                {
                    InitializeWorkspace(p.Name, p.Path);
                    var session = _neuralSessions[p.Name];
                    session.IsProcessing = true;

                    Task.Run(async () => {
                        try {
                            string prompt = $"I am about to work on this task: '{task}'. Please analyze the repository structure and generate a list of glob patterns for folders/files that are completely irrelevant to this task. Output ONLY the raw glob patterns, one per line. Do not include markdown formatting or explanations.";
                            string res = await cliExecutionService.ExecutePromptAsync(p.Path, prompt, "");

                            var lines = res.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("`")).ToList();

                            string ignorePath = Path.Combine(p.Path, ".geminiignore");
                            string existing = File.Exists(ignorePath) ? File.ReadAllText(ignorePath) : "";

                            var sb = new StringBuilder();
                            sb.AppendLine(existing);
                            sb.AppendLine($"\n# Auto-compacted for task: {task}");
                            foreach(var l in lines) sb.AppendLine(l);

                            File.WriteAllText(ignorePath, sb.ToString());

                            string ts = DateTime.Now.ToString("HH:mm");
                            lock (session.Lock) {
                                session.History.Add($"[dim grey]{ts}[/] [bold yellow]SYSTEM:[/] Context compacted. Appended {lines.Count} ignore patterns to .geminiignore.");
                            }
                        } catch (Exception ex) {
                            lock(session.Lock) { session.History.Add($"[red]Error:[/] {Markup.Escape(ex.Message)}"); }
                        } finally {
                            session.IsProcessing = false;
                            _needsRedraw = true;
                        }
                    });
                }
                else
                {
                    _forceClear = true;
                    _needsRedraw = true;
                }
            }
        }

        /// <summary>
        /// Opens an interactive wizard to scaffold a conductor plan + Gemini prompt pair.
        /// </summary>
        private void TriggerPlanCreationWizard()
        {
            _isModal = true;
            Console.Clear();
            AnsiConsole.Write(layoutService.GetHeroHeader());

            // Step 1 — choose project
            var projectNames = _currentProjects
                .Select(p => p.Name.ToLowerInvariant())
                .OrderBy(n => n)
                .ToList();

            if (projectNames.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No projects found. Refresh the dashboard first.[/]");
                Thread.Sleep(2000);
                _isModal = false; _forceClear = true; _needsRedraw = true;
                return;
            }

            string project = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]Select project:[/]")
                    .AddChoices(projectNames));

            // Step 2 — slug
            string slug = AnsiConsole.Ask<string>("[bold cyan]Plan slug[/] [grey](kebab-case, e.g. rate-limiting):[/]").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(slug))
            {
                AnsiConsole.MarkupLine("[yellow]Aborted — slug cannot be empty.[/]");
                Thread.Sleep(1500);
                _isModal = false; _forceClear = true; _needsRedraw = true;
                return;
            }

            try
            {
                var (planPath, promptPath) = _planService.CreatePlanPair(project, slug);
                AnsiConsole.MarkupLine($"[bold green]✅ Plan pair created![/]");
                AnsiConsole.MarkupLine($"[grey]  Plan  :[/] {planPath}");
                AnsiConsole.MarkupLine($"[grey]  Prompt:[/] {promptPath}");
                AnsiConsole.MarkupLine($"[dim]NEXT.md updated. Open the files and fill in the details.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            }

            Thread.Sleep(3000);
            _isModal = false;
            _forceClear = true;
            _needsRedraw = true;
        }

        private void ExecuteSelection()
        {
            string selection = _flatMenu[_selectedIndex];
            if (selection.StartsWith("PROJ:")) {
                var p = _currentProjects.FirstOrDefault(x => x.Name == selection.Substring(5));
                if (p != null) InitializeWorkspace(p.Name, p.Path, triggerPrompt: "Summarize status.");
            }
            else if (selection.Contains("EXIT")) Environment.Exit(0);
            else if (selection.Contains("META")) {
                string args = "--include-directories " + string.Join(" ", _currentProjects.Select(p => $".\\{p.Name}"));
                InitializeWorkspace("UNIFIED ECOSYSTEM", _settings.ReposRoot, args, triggerPrompt: "Ecosystem status.");
            }
            else if (selection.Contains("MARKETING")) StartMarketingWizard();
            else if (selection.Contains("JOURNAL")) StartJournalWizard();
            else if (selection.Contains("NEW PROJECT")) StartScaffolderWizard();
            else if (selection.Contains("EVOLVE")) InitializeWorkspace("NEXUS HUB", Path.Combine(_settings.ReposRoot, "NexusShell"), triggerPrompt: "Hub status.");
            else if (selection.Contains("HELP")) ShowHelp();
            else if (selection.Contains("MAINTENANCE")) ShowMaintenance();
        }

        private void StartJournalWizard() {
            InitializeWorkspace("JOURNAL", _settings.ConductorRoot);
            var s = _neuralSessions["JOURNAL"]; s.WizardStep = 1;
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold yellow]WIZARD:[/] Welcome. Step 1: Key events/hurdles?");
            _needsRedraw = true;
        }

        private void StartMarketingWizard() {
            InitializeWorkspace("MARKETING", _settings.ReposRoot);
            var s = _neuralSessions["MARKETING"]; s.WizardStep = 1;
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold yellow]WIZARD:[/] Marketing Strategist initialized.");
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold yellow]WIZARD:[/] [white]Step 1: What would you like to do?[/]");
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold yellow]WIZARD:[/] [white](1) Generate Social Post  (2) Compile Weekly Changelog[/]");
            _needsRedraw = true;
        }

        private void StartScaffolderWizard() {
            InitializeWorkspace("SCAFFOLDER", _settings.ConductorRoot);
            var s = _neuralSessions["SCAFFOLDER"]; s.WizardStep = 1;
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold yellow]WIZARD:[/] Project Architect initialized.");
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold yellow]WIZARD:[/] [white]Step 1: What is the name of the new project?[/]");
            _needsRedraw = true;
        }

        private void ProcessWizardStep(NeuralSession s, string i) {
            string ts = DateTime.Now.ToString("HH:mm"); s.History.Add($"[dim grey]{ts}[/] [bold cyan]YOU:[/] {Markup.Escape(i)}");
            if (s.ProjectName == "JOURNAL") {
                if (s.WizardStep == 1) { s.WizardData["E"] = i; s.WizardStep = 2; s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] Step 2: Key decisions?"); }
                else if (s.WizardStep == 2) { s.WizardData["D"] = i; s.WizardStep = 3; s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] Step 3: Lessons learned?"); }
                else if (s.WizardStep == 3) { s.WizardData["L"] = i; s.WizardStep = 0; FinalizeJournal(s); }
            } else if (s.ProjectName == "MARKETING") {
                if (s.WizardStep == 1) {
                    if (i.Contains("1")) {
                        s.WizardStep = 2;
                        s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] [white]Step 2: Which project or feature should we promote today?[/]"); 
                    } else if (i.Contains("2")) {
                        s.WizardStep = 0;
                        s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]WIZARD:[/] Initiating Weekly Changelog compilation...");
                        SubmitTriggerPrompt(s, "Read the journal entries from the past 7 days. Filter out personal reflections and extract only the shipped features and technical milestones. Generate a professional Markdown CHANGELOG.md draft and a 'Founder Update' newsletter.");
                    } else {
                        s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] Please enter 1 or 2.");
                    }
                }
                else if (s.WizardStep == 2) { s.WizardData["T"] = i; s.WizardStep = 3; s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] [white]Step 3: What is the primary hook or technical achievement?[/]"); }
                else if (s.WizardStep == 3) { s.WizardData["H"] = i; s.WizardStep = 0; FinalizeMarketing(s); }
            } else if (s.ProjectName == "SCAFFOLDER") {
                if (s.WizardStep == 1) {
                    s.WizardData["Name"] = i; s.WizardStep = 2;
                    s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] [white]Step 2: Which branch? (1) LOCAL HERO (React/Vite) or (2) GLOBAL SAAS (.NET 10)[/]");
                }
                else if (s.WizardStep == 2) {
                    s.WizardData["Branch"] = i; s.WizardStep = 3;
                    s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] [white]Step 3: Provide a one-sentence description of the project.[/]");
                }
                else if (s.WizardStep == 3) {
                    s.WizardData["Description"] = i;
                    if (s.WizardData["Branch"].Contains("2")) {
                        s.WizardStep = 4;
                        s.History.Add($"[dim grey]{ts}[/] [bold yellow]WIZARD:[/] [white]Step 4: Do you want to automatically deploy this to Azure (Staging) via Bicep? (Y/N)[/]");
                    } else {
                        s.WizardData["Deploy"] = "N";
                        s.WizardStep = 0; FinalizeScaffolder(s);
                    }
                }
                else if (s.WizardStep == 4) {
                    s.WizardData["Deploy"] = i.Equals("Y", StringComparison.OrdinalIgnoreCase) ? "Y" : "N";
                    s.WizardStep = 0; FinalizeScaffolder(s);
                }
            }
        }

        private void FinalizeJournal(NeuralSession s) {
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            string path = Path.Combine(_settings.ConductorRoot, "journal", $"{date}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"# Journal {date}\n\n## Events\n{s.WizardData["E"]}\n\n## Decisions\n{s.WizardData["D"]}\n\n## Lessons\n{s.WizardData["L"]}");
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]WIZARD:[/] Journal saved. Analyzing...");
            SubmitTriggerPrompt(s, $"Read journal/{date}.md and summarize.");
        }

        private void FinalizeMarketing(NeuralSession s) {
            File.AppendAllText(Path.Combine(_settings.ConductorRoot, "marketing_drafts.md"), $"\n- {DateTime.Now}: {s.WizardData["T"]} Hook: {s.WizardData["H"]}");
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]WIZARD:[/] Draft logged. Generating social content...");
            SubmitTriggerPrompt(s, $"Generate 3 social posts for {s.WizardData["T"]} with hook {s.WizardData["H"]} using nexus-social-marketing skill.");
        }

        private void FinalizeScaffolder(NeuralSession s) {
            string projectName = s.WizardData["Name"];
            string branch = s.WizardData["Branch"]; // "1" for LOCAL, "2" for SAAS
            string description = s.WizardData["Description"];
            bool doDeploy = s.WizardData.TryGetValue("Deploy", out var dep) && dep == "Y";

            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]WIZARD:[/] Scaffolding '{Markup.Escape(projectName)}'...");
            if (doDeploy) s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]WIZARD:[/] Bicep Cloud Deployment enabled.");

            _needsRedraw = true;

            Task.Run(() => {
                try {
                    string projectDir = Path.Combine(_settings.ReposRoot, projectName);
                    Directory.CreateDirectory(projectDir);

                    string cmd = "";
                    if (branch.Contains("1")) { // LOCAL
                        cmd = $"cd '{_settings.ReposRoot}'; npm create vite@latest {projectName} -- --template react-ts";
                    } else { // SAAS
                        cmd = $"cd '{_settings.ReposRoot}'; dotnet new webapi -n {projectName}";
                    }

                    var psi = new ProcessStartInfo("powershell.exe") {
                        Arguments = $"-NoProfile -Command \"{cmd}; cd '{projectDir}'; git init\"",
                        CreateNoWindow = true, UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit();

                    string geminiMd = $"# {projectName}\n\n## Description\n{description}\n\n## Branch\n{(branch.Contains("1") ? "LOCAL HERO (React/Vite)" : "GLOBAL SAAS (.NET 10)")}\n";
                    if (doDeploy) geminiMd += "\n## Infrastructure\nAuto-deployed to Azure via Bicep templates.\n";
                    File.WriteAllText(Path.Combine(projectDir, "GEMINI.md"), geminiMd);

                    lock (s.Lock) {
                        s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]WIZARD:[/] Project scaffolded successfully. Triggering AI Architect...");
                    }

                    s.ProjectName = projectName;
                    s.ProjectPath = projectDir;
                    s.SystemPrompt = "You are an Architect AI. Review the newly scaffolded project and suggest the next 3 development steps based on its GEMINI.md.";

                    SubmitTriggerPrompt(s, "Project has just been scaffolded. Read GEMINI.md and provide an initial architectural summary and next steps.");
                } catch (Exception ex) {
                    lock(s.Lock) { s.History.Add($"[red]Error scaffolding:[/] {Markup.Escape(ex.Message)}"); }
                    _needsRedraw = true;
                }
            });
        }

        private void ShowMaintenance() {
            _isModal = true; Console.Clear(); AnsiConsole.Write(layoutService.GetHeroHeader());
            var choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title(" MAINTENANCE ").AddChoices("Sync Registry", "Clear History", "Back"));
            if (choice == "Sync Registry") { registryService.UpdateRegistry(projectService.GetProjects()); Thread.Sleep(1000); }
            else if (choice == "Clear History") { historyService.ClearAll(); Thread.Sleep(1000); }
            _isModal = false; _forceClear = true; _needsRedraw = true;
        }

        private void ShowHelp() {
            _isModal = true; Console.Clear(); AnsiConsole.Write(layoutService.GetHeroHeader());
            AnsiConsole.Write(new Panel(new Markup("[bold cyan]HELP & HOTKEYS[/]\n\n• [yellow]Arrows[/]: Navigate Hub / Scroll History\n• [yellow]Tab / F1-F12[/]: Switch Workspaces\n• [yellow]Esc[/]: Return to Hub\n• [yellow]Enter[/]: Launch / Send Prompt\n• [yellow]C[/]: Auto-Commit selected project\n• [yellow]D[/]: Delegate task (Swarm)\n• [yellow]F[/]: Focus Mode (Compact Context)\n• [yellow]S[/]: Sync Fleet/Project (Git Pull/Push)\n• [yellow]M[/]: Morning Standup (Intel Briefing)")).Expand());
            Console.ReadKey(); _isModal = false; _forceClear = true; _needsRedraw = true;
        }

        private void SubmitUserPrompt(NeuralSession s, string p) {
            lock (s.Lock) { s.Turns.Add(new ConversationTurn { Role = "user", Content = p }); s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold cyan]YOU:[/] {Markup.Escape(p)}"); }
            s.IsProcessing = true; ExecutePromptInBackground(s, p);
        }

        private void SubmitTriggerPrompt(NeuralSession s, string p) { s.IsProcessing = true; ExecutePromptInBackground(s, p); }

        private void ExecutePromptInBackground(NeuralSession s, string p) {
            Task.Run(async () => {
                try {
                    ProjectInfo? projInfo = null; lock(_dataLock) { projInfo = _currentProjects.FirstOrDefault(x => x.Name == s.ProjectName); }
                    ProjectContext? ctx = projInfo?.Context;

                    var sb = new StringBuilder();
                    if (!string.IsNullOrEmpty(s.SystemPrompt)) sb.AppendLine(s.SystemPrompt);
                    else if (ctx != null)
                    {
                        sb.AppendLine($"Project: {s.ProjectName} | Goal: {ctx.Objective}");
                        string geminiMdPath = Path.Combine(s.ProjectPath, "GEMINI.md");
                        if (File.Exists(geminiMdPath)) {
                            sb.AppendLine("--- GEMINI.md CONTEXT ---");
                            try { sb.AppendLine(File.ReadAllText(geminiMdPath)); } catch {}
                            sb.AppendLine("-------------------------");
                        }
                        if (projInfo != null && projInfo.HasChanges && !string.IsNullOrEmpty(projInfo.Diff)) {
                            sb.AppendLine("--- UNCOMMITTED CHANGES (GIT DIFF) ---");
                            sb.AppendLine(projInfo.Diff.Length > 3000 ? projInfo.Diff.Substring(0, 3000) + "\n...[diff truncated]" : projInfo.Diff);
                            sb.AppendLine("--------------------------------------");
                        }
                    }

                    List<ConversationTurn> turns; lock(s.Lock) { turns = s.Turns.SkipLast(1).TakeLast(5).ToList(); }
                    if (turns.Any()) { sb.AppendLine("--- HISTORY ---"); foreach (var t in turns) sb.AppendLine($"{(t.Role == "user" ? "USER" : "AI")}: {t.Content}"); sb.AppendLine("--- PROMPT ---"); }
                    sb.Append(p);

                    string ts = DateTime.Now.ToString("HH:mm");
                    string accumulatedResponse = "";
                    int historyIndex = -1;

                    lock (s.Lock) {
                        s.Turns.Add(new ConversationTurn { Role = "ai", Content = "" });
                        s.History.Add($"[dim grey]{ts}[/] [bold green]AI:[/] ");
                        historyIndex = s.History.Count - 1;
                    }

                    await foreach (var chunk in cliExecutionService.StreamPromptAsync(s.ProjectPath, sb.ToString(), s.ExtraArgs))
                    {
                        accumulatedResponse += chunk + "\n";
                        lock (s.Lock)
                        {
                            s.Turns.Last().Content = accumulatedResponse.TrimEnd();
                            s.History[historyIndex] = $"[dim grey]{ts}[/] [bold green]AI:[/] {MarkdownRenderer.ToSpectreMarkup(accumulatedResponse.TrimEnd())}";
                        }
                        _historyScrollOffset = 0;
                        _needsRedraw = true;
                    }

                    List<ConversationTurn> save; lock(s.Lock) { save = s.Turns.ToList(); } chatPersistence.SaveHistory(s.ProjectPath, save);
                } catch (Exception ex) { lock(s.Lock) { s.History.Add($"[red]Error:[/] {Markup.Escape(ex.Message)}"); } }
                finally { s.IsProcessing = false; _needsRedraw = true; }
            });
        }

        private void InitializeWorkspace(string name, string path, string extraArgs = "", string systemPromptOverride = "", string greeterQuestion = "", string triggerPrompt = "") {
            if (!_activeWorkspaces.Contains(name)) {
                _activeWorkspaces.Add(name); var turns = chatPersistence.LoadHistory(path);
                var s = new NeuralSession { ProjectName = name, ProjectPath = path, ExtraArgs = extraArgs, SystemPrompt = systemPromptOverride, Turns = turns };
                foreach (var t in turns) s.History.Add($"[dim grey]{t.Timestamp:HH:mm}[/] [{(t.Role == "user" ? "bold cyan]YOU" : "bold green]AI")}:[/] {MarkdownRenderer.ToSpectreMarkup(t.Content)}");
                _neuralSessions[name] = s; _ = FetchResumableSessionsAsync(s); if (!string.IsNullOrEmpty(triggerPrompt)) SubmitTriggerPrompt(s, triggerPrompt);
            }
            _activeWorkspaceIndex = _activeWorkspaces.IndexOf(name); _historyScrollOffset = 0; _needsRedraw = true; _forceClear = true;
        }

        private async Task FetchResumableSessionsAsync(NeuralSession s) {
            try {
                string output = await cliExecutionService.ExecutePromptAsync(s.ProjectPath, "", "--list-sessions");
                lock(s.Lock) { s.ResumableSessions = output.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l) && char.IsDigit(l[0])).ToList(); }
            } catch { }
        }

        private IRenderable GetDefaultBriefingPanel() => new Panel("[dim grey]Select a project to view its intelligence context.[/]").Header("[bold grey] BRIEFING [/]").Expand();
        private string GetMenuItemMarkup(string l, int i) => i == _selectedIndex ? $"  [bold cyan]>[/] [cyan invert]{l}[/]" : $"    [cyan]{l}[/]";
        private ProjectInfo? GetSelectedProject(List<ProjectInfo> p) { string s = _flatMenu[_selectedIndex]; return s.StartsWith("PROJ:") ? p.FirstOrDefault(x => x.Name == s.Substring(5)) : null; }  
        private string GetProjectDisplayName(ProjectInfo p) {
            string act = _neuralSessions.TryGetValue(p.Name, out var s) && s.IsProcessing ? " [bold yellow]●[/]" : _activeWorkspaces.Contains(p.Name) ? " [bold green]●[/]" : "";

            string health = "";
            if (p.TestStatus == "Pass") health += " [green]✓[/]";
            else if (p.TestStatus == "Fail") health += " [red]✖[/]";
            if (!string.IsNullOrEmpty(p.Coverage)) health += $" [grey]({p.Coverage})[/]";

            return $"{(p.Type=="Mono"?"🛐":"📁")} {p.Name.PadRight(18)} [grey][[{p.Branch}]][/]{(p.HasChanges?" [yellow]![/]":"")}{health}{act}";
        }
        private void AddGroupToGrid(Grid g, string h, List<ProjectInfo> p, int s) {
            if (!p.Any()) return; g.AddRow($"\n  [bold white]{h}[/]");
            for (int i=0; i<p.Count; i++) { int idx = s+i; string d = GetProjectDisplayName(p[i]); g.AddRow(idx == _selectedIndex ? $"    [green]>[/] [white invert]{d}[/]" : $"      {d}"); }
        }
    }
}
