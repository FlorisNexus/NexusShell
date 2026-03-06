# NexusShell Gemini Mainframe Design (v16.0)
_Date: 2026-03-05_

## Vision

Evolve NexusShell from a project launcher into a true mainframe terminal: persistent, context-aware Gemini workspaces that remember conversations, render responses beautifully, and know each project's objective and status — without leaving the TUI or using the Gemini CLI interactively.

## Constraints

- Gemini integration stays as `gemini -p "..." -o text` (headless CLI)
- No API key / no separate billing — uses the user's Google Pro subscription via OAuth
- Runtime: .NET 10, Spectre.Console, Windows terminal

---

## Architecture

### Approach: In-app conversation history + context injection (Approach C)

NexusShell owns the full conversation model. Each prompt sent to the CLI includes the rolling conversation history and project context as a preamble. History is persisted to disk so workspaces survive restarts.

---

## Section 1: Data Model

**New: `ConversationTurn` record** (added to `Models/ProjectModels.cs`)
```
Role: "user" | "ai"
Content: string (raw, unescaped)
Timestamp: DateTime
```

**Extended: `NeuralSession`**
- Add `List<ConversationTurn> Turns` — structured data for injection and persistence
- Existing `List<string> History` — display-ready Spectre markup strings, derived from Turns at render time

---

## Section 2: Chat Persistence

**New: `IChatPersistenceService` / `ChatPersistenceService`**

| Method | Behavior |
|---|---|
| `LoadHistory(string projectPath)` | Reads `.gemini/chat_history.json`, returns `List<ConversationTurn>` |
| `SaveHistory(string projectPath, List<ConversationTurn> turns)` | Writes turns (capped at last 50) |

- Called on `InitializeWorkspace()` to rehydrate the session from disk
- Called after each completed AI response to persist the new turn
- Storage: `.gemini/chat_history.json` per project (alongside existing `nexus_context.json`)
- Cap: **50 turns stored**, **10 turns injected** per prompt call

---

## Section 3: Context Injection

`ExecuteHeadlessPrompt` is extended to accept `List<ConversationTurn> history` and `ProjectContext context`.

**Prompt preamble format:**
```
You are an AI assistant for the project "{ProjectName}".
Objective: {context.Objective}
Status: {context.AgentStatus}
Recent work: {context.Resume.LastOrDefault() ?? "none"}

[Conversation so far]
USER: {turn.Content}
AI: {turn.Content}
...

Now respond to:
{newPrompt}
```

- Turn 0: full project context included
- Subsequent turns: rolling 10-turn window only (no repeated system header)
- The assembled string is passed as the `-p "..."` argument

---

## Section 4: Markdown Rendering

**New: `MarkdownRenderer` static class** (`Services/MarkdownRenderer.cs`)

Converts Gemini markdown output to Spectre.Console markup:

| Markdown | Spectre Output |
|---|---|
| `**text**` | `[bold]text[/]` |
| `# Heading` | `[bold underline cyan]Heading[/]` |
| `## Heading` | `[bold cyan]Heading[/]` |
| `` `code` `` | `[cyan on grey15]code[/]` |
| ```` ```block``` ```` | dim panel with grey border |
| `- item` | `• item` |
| Raw `[` / `]` | Escaped to `[[` / `]]` |

Applied to every AI response before it is added to `History`.

---

## Section 5: UI Polish

- **Workspace header**: shows project objective, turn count, last save timestamp
- **Turn timestamps**: each rendered turn prefixed with `[dim grey]HH:mm[/]`
- **Thinking state**: Spectre spinner rendered inline in history panel (replaces static text)
- **Word-wrap**: Spectre's markup handles wrapping automatically in panel context

---

## Files Changed

| File | Change |
|---|---|
| `Models/ProjectModels.cs` | Add `ConversationTurn` record; extend `NeuralSession` with `Turns` |
| `Interfaces/IServices.cs` | Add `IChatPersistenceService` interface |
| `Services/ChatPersistenceService.cs` | **New** — load/save `.gemini/chat_history.json` |
| `Services/MarkdownRenderer.cs` | **New** — static markdown-to-Spectre converter |
| `Services/UserInterface.cs` | Wire persistence, context injection, markdown rendering, updated workspace header |
| `Program.cs` | Register `ChatPersistenceService` in DI |

---

## Out of Scope

- Streaming output (Gemini CLI headless mode returns full response only)
- Gemini CLI native session IDs (fragile, undocumented)
- API key / Vertex AI integration
