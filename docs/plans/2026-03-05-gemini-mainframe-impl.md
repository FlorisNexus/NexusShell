# NexusShell Gemini Mainframe Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Transform NexusShell into a professional mainframe TUI where every feature (projects, marketing, new project, journal) opens as a persistent tab (F1–F12), each tab runs a context-aware, multi-turn Gemini session with persisted history and rich markdown rendering — primed for future subagent use.

**Architecture:** In-app conversation history with context injection. Each prompt assembles a preamble (project context + rolling 10-turn history) and sends it as a single `gemini -p "..." -o text` call. Turns are persisted to `.gemini/chat_history.json` per project so sessions survive restarts. A static `MarkdownRenderer` converts Gemini's markdown to Spectre.Console markup.

**Tech Stack:** .NET 10, Spectre.Console 0.54.0, xunit + FluentAssertions + Moq, `gemini` CLI (headless mode)

---

## Task 1: Add `ConversationTurn` to the model

**Files:**
- Modify: `Models/ProjectModels.cs`
- Modify: `NexusShell.Tests/HistoryServiceTests.cs` (add one smoke test)

**Step 1: Write the failing test**

Add to `NexusShell.Tests/HistoryServiceTests.cs`:
```csharp
[Fact]
public void ConversationTurn_ShouldRoundTripJson()
{
    var turn = new ConversationTurn { Role = "user", Content = "hello", Timestamp = new DateTime(2026, 3, 5, 10, 0, 0) };
    var json = System.Text.Json.JsonSerializer.Serialize(turn);
    var result = System.Text.Json.JsonSerializer.Deserialize<ConversationTurn>(json);
    result.Should().NotBeNull();
    result!.Role.Should().Be("user");
    result.Content.Should().Be("hello");
}
```

**Step 2: Run test to verify it fails**
```
cd C:\Users\flori\source\repos\conductor
dotnet test NexusShell.Tests --filter "ConversationTurn_ShouldRoundTripJson" -v minimal
```
Expected: FAIL — `ConversationTurn` does not exist.

**Step 3: Add `ConversationTurn` to `Models/ProjectModels.cs`**

Add after the `NeuralSession` class:
```csharp
/// <summary>
/// Represents a single turn in a Gemini conversation.
/// </summary>
public class ConversationTurn
{
    public string Role { get; set; } = "user";   // "user" or "ai"
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

Also extend `NeuralSession` — add this property:
```csharp
/// <summary>
/// Structured conversation turns used for context injection and persistence.
/// </summary>
public List<ConversationTurn> Turns { get; set; } = new();
```

**Step 4: Run test to verify it passes**
```
dotnet test NexusShell.Tests --filter "ConversationTurn_ShouldRoundTripJson" -v minimal
```
Expected: PASS

**Step 5: Commit**
```bash
git add NexusShell/Models/ProjectModels.cs NexusShell.Tests/HistoryServiceTests.cs
git commit -m "feat: add ConversationTurn model and extend NeuralSession"
```

---

## Task 2: `IChatPersistenceService` + `ChatPersistenceService`

**Files:**
- Modify: `Interfaces/IServices.cs`
- Create: `Services/ChatPersistenceService.cs`
- Create: `NexusShell.Tests/ChatPersistenceServiceTests.cs`

**Step 1: Write failing tests**

Create `NexusShell.Tests/ChatPersistenceServiceTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NexusShell.Models;
using NexusShell.Services;
using Xunit;

namespace NexusShell.Tests
{
    public class ChatPersistenceServiceTests : IDisposable
    {
        private readonly string _tempPath;

