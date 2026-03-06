# 🗺️ Nexus Neural C2: Enterprise Ecosystem Roadmap

## Objective
Transition from a "Launcher-based Hub" to a "Client-Kernel-Worker" architecture. 
Goal: <100ms response latency, cross-project intelligence, and team-ready scalability.

---

## 🚀 Phase 1: The High-Speed Bridge (v16.5)
**Focus:** Immediate Performance & Latency Elimination.

*   **Architectural Shift:** Decouple the Hub (C#) from the Gemini startup logic.
*   **The "Nexus Kernel":** A persistent Node.js daemon that keeps the Gemini framework and API connections "warm" in memory.
*   **Workflow Integration:**
    - Hub auto-starts the Kernel on boot via a background process.
    - `ICliExecutionService` switches from `Process.Start` to `HttpClient` streaming.
    - Time-to-first-token drops from 3s to ~100ms.
*   **UI Update:** Add a "Neural Mesh Status" indicator to the dashboard header.

## 🧠 Phase 2: Proactive Intelligence & Context Compaction (v17.0)
**Focus:** Persistence & Autonomous Repo Management.

*   **Cross-Project Memory:** 
    - The Kernel monitors the `conductor/` folder in real-time.
    - "Morning Standup" logic: Kernel pre-analyzes Git logs and Journal entries before you even open the Hub.
*   **Dynamic Focus Mode:**
    - Kernel automatically generates and maintains `.geminiignore` based on your active "Task State."
*   **Split-Pane Diffing Enhancement:** Direct gRPC integration for streaming code diffs into the UI without shell overhead.

## 🐝 Phase 3: The Enterprise Hive (v18.0+)
**Focus:** Scaling, Cloud Sync & Team Collaboration.

*   **Architectural Shift:** Client (TUI/Desktop) <--> Enterprise Kernel (Local or Azure-hosted).
*   **Cloud Memory Sync:**
    - Securely sync `conductor/` metadata to an Azure SQL/CosmosDB store.
    - Access your "Founder Journal" and "Tracks Registry" from any machine.
*   **Automated Scaffolding Workers:** 
    - Full execution of Bicep/CI-CD templates via specialized MCP worker nodes.
*   **Team Readiness:** Multi-user support with role-based access to specific project contexts (Branch 1 vs. Branch 2).
