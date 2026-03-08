# NexusShell

CLI orchestration hub for the FlorisNexus workspace. Provides a dashboard showing all projects,
their Git status, and session tracking. Published as a single-file Windows executable.

## Tech Stack

- **Runtime:** .NET 10 (C# 14), console app
- **UI:** Spectre.Console (cyberpunk aesthetic — maintain high-fidelity styling)
- **Publish:** Single-file self-contained, win-x64

## Architecture

```
NexusShell/
  Program.cs          # Entry point, APP_VERSION constant
  Services/
    LayoutService     # Dashboard drawing (header, strategic panel)
    ProjectService    # Multi-track Git status discovery
    HistoryService    # Persistent JSON activity logging
    SessionOrchestrator # Live session tracking via process events
  Models/
  Interfaces/
NexusShell.Tests/     # xUnit tests
```

## Versioning Mandate

**APP_VERSION** in `Program.cs` must be incremented on every significant change.
The matching version string in the PowerShell profile (`$PROFILE`) must also be updated to stay in sync.

## Build & Test

```bash
dotnet build -c Release
dotnet test --no-restore
```

## Coding Standards

Follows workspace conventions (root CLAUDE.md) plus:
- XML docs mandatory on all public/internal members
- Spectre.Console icons and spacing must stay aligned — do not break the visual layout
