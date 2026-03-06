# CLI → API Migration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove all Gemini CLI subprocess usage from UserInterface; route META-WORKSPACE through the REST API with a rich file-based system instruction.

**Architecture:** Delete `ExecuteCliPrompt` and `ExecuteHeadlessPrompt`. Add `BuildMetaSystemInstruction()` that reads `GEMINI.md`, `tracks.md`, and in-memory project contexts. `ExecuteContextualPrompt` detects META-WORKSPACE by name and uses that instruction instead of the CLI fallback.

**Tech Stack:** .NET 10, `System.IO.File`, `IGeminiApiService` (already wired)

---

### Task 1: Remove CLI routing from `ExecuteContextualPrompt`

**File:** `Services/UserInterface.cs`

**Step 1: Remove the CLI fallback block**

Delete these lines from `ExecuteContextualPrompt` (currently lines 439–441):

```csharp
// META-WORKSPACE uses CLI (needs --include-directories for file reading)
if (!string.IsNullOrEmpty(session.ExtraArgs))
    return await ExecuteCliPrompt(session, prompt, ctx);
```

**Step 2: Add META-WORKSPACE detection + system instruction**

Replace the existing `if (!string.IsNullOrEmpty(session.SystemPrompt)) ... else if (ctx != null)` block with:

```csharp
var sb = new StringBuilder();
if (session.ProjectName == "UNIFIED ECOSYSTEM")
{
    sb.Append(BuildMetaSystemInstruction());
}
else if (!string.IsNullOrEmpty(session.SystemPrompt))
{
    sb.AppendLine(session.SystemPrompt);
}
else if (ctx != null)
{
    sb.AppendLine($"You are an AI for project \"{session.ProjectName}\".");
    sb.AppendLine($"Objective: {ctx.Objective}");
    sb.AppendLine($"Status: {ctx.AgentStatus}");
    if (ctx.Resume.Count > 0) sb.AppendLine($"Last action: {ctx.Resume.Last()}");
    sb.AppendLine("Be concise and technical. Structure output clearly for future subagents.");
}
```

The rest of the method (history snapshot + `geminiApi.GenerateAsync` call) stays unchanged.

**Step 3: Build — confirm no new errors**

```
dotnet build NexusShell -p:RestoreSources="https://api.nuget.org/v3/index.json;C:\Users\flori\.nuget\packages"
```

Expected: build succeeds (CLI methods still exist at this point).

---

### Task 2: Add `BuildMetaSystemInstruction()`

**File:** `Services/UserInterface.cs`

**Step 1: Add the method** (insert after `ExecuteContextualPrompt`, before `ExecuteCliPrompt`):

```csharp
private string BuildMetaSystemInstruction()
{
    var sb = new StringBuilder();

    // Strategic context from GEMINI.md at repo root
    string geminiMd = Path.Combine(reposRoot, "GEMINI.md");
    if (File.Exists(geminiMd))
        sb.AppendLine(File.ReadAllText(geminiMd));

    // Live project registry
    string tracksMd = Path.Combine(conductorRoot, "tracks.md");
    if (File.Exists(tracksMd))
    {
        sb.AppendLine("\n--- LIVE PROJECT REGISTRY ---");
        sb.AppendLine(File.ReadAllText(tracksMd));
    }

    // Per-project objectives and status from in-memory contexts
    List<ProjectInfo> projects;
    lock (_dataLock) { projects = new List<ProjectInfo>(_currentProjects); }
    if (projects.Count > 0)
    {
        sb.AppendLine("\n--- PROJECT CONTEXTS ---");
        foreach (var p in projects)
        {
            sb.AppendLine($"\n## {p.Name}");
            sb.AppendLine($"Path: {p.Path}");
            sb.AppendLine($"Objective: {p.Context.Objective}");
            sb.AppendLine($"Status: {p.Context.AgentStatus}");
            if (p.Context.Resume.Count > 0)
                sb.AppendLine($"Recent: {string.Join(" | ", p.Context.Resume.Take(3))}");
        }
    }

    return sb.ToString();
}
```

**Step 2: Build**

```
dotnet build NexusShell -p:RestoreSources="https://api.nuget.org/v3/index.json;C:\Users\flori\.nuget\packages"
```

Expected: succeeds.

---

### Task 3: Delete CLI methods and dead code

**File:** `Services/UserInterface.cs`

**Step 1: Delete `ExecuteCliPrompt`** — the entire method (currently ~lines 466–501).

**Step 2: Delete `ExecuteHeadlessPrompt`** — the entire method (currently ~lines 503–549).

**Step 3: Remove the `includeArgs` construction from `ExecuteSelection`**

Find the META-WORKSPACE branch in `ExecuteSelection` (currently ~lines 564–569):

```csharp
else if (selection.Contains("META-WORKSPACE"))
{
    List<string> allDirs;
    lock(_dataLock) { allDirs = _currentProjects.Select(p => p.Name).ToList(); }
    string includeArgs = "--include-directories " + string.Join(" ", allDirs.Select(d => $".\\{d}"));
    InitializeWorkspace("UNIFIED ECOSYSTEM", reposRoot, includeArgs);
}
```

Replace with:

```csharp
else if (selection.Contains("META-WORKSPACE"))
{
    InitializeWorkspace("UNIFIED ECOSYSTEM", reposRoot);
}
```

**Step 4: Remove `using System.Diagnostics;`** from the top of the file — it was only needed for `ProcessStartInfo`.

**Step 5: Build**

```
dotnet build NexusShell -p:RestoreSources="https://api.nuget.org/v3/index.json;C:\Users\flori\.nuget\packages"
```

Expected: **0 errors, 0 warnings**.

---

### Task 4: Run full test suite

**Step 1: Run tests**

```
dotnet test NexusShell.Tests -p:RestoreSources="https://api.nuget.org/v3/index.json;C:\Users\flori\.nuget\packages"
```

Expected: `Passed! - Failed: 0, Passed: 21`

**Step 2: Commit**

```
git add NexusShell/Services/UserInterface.cs NexusShell/docs/plans/2026-03-06-cli-to-api-migration.md
git commit -m "feat: migrate META-WORKSPACE from CLI subprocess to REST API with file-based context"
```

---

## Verification checklist

- `ExecuteCliPrompt` — deleted
- `ExecuteHeadlessPrompt` — deleted
- `using System.Diagnostics` — removed
- `includeArgs` / `--include-directories` — gone from `ExecuteSelection`
- META-WORKSPACE sends `BuildMetaSystemInstruction()` as system instruction to `geminiApi.GenerateAsync`
- 21 tests pass