        public ChatPersistenceServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempPath)) Directory.Delete(_tempPath, true);
        }

        [Fact]
        public void LoadHistory_ShouldReturnEmpty_WhenFileDoesNotExist()
        {
            var svc = new ChatPersistenceService();
            var result = svc.LoadHistory(_tempPath);
            result.Should().BeEmpty();
        }

        [Fact]
        public void SaveAndLoad_ShouldRoundTrip()
        {
            var svc = new ChatPersistenceService();
            var turns = new List<ConversationTurn>
            {
                new() { Role = "user", Content = "hello", Timestamp = DateTime.Now },
                new() { Role = "ai",   Content = "world", Timestamp = DateTime.Now }
            };

            svc.SaveHistory(_tempPath, turns);
            var loaded = svc.LoadHistory(_tempPath);

            loaded.Should().HaveCount(2);
            loaded[0].Role.Should().Be("user");
            loaded[0].Content.Should().Be("hello");
            loaded[1].Role.Should().Be("ai");
        }

        [Fact]
        public void SaveHistory_ShouldCapAt50Turns()
        {
            var svc = new ChatPersistenceService();
            var turns = new List<ConversationTurn>();
            for (int i = 0; i < 60; i++)
                turns.Add(new ConversationTurn { Role = "user", Content = $"turn {i}" });

            svc.SaveHistory(_tempPath, turns);
            var loaded = svc.LoadHistory(_tempPath);

            loaded.Should().HaveCount(50);
            loaded[0].Content.Should().Be("turn 10"); // last 50 of 60
        }
    }
}
```

**Step 2: Run tests to verify they fail**
```
dotnet test NexusShell.Tests --filter "ChatPersistenceService" -v minimal
```
Expected: FAIL — type not found.

**Step 3: Add interface to `Interfaces/IServices.cs`**

Add after the `IRegistryService` interface:
```csharp
/// <summary>
/// Service for persisting per-project Gemini conversation history.
/// </summary>
public interface IChatPersistenceService
{
    /// <summary>
    /// Loads conversation turns from .gemini/chat_history.json in the given project path.
    /// Returns an empty list if the file does not exist.
    /// </summary>
    List<ConversationTurn> LoadHistory(string projectPath);

    /// <summary>
    /// Saves conversation turns (last 50) to .gemini/chat_history.json.
    /// </summary>
    void SaveHistory(string projectPath, List<ConversationTurn> turns);
}
```

**Step 4: Create `Services/ChatPersistenceService.cs`**
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Persists per-project Gemini conversation history to .gemini/chat_history.json.
    /// </summary>
    public class ChatPersistenceService : IChatPersistenceService
    {
        private const string HISTORY_FILE = ".gemini/chat_history.json";
        private const int MAX_STORED_TURNS = 50;
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        /// <inheritdoc />
        public List<ConversationTurn> LoadHistory(string projectPath)
        {
            string path = Path.Combine(projectPath, HISTORY_FILE);
            if (!File.Exists(path)) return new List<ConversationTurn>();
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ConversationTurn>>(json) ?? new();
            }
            catch { return new List<ConversationTurn>(); }
        }

        /// <inheritdoc />
        public void SaveHistory(string projectPath, List<ConversationTurn> turns)
        {
            string fullPath = Path.Combine(projectPath, HISTORY_FILE);
            string? dir = Path.GetDirectoryName(fullPath);
            try
            {
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var toSave = turns.TakeLast(MAX_STORED_TURNS).ToList();
                File.WriteAllText(fullPath, JsonSerializer.Serialize(toSave, _json));
            }
            catch { /* Silently fail — history is non-critical */ }
        }
    }
}
```

**Step 5: Run tests to verify they pass**
```
dotnet test NexusShell.Tests --filter "ChatPersistenceService" -v minimal
```
Expected: all 3 PASS

**Step 6: Commit**
```bash
git add NexusShell/Interfaces/IServices.cs NexusShell/Services/ChatPersistenceService.cs NexusShell.Tests/ChatPersistenceServiceTests.cs
git commit -m "feat: add IChatPersistenceService with 50-turn cap and round-trip persistence"
```

---

## Task 3: `MarkdownRenderer` static helper

**Files:**
- Create: `Services/MarkdownRenderer.cs`
- Create: `NexusShell.Tests/MarkdownRendererTests.cs`

**Step 1: Write failing tests**

