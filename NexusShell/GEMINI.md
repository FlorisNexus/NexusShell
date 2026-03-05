# NexusShell Project Context

## 📋 Coding Standards
- Follow `.NET 10` and `C# 14` standards.
- Use **Primary Constructors** for DI.
- **XML Documentation** is mandatory for all public/internal members.
- UI styling must adhere to the high-fidelity **Spectre.Console** cyberpunk aesthetic.

## 📦 Versioning Mandate
- **Critical:** You MUST increment the `APP_VERSION` constant in `Program.cs` at every iteration or after implementing a significant new feature or architectural change.
- **Alignment:** Always synchronize the version in the PowerShell profile loading message if modified.

## 🏛️ Architecture
- **LayoutService:** Handles all dashboard drawing (Header, Strategic panel).
- **ProjectService:** Handles multi-track discovery and Git status.
- **HistoryService:** Manages persistent JSON-based activity logging.
- **SessionOrchestrator:** Hooks into process events to track live sessions.
