# 🏗️ Nexus Neural OS: Master Execution Plan (v14.0)

This plan outlines the evolution of the Nexus Hub from a launcher into a true "Neural OS Kernel" that manages enterprise-wide multitasking and persistent intelligence.

## 🏛️ Strategic Vision
To provide a centralized "Mainframe" command center that manages background subagents and provides a deep, persistent "Neural Link" context across the entire enterprise (`sources/repos`).

---

## 📅 Roadmap: The 4-Phase Sprint

### Phase 1: Enterprise Intelligence Mesh (The "Memory")
- **The Context Standard**: Define the `nexus_context.json` schema.
- **The Context Service**: Implement `IContextService` to manage project state.
- **Project Briefing UI**: Build the `Intelligence Briefing` pane for the Dashboard.
- **The Live Resume**: Implement the automated "Resume of what was done" for each project.

### Phase 2: Mainframe Command Center (The "Dashboard")
- **Dual-Pane Layout**: Implement `Spectre.Console.Layout` for side-by-side Fleet View and Briefing.
- **Categorized Menu**: Elegant project grouping (SAAS, LOCAL, TOOLS, ACTIVE AGENTS).
- **Background Status**: Display active background subagent progress bars in the project menu.

### Phase 3: Focused Workspace Engine (The "Window")
- **Workspace State**: Implement the transition between Dashboard and Focused Workspaces.
- **Sticky Header**: Fixed "Briefing + Plan" header at the top of active sessions.
- **Neural Link Integration**: Seamless interactive Gemini session within the Hub's terminal window.

### Phase 4: Neural Kernel (The "Multitasking")
- **Process Management**: Persistent background sessions that stay "alive" when you toggle back to the Dashboard.
- **Agent Orchestrator**: Logic to spawn headless subagents (e.g., for security auditing or marketing generation).
- **Subagent Delegation**: The Hub can delegate a list of tasks to a background agent.

---

## 🛠️ Components & Services
| Component | Responsibility |
| :--- | :--- |
| `ContextService` | Reading/Writing `nexus_context.json` across the enterprise. |
| `LayoutService` | Managing the TUI Layout (Spectre.Console). |
| `SessionManager` | Tracking background processes and their output buffers. |
| `AgentOrchestrator` | Spawning and monitoring headless Gemini subagents. |

---

## 🧠 Smart v14.0 Improvements
- **Ghost Suggestions**: AI-driven tips based on recent project events (build failures, etc.).
- **Unified Neural Search**: Search all project history from a single Hub prompt.
- **Neural Status Bar**: Real-time stats (CPU, Context Tokens, Agent Count).