Create `NexusShell.Tests/MarkdownRendererTests.cs`:
```csharp
using FluentAssertions;
using NexusShell.Services;
using Xunit;

namespace NexusShell.Tests
{
    public class MarkdownRendererTests
    {
        [Fact]
        public void Bold_ShouldConvertToSpectreMarkup()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("**hello**");
            result.Should().Be("[bold]hello[/]");
        }

        [Fact]
        public void H1_ShouldConvertToUnderlineCyan()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("# Title");
            result.Should().Be("[bold underline cyan]Title[/]");
        }

        [Fact]
        public void H2_ShouldConvertToBoldCyan()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("## Subtitle");
            result.Should().Be("[bold cyan]Subtitle[/]");
        }

        [Fact]
        public void InlineCode_ShouldConvertToMonospace()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("Use `dotnet run`");
            result.Should().Be("Use [cyan on grey15]dotnet run[/]");
        }

        [Fact]
        public void Bullet_ShouldConvertToBulletChar()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("- item one");
            result.Should().Be("• item one");
        }

        [Fact]
        public void SquareBrackets_ShouldBeEscaped()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("see [docs]");
            result.Should().Be("see [[docs]]");
        }

        [Fact]
        public void PlainText_ShouldPassThrough()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("just plain text");
            result.Should().Be("just plain text");
        }
    }
}
```

**Step 2: Run tests to verify they fail**
```
dotnet test NexusShell.Tests --filter "MarkdownRenderer" -v minimal
```
Expected: FAIL — type not found.

**Step 3: Create `Services/MarkdownRenderer.cs`**

```csharp
using System.Text.RegularExpressions;

namespace NexusShell.Services
{
    /// <summary>
    /// Converts Gemini markdown output to Spectre.Console markup strings.
    /// Order matters: escape brackets first, then apply markdown transforms.
    /// </summary>
    public static class MarkdownRenderer
    {
        public static string ToSpectreMarkup(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            // 1. Escape raw square brackets that are NOT part of our markup
            //    (applied before we inject any markup tags)
            string s = raw.Replace("[", "[[").Replace("]", "]]");

            // 2. Code blocks (``` ... ```) — render as a dim block
            //    Use a placeholder since we process line by line for bullets/headers
            s = Regex.Replace(s, @"```[\w]*\n([\s\S]*?)```",
                m => $"\n[grey on grey11]{m.Groups[1].Value.Trim()}[/]\n",
                RegexOptions.Multiline);

            // 3. Headers (must come before bold so # doesn't interfere)
            s = Regex.Replace(s, @"^## (.+)$", "[bold cyan]$1[/]", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^# (.+)$",  "[bold underline cyan]$1[/]", RegexOptions.Multiline);

            // 4. Bold **text**
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "[bold]$1[/]");

            // 5. Italic *text*
            s = Regex.Replace(s, @"\*(.+?)\*", "[italic]$1[/]");

            // 6. Inline code `text`
            s = Regex.Replace(s, @"`([^`]+)`", "[cyan on grey15]$1[/]");

            // 7. Bullet points
            s = Regex.Replace(s, @"^- (.+)$", "• $1", RegexOptions.Multiline);

            return s;
        }
    }
}
```

**Step 4: Run tests to verify they pass**
```
dotnet test NexusShell.Tests --filter "MarkdownRenderer" -v minimal
```
Expected: all 7 PASS

**Step 5: Commit**
```bash
git add NexusShell/Services/MarkdownRenderer.cs NexusShell.Tests/MarkdownRendererTests.cs
git commit -m "feat: add MarkdownRenderer converting Gemini output to Spectre markup"
```

---

## Task 4: Wire persistence + context injection into `UserInterface`

**Files:**
- Modify: `Services/UserInterface.cs`
- Modify: `Program.cs`

This task has no new tests (UserInterface is a TUI class; integration is manual). Focus is on wiring the new services.

**Step 1: Add `IChatPersistenceService` to `UserInterface` constructor**

In `Services/UserInterface.cs`, update the constructor parameters:
```csharp
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
    ILayoutService layoutService,
    IChatPersistenceService chatPersistence) : IUserInterface   // <-- add this
