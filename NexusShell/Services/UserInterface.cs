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
        ICliExecutionService cliExecutionService) : IUserInterface
    {
        private readonly ISessionOrchestrator _sessionOrchestrator = sessionOrchestrator;
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

                masterTable.AddRow(GetWorkspaceHistoryContent(workspaceName));
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
            var saas = projects.Where(p => p.Track == "SAAS").ToList();
            var local = projects.Where(p => p.Track == "LOCAL").ToList();
            var other = projects.Where(p => p.Track == "OTHER").ToList();

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

            var menuGrid = new Grid().AddColumn();
            menuGrid.AddRow("[bold white]» SELECT OPERATION OR NEURAL TRACK[/]");
            for (int i = 0; i < coreMenu.Count; i++) menuGrid.AddRow(GetMenuItemMarkup(coreMenu[i], i));

            AddGroupToGrid(menuGrid, "GLOBAL SAAS TRACK", saas, coreMenu.Count);
            AddGroupToGrid(menuGrid, "LOCAL HERO TRACK", local, coreMenu.Count + saas.Count);
            AddGroupToGrid(menuGrid, "OTHER TRACKS", other, coreMenu.Count + saas.Count + local.Count);

            var newFlatMenu = new List<string>(coreMenu);
            foreach(var p in saas) newFlatMenu.Add("PROJ:" + p.Name);
            foreach(var p in local) newFlatMenu.Add("PROJ:" + p.Name);
            foreach(var p in other) newFlatMenu.Add("PROJ:" + p.Name);
            _flatMenu = newFlatMenu;

            var selected = GetSelectedProject(projects);
            IRenderable briefing;
            
            if (selected != null) {
                briefing = layoutService.GetProjectBriefing(selected);
            } else {
                string s = _flatMenu[_selectedIndex];
                string? key = s switch {
                    _ when s.Contains("MARKETING") => "MARKETING",
                    _ when s.Contains("JOURNAL") => "JOURNAL",
                    _ when s.Contains("NEW PROJECT") => "SCAFFOLDER",
                    _ when s.Contains("EVOLVE") => "NEXUS HUB",
                    _ when s.Contains("META-WORKSPACE") => "UNIFIED ECOSYSTEM",
                    _ => null
                };
                
                if (key != null && _neuralSessions.TryGetValue(key, out var sess)) {
                    var grid = new Grid().AddColumn(new GridColumn().NoWrap()).AddColumn(new GridColumn());
                    grid.AddRow("[cyan]Status:[/]", sess.IsProcessing ? "[bold yellow]Processing...[/]" : "[green]Idle[/]");
                    
                    // Simple text extraction for last context
                    string lastLine = sess.History.LastOrDefault() ?? "No history.";
                    grid.AddRow("[cyan]Last Context:[/]", lastLine);
                    
                    briefing = new Panel(grid).Header($"[bold cyan] {key} INTELLIGENCE [/]").BorderColor(Color.Cyan1).Expand();
                } else {
                    briefing = GetDefaultBriefingPanel();
                }
            }

            var layout = new Table().Border(TableBorder.None).HideHeaders().Expand();
            layout.AddColumn(new TableColumn("").Width(60).NoWrap());
            layout.AddColumn(new TableColumn(""));
            layout.AddRow(new Panel(menuGrid).Header("[bold cyan] FLEET VIEW [/]").BorderColor(Color.Cyan1).Expand(), briefing);
            return layout;
        }

        private IRenderable GetWorkspaceHistoryContent(string workspaceName)
        {
            if (!_neuralSessions.TryGetValue(workspaceName, out var session)) return new Markup("[red]Session sync error.[/]");

            // Overhead increased to 32 to completely remove scrollbar risks on all standard windows.
            int availableLines = Math.Max(5, Console.WindowHeight - 32);
            List<string> history; List<string> sessions;
            lock (session.Lock) { history = new List<string>(session.History); sessions = new List<string>(session.ResumableSessions); }

            // Using Table instead of Grid ensures 100% width expansion for the blue rectangle
            var histGrid = new Table().Border(TableBorder.None).HideHeaders().Expand().AddColumn("");
            var displayedCount = 0;

            if (history.Count == 0 && sessions.Count > 0) {
                histGrid.AddRow("[bold yellow]RECURRING NEURAL PATHS (RESUMABLE SESSIONS):[/]");
                histGrid.AddRow("[dim grey]Use 'gemini -r [[index]]' or 'gemini -r latest' to resume in CLI.[/]");
                histGrid.AddRow("");
                var take = Math.Min(sessions.Count, availableLines - 4);
                foreach (var s in sessions.Take(take)) histGrid.AddRow($"  [cyan]{Markup.Escape(s)}[/]");
                displayedCount = take + 3;
            } else {
                int skip = Math.Max(0, history.Count - availableLines - _historyScrollOffset);
                int take = Math.Min(availableLines, history.Count - skip);
                if (skip > 0) histGrid.AddRow($"[dim grey]  ↑ {skip} more lines above (Arrows to scroll)[/]"); else histGrid.AddRow("");
                var batch = history.Skip(skip).Take(take).ToList();
                foreach (var line in batch) histGrid.AddRow(line);
                displayedCount = batch.Count + 1;
            }

            for (int i = 0; i < (availableLines - displayedCount); i++) histGrid.AddRow("");
            if (_historyScrollOffset > 0) histGrid.AddRow($"[dim grey]  ↓ {_historyScrollOffset} lines below[/]"); else histGrid.AddRow("");

            var inputGrid = new Grid().AddColumn();
            if (session.IsProcessing) {
                string[] frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
                string frame = frames[(DateTime.Now.Millisecond / 100) % frames.Length];
                inputGrid.AddRow($"\n  [bold yellow]{frame} CLI INITIALIZING...[/] [grey]Updating context...[/]");
            } else {
                string promptPrefix = "  [bold cyan]>[/]";
                if (session.WizardStep > 0)
                {
                    string wizardContext = session.ProjectName == "JOURNAL" ? $"Step {session.WizardStep}" : 
                                           (session.ProjectName == "MARKETING" ? $"Step {session.WizardStep}" : "Wizard");
                    promptPrefix = $"  [bold yellow][[{session.ProjectName}: {wizardContext}]][/] [bold cyan]>[/]";
                }

                inputGrid.AddRow($"\n{promptPrefix} {_inputBuffer}[blink white]_ [/]");
                inputGrid.AddRow("[grey]  (Esc: Hub | F1-F12: Switch | Ctrl+W: Close | /clear: Empty History)[/]");
            }

            var container = new Grid().AddColumn();
            container.AddRow(new Panel(histGrid).Header("[bold blue] NEURAL LINK HISTORY [/]").BorderColor(Color.Blue1).Expand());
            container.AddRow(inputGrid);
            return container;
        }

        private void HandleInput(ConsoleKeyInfo key)
        {
            if (key.Key >= ConsoleKey.F1 && key.Key <= ConsoleKey.F12) {
                int target = (int)key.Key - (int)ConsoleKey.F1;
                if (target < _activeWorkspaces.Count) { _activeWorkspaceIndex = target; _historyScrollOffset = 0; _inputBuffer.Clear(); _needsRedraw = true; _forceClear = true; }
                return;
            }
            if (key.Key == ConsoleKey.Tab) { _activeWorkspaceIndex = (_activeWorkspaceIndex + 1) % _activeWorkspaces.Count; _historyScrollOffset = 0; _inputBuffer.Clear(); _needsRedraw = true; _forceClear = true; return; }

            if (_activeWorkspaceIndex == 0) {
                switch (key.Key) {
                    case ConsoleKey.UpArrow: _selectedIndex = (_selectedIndex - 1 + _flatMenu.Count) % _flatMenu.Count; _needsRedraw = true; break;
                    case ConsoleKey.DownArrow: _selectedIndex = (_selectedIndex + 1) % _flatMenu.Count; _needsRedraw = true; break;
                    case ConsoleKey.Enter: ExecuteSelection(); _needsRedraw = true; break;
                    case ConsoleKey.C: TriggerCommitForSelectedProject(); break;
                    case ConsoleKey.D: TriggerDelegationForSelectedProject(); break;
                    case ConsoleKey.F: TriggerFocusModeForSelectedProject(); break;
                }
            } else {
                var session = _neuralSessions[_activeWorkspaces[_activeWorkspaceIndex]];
                if (key.Key == ConsoleKey.Escape) { _activeWorkspaceIndex = 0; _inputBuffer.Clear(); _needsRedraw = true; _forceClear = true; }
                else if (key.Key == ConsoleKey.W && key.Modifiers.HasFlag(ConsoleModifiers.Control)) { CloseWorkspace(_activeWorkspaces[_activeWorkspaceIndex]); }
                else if (key.Key == ConsoleKey.UpArrow) { _historyScrollOffset++; _needsRedraw = true; }
                else if (key.Key == ConsoleKey.DownArrow) { _historyScrollOffset = Math.Max(0, _historyScrollOffset - 1); _needsRedraw = true; }
                else if (key.Key == ConsoleKey.Enter) {
                    string p = _inputBuffer.ToString().Trim();
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
                    _inputBuffer.Clear(); _needsRedraw = true;
                }
                else if (key.Key == ConsoleKey.Backspace) { if (_inputBuffer.Length > 0) _inputBuffer.Remove(_inputBuffer.Length - 1, 1); _needsRedraw = true; }
                else if (!char.IsControl(key.KeyChar)) { _inputBuffer.Append(key.KeyChar); _needsRedraw = true; }
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
                    string prompt = $"I am about to work on this task: '{task}'. Please analyze the repository structure and generate a list of glob patterns for folders/files that are completely irrelevant to this task. Output ONLY the glob patterns, one per line. I will write this to .geminiignore.";
                    InitializeWorkspace(p.Name, p.Path, triggerPrompt: prompt);
                }
                else
                {
                    _forceClear = true;
                    _needsRedraw = true;
                }
            }
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
                    s.WizardData["Description"] = i; s.WizardStep = 0; FinalizeScaffolder(s); 
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
            
            s.History.Add($"[dim grey]{DateTime.Now:HH:mm}[/] [bold green]WIZARD:[/] Scaffolding '{Markup.Escape(projectName)}'...");
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
            AnsiConsole.Write(new Panel(new Markup("[bold cyan]HELP & HOTKEYS[/]\n\n• [yellow]Arrows[/]: Navigate Hub / Scroll History\n• [yellow]Tab / F1-F12[/]: Switch Workspaces\n• [yellow]Esc[/]: Return to Hub\n• [yellow]Enter[/]: Launch / Send Prompt\n• [yellow]C[/]: Auto-Commit selected project\n• [yellow]D[/]: Delegate task (Swarm)\n• [yellow]F[/]: Focus Mode (Compact Context)")).Expand());
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
                    ProjectContext? ctx = null; lock(_dataLock) { ctx = _currentProjects.FirstOrDefault(x => x.Name == s.ProjectName)?.Context; }
                    
                    var sb = new StringBuilder(); 
                    if (!string.IsNullOrEmpty(s.SystemPrompt)) sb.AppendLine(s.SystemPrompt); 
                    else if (ctx != null) sb.AppendLine($"Project: {s.ProjectName} | Goal: {ctx.Objective}");
                    
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
            else if (p.TestStatus == "Fail") health += " [red]✗[/]";
            if (!string.IsNullOrEmpty(p.Coverage)) health += $" [grey]({p.Coverage})[/]";

            return $"{(p.Type=="Mono"?"🐙":"📁")} {p.Name.PadRight(18)} [grey][[{p.Branch}]][/]{(p.HasChanges?" [yellow]![/]":"")}{health}{act}";
        }
        private void AddGroupToGrid(Grid g, string h, List<ProjectInfo> p, int s) {
            if (!p.Any()) return; g.AddRow($"\n  [bold white]{h}[/]");
            for (int i=0; i<p.Count; i++) { int idx = s+i; string d = GetProjectDisplayName(p[i]); g.AddRow(idx == _selectedIndex ? $"    [green]>[/] [white invert]{d}[/]" : $"      {d}"); }
        }
    }
}
