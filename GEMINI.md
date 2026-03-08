# NexusShell Project Context

## 🤝 AI Collaboration

This project is worked on by **two AI agents**:
- **Gemini** (you): daily coding, implementation, refactoring.
- **Claude**: architecture, plans, ADRs, specs — stored in `conductor/plans/` at the workspace root.

Before implementing a significant feature, check `conductor/plans/` for a `nexusshell-plan-*` or `nexusshell-spec-*` file.
After completing work, update `CHANGELOG.md` (`[Unreleased]` section) and mark the plan `[DONE]`.

---

## 📋 Coding Standards
- Follow `.NET 10` and `C# 14` standards.
- Use **Primary Constructors** for DI.
- **XML Documentation** is mandatory for all public/internal members.
- UI styling must adhere to the high-fidelity **Spectre.Console** cyberpunk aesthetic.

## 📦 Versioning & Profile Mandate
- **Critical:** You MUST increment the `APP_VERSION` constant in `Program.cs` at every iteration or after implementing a significant new feature or architectural change.
- **PowerShell Sync:** You MUST also update the version number in the PowerShell `$PROFILE` (located at `C:\Users\flori\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1`) to ensure the loading message matches the Hub's version.
- **Alignment:** Always synchronize the version in the PowerShell profile loading message and header if modified.

## 🚀 Post-Iteration Automation
- **Mandatory Workflow:** After EVERY significant change or iteration, you MUST:
  1. **Build:** `dotnet build -c Release`
  2. **Test:** `dotnet test --no-restore`
  3. **Validate:** Ensure the UI remains responsive and features work as intended.
  4. **Commit:** Atomic commit with descriptive message.
  5. **Push:** `git push origin main`

## 🏛️ Architecture
- **LayoutService:** Handles all dashboard drawing (Header, Strategic panel).
- **ProjectService:** Handles multi-track discovery and Git status.
- **HistoryService:** Manages persistent JSON-based activity logging.
- **SessionOrchestrator:** Hooks into process events to track live sessions.
