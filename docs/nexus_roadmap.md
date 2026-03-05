# Nexus Command Center - Future Roadmap

This document tracks planned improvements for the NexusShell (v10.5+) to continue its evolution into a professional AI-OS.

## 🚀 Priority Backlog

### 1. ⚙️ External Configuration (`appsettings.json`) [COMPLETED]
- **Objective:** Remove hardcoded paths from `Program.cs`.
- **Implementation:** 
  - Use `Microsoft.Extensions.Configuration`.
  - Moved `REPOS_ROOT` and `CONDUCTOR_ROOT` to `appsettings.json`.
  - Added dynamic config loading with fallback support.

### 2. 🧹 Full Script Migration (`sync-tracks`)
- **Objective:** Eliminate the last remaining `.ps1` dependencies.
- **Implementation:** 
  - Migrate logic from `sync-tracks.ps1` into a native C# `RegistryService`.
  - Implement Git maintenance tasks (e.g., `git fetch --prune`) within the hub.

### 3. 🔔 Windows Notifications (Toast)
- **Objective:** Notify the user when background sessions change state.
- **Implementation:** 
  - Use `Microsoft.Toolkit.Uwp.Notifications`.
  - Send toasts when a "Neural Link" is successfully established or closed.
  - Alert on critical errors or required Git pulls.

### 4. 📊 Project Deep-Dive View
- **Objective:** View more detailed stats for a specific project before launching.
- **Implementation:** 
  - New "Project Details" screen.
  - Show commit history, branch visualization, and extended launch logs.

### 5. 🛠️ Global CLI Plugin System
- **Objective:** Allow adding new features to the Nexus menu without recompiling.
- **Implementation:** 
  - Dynamic loading of assemblies or script-based plugins.
