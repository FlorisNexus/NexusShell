# Diff Viewer and Fleet Sync Implementation Plan (v16.4)

## Objective
Implement two major Workflow Multipliers for the NexusShell Mainframe:
1. **Split-Pane Diff Viewer:** Dynamically split the workspace tab to show `git diff` output when a project has uncommitted changes, allowing for instant code review alongside the AI chat.
2. **One-Click Fleet Sync:** Add a global hotkey (`S`) to the Hub that synchronizes (pull rebase + push) all repositories, or a specific repository, seamlessly.

## 1. Split-Pane Diff Viewer
- **Update `ProjectService`:** Add a method `GetProjectDiff(string path)` that executes `git diff --color=always` (or parses standard diff to markup) and returns the string. Since Spectre.Console supports markup, we'll strip raw ANSI and use a simple formatter, or let Spectre handle it if we use standard text. Actually, standard text `git diff` is best parsed into a `Markup` or colored `Table`.
- **Update `UserInterface.RenderWorkspaceView`:** 
  - Check if the active workspace is a valid Git project.
  - Call `GetProjectDiff`.
  - If the diff is not empty, change the layout from a single column to two columns: `[History (60%)] | [Diff (40%)]`.
  - The Diff panel will be scrollable or truncated to fit the fixed height.

## 2. One-Click Fleet Sync
- **Update `UserInterface.HandleInput`:**
  - Add `case ConsoleKey.S:` in the Hub view.
- **Implement `TriggerFleetSync`:**
  - If a specific project is selected, sync just that project.
  - If the "META-WORKSPACE" is selected, loop through `_currentProjects` and sync all of them.
  - Show a modal popup while syncing: "Syncing [Project]... Pulling... Pushing...".
  - Update `ProjectService` to execute `git pull --rebase` and `git push` securely.

## Steps
1. Add `GetProjectDiff` and `SyncProject` to `IProjectService` and `ProjectService`.
2. Modify `UserInterface.cs` to capture `S` key.
3. Modify `UserInterface.cs` to split the UI if a diff exists.
4. Build, test, and push.