```

**Step 2: Load history on `InitializeWorkspace`**

Replace the existing `InitializeWorkspace` method:
```csharp
private void InitializeWorkspace(string name, string path, string extraArgs = "")
{
    if (!_activeWorkspaces.Contains(name))
    {
        _activeWorkspaces.Add(name);

        // Load persisted turns from disk
        var turns = chatPersistence.LoadHistory(path);

        var session = new NeuralSession
        {
            ProjectName = name,
            ProjectPath = path,
            ExtraArgs = extraArgs,
            Turns = turns
        };

        // Rebuild display history from persisted turns
        foreach (var t in turns)
        {
            string ts = t.Timestamp.ToString("HH:mm");
            if (t.Role == "user")
                session.History.Add($"[dim grey]{ts}[/] [bold cyan]YOU:[/] {Markup.Escape(t.Content)}");
            else
                session.History.Add($"[dim grey]{ts}[/] [bold green]AI:[/] {MarkdownRenderer.ToSpectreMarkup(t.Content)}");
        }

        if (turns.Count == 0)
            session.History.Add($"[dim grey]Neural Link established for {name}. Ready for input.[/]");

        _neuralSessions[name] = session;
    }

    _activeWorkspaceIndex = _activeWorkspaces.IndexOf(name);
    historyService.RecordLaunch(name);
    _needsRedraw = true;
}
```

**Step 3: Save after each AI response in `SubmitPrompt`**

Replace the existing `SubmitPrompt` method:
```csharp
private void SubmitPrompt(NeuralSession session, string prompt)
{
    string ts = DateTime.Now.ToString("HH:mm");
    var userTurn = new ConversationTurn { Role = "user", Content = prompt, Timestamp = DateTime.Now };
    session.Turns.Add(userTurn);
    session.History.Add($"[dim grey]{ts}[/] [bold cyan]YOU:[/] {Markup.Escape(prompt)}");
    session.IsProcessing = true;

    Task.Run(async () => {
        try {
            // Load project context for injection
            ProjectContext? ctx = null;
            lock (_dataLock)
            {
                var proj = _currentProjects.FirstOrDefault(p => p.Name == session.ProjectName);
                if (proj != null) ctx = proj.Context;
            }

            string response = await ExecuteContextualPrompt(session, prompt, ctx);
            string renderTs = DateTime.Now.ToString("HH:mm");

            var aiTurn = new ConversationTurn { Role = "ai", Content = response, Timestamp = DateTime.Now };
            session.Turns.Add(aiTurn);
            session.History.Add($"[dim grey]{renderTs}[/] [bold green]AI:[/] {MarkdownRenderer.ToSpectreMarkup(response)}");

            // Persist after each exchange
            chatPersistence.SaveHistory(session.ProjectPath, session.Turns);
        } catch (Exception ex) {
            session.History.Add($"[bold red]ERROR:[/] {Markup.Escape(ex.Message)}");
        } finally {
            session.IsProcessing = false;
            _needsRedraw = true;
        }
    });
}
```

**Step 4: Add `ExecuteContextualPrompt` — replaces `ExecuteHeadlessPrompt`**

Add this method (keep the old `ExecuteHeadlessPrompt` for now — it will be removed after this works):
```csharp
private async Task<string> ExecuteContextualPrompt(NeuralSession session, string prompt, ProjectContext? ctx)
{
    // Build preamble
    var sb = new System.Text.StringBuilder();

    // System context (only if we have project info)
    if (ctx != null)
    {
        sb.AppendLine($"You are an AI assistant for the project \"{session.ProjectName}\".");
        sb.AppendLine($"Objective: {ctx.Objective}");
        sb.AppendLine($"Status: {ctx.AgentStatus}");
        if (ctx.Resume.Count > 0) sb.AppendLine($"Last action: {ctx.Resume.Last()}");
        sb.AppendLine();
        sb.AppendLine("You are operating inside a mainframe terminal. Be concise and technical.");
        sb.AppendLine("Future subagents may act on your output, so structure responses clearly.");
        sb.AppendLine();
    }

    // Rolling conversation history (last 10 turns)
    var history = session.Turns.SkipLast(1).TakeLast(10).ToList(); // exclude the turn just added
    if (history.Count > 0)
    {
        sb.AppendLine("[Conversation so far]");
        foreach (var t in history)
            sb.AppendLine($"{t.Role.ToUpper()}: {t.Content}");
        sb.AppendLine();
    }

    sb.Append(prompt);

    return await ExecuteHeadlessPrompt(session.ProjectPath, sb.ToString(), session.ExtraArgs);
}
```

**Step 5: Update `RenderWorkspaceView` to show context header**

Replace the workspace header block (the part that draws `DrawWorkspaceHeader`). After calling `DrawWorkspaceHeader`, add:
```csharp
// Show turn count and last saved info
if (session.Turns.Count > 0)
{
    var last = session.Turns.Last();
    AnsiConsole.MarkupLine($"[dim grey]  {session.Turns.Count} turns · last: {last.Timestamp:HH:mm} · path: {session.ProjectPath}[/]");
}
```

**Step 6: Register `ChatPersistenceService` in `Program.cs`**

In `CreateHostBuilder`, inside `ConfigureServices`, add:
```csharp
services.AddSingleton<IChatPersistenceService, ChatPersistenceService>();
```

Update the `UserInterface` registration to pass the new service:
```csharp
services.AddSingleton<IUserInterface, UserInterface>(sp => new UserInterface(
    sp.GetRequiredService<NexusSettings>().ReposRoot,
    sp.GetRequiredService<NexusSettings>().ConductorRoot,
    sp.GetRequiredService<IProjectService>(),
    sp.GetRequiredService<IHistoryService>(),
    sp.GetRequiredService<ISessionOrchestrator>(),
    sp.GetRequiredService<IMarketingService>(),
    sp.GetRequiredService<IJournalService>(),
    sp.GetRequiredService<INewProjectService>(),
    sp.GetRequiredService<IRegistryService>(),
    sp.GetRequiredService<ILayoutService>(),
    sp.GetRequiredService<IChatPersistenceService>()));  // <-- add
```

Also update version to `v16.0`:
```csharp
private const string APP_VERSION = "v16.0";
```

**Step 7: Build to verify no compile errors**
```
cd C:\Users\flori\source\repos\conductor
dotnet build NexusShell -c Debug
```
Expected: Build succeeded, 0 errors.

**Step 8: Run all tests**
```
dotnet test NexusShell.Tests -v minimal
```
Expected: all tests pass.

**Step 9: Commit**
```bash
git add NexusShell/Services/UserInterface.cs NexusShell/Program.cs
git commit -m "feat: wire chat persistence, context injection, and markdown rendering (v16.0)"
```

---

## Task 5: Tab UX — ensure ALL features open as workspaces

**Files:**
- Modify: `Services/UserInterface.cs`

Currently `FOUNDER JOURNAL` and `NEW PROJECT` call synchronous `Execute()` methods (blocking the UI loop) instead of opening a tab. Fix this so they open as Gemini-backed workspaces, keeping the mainframe feel.

**Step 1: Replace blocking calls in `ExecuteSelection`**

Find these two lines in `ExecuteSelection`:
```csharp
else if (selection.Contains("FOUNDER JOURNAL")) journalService.Execute();
else if (selection.Contains("NEW PROJECT")) newProjectService.Execute();
```

Replace with workspace tabs:
```csharp
else if (selection.Contains("FOUNDER JOURNAL"))
    InitializeWorkspace("JOURNAL", Path.Combine(conductorRoot, "journal"), "--persona founder-journal");
else if (selection.Contains("NEW PROJECT"))
    InitializeWorkspace("SCAFFOLDER", Path.Combine(conductorRoot, "templates"), "--persona project-architect");
```

The `--persona` extra args prime Gemini's context for the use case (they are passed as part of context injection preamble, not as actual CLI flags).

Actually — these need to pass through `ExecuteHeadlessPrompt` which uses `-p` mode. Remove `--persona` from ExtraArgs (it's not a valid gemini flag) and instead make `InitializeWorkspace` accept an optional system prompt override:

```csharp
private void InitializeWorkspace(string name, string path, string extraArgs = "", string systemPromptOverride = "")
{
    if (!_activeWorkspaces.Contains(name))
    {
        var turns = chatPersistence.LoadHistory(path);
        var session = new NeuralSession
        {
            ProjectName = name,
            ProjectPath = path,
            ExtraArgs = extraArgs,
            SystemPrompt = systemPromptOverride,  // see Step 2
            Turns = turns
        };
        // ... rest unchanged
    }
}
```

**Step 2: Add `SystemPrompt` property to `NeuralSession`**

In `Models/ProjectModels.cs`, add to `NeuralSession`:
```csharp
/// <summary>
/// Optional system prompt override injected before conversation history.
/// Used for non-project workspaces (Journal, Scaffolder, Meta).
/// </summary>
public string SystemPrompt { get; set; } = "";
```

**Step 3: Use `SystemPrompt` in `ExecuteContextualPrompt`**

In the context preamble section, add after the `if (ctx != null)` block:
```csharp
if (!string.IsNullOrEmpty(session.SystemPrompt))
{
    sb.AppendLine(session.SystemPrompt);
    sb.AppendLine();
}
```

**Step 4: Update workspace initialization calls with system prompts**

```csharp
// In ExecuteSelection:
else if (selection.Contains("FOUNDER JOURNAL"))
    InitializeWorkspace("JOURNAL", Path.Combine(conductorRoot, "journal"),
        systemPromptOverride: "You are a founder journal AI. Help reflect on daily progress, decisions, and lessons learned. Be thoughtful and strategic.");

else if (selection.Contains("NEW PROJECT"))
    InitializeWorkspace("SCAFFOLDER", Path.Combine(conductorRoot, "templates"),
        systemPromptOverride: "You are a project architect. Help design and scaffold new enterprise projects. Ask clarifying questions and propose concrete file structures.");

else if (selection.Contains("MARKETING ASSISTANT"))
    InitializeWorkspace("MARKETING", reposRoot,
        systemPromptOverride: "You are a marketing AI for a solo founder building SaaS products. Generate 'Build in Public' content, landing copy, and growth strategies.");
```

**Step 5: Build**
```
dotnet build NexusShell -c Debug
```
Expected: 0 errors.

**Step 6: Run tests**
```
dotnet test NexusShell.Tests -v minimal
```
Expected: all pass.

**Step 7: Commit**
```bash
git add NexusShell/Models/ProjectModels.cs NexusShell/Services/UserInterface.cs
git commit -m "feat: all features open as Gemini workspace tabs with role-specific system prompts"
```

---

## Task 6: Final build + publish

**Step 1: Full test run with coverage**
```
cd C:\Users\flori\source\repos\conductor
dotnet test NexusShell.Tests /p:CollectCoverage=true /p:CoverletOutputFormat=opencover -v minimal
```
Expected: all pass.

**Step 2: Build release**
```
dotnet build NexusShell -c Release
```
Expected: 0 errors, 0 warnings.

**Step 3: Smoke test manually**
Run the shell:
```
dotnet run --project NexusShell -c Debug
```
Verify:
- [ ] Dashboard loads, v16.0 shown in header
- [ ] Navigate to a project, press Enter → opens as a new tab (F2+)
- [ ] Type a prompt, press Enter → context preamble is sent, response renders with markdown
- [ ] Press Esc → return to Hub (F1)
- [ ] Kill and restart → workspace history reloads from disk

**Step 4: Final commit**
```bash
git add -A
git commit -m "feat: NexusShell v16.0 - mainframe Gemini workspaces with persistence and markdown"
```

---

## Summary of New/Changed Files

| File | Status |
|---|---|
| `Models/ProjectModels.cs` | Modified — `ConversationTurn`, `NeuralSession.Turns`, `NeuralSession.SystemPrompt` |
| `Interfaces/IServices.cs` | Modified — add `IChatPersistenceService` |
| `Services/ChatPersistenceService.cs` | **New** |
| `Services/MarkdownRenderer.cs` | **New** |
| `Services/UserInterface.cs` | Modified — persistence wiring, context injection, tab UX |
| `Program.cs` | Modified — register service, version bump |
| `NexusShell.Tests/ChatPersistenceServiceTests.cs` | **New** |
| `NexusShell.Tests/MarkdownRendererTests.cs` | **New** |
