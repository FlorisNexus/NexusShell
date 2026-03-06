using System;
using System.Collections.Generic;
using Spectre.Console.Rendering;
using NexusShell.Models;

namespace NexusShell.Interfaces
{
    /// <summary>
    /// Service for managing persistent project statistics.
    /// </summary>
    public interface IHistoryService
    {
        /// <summary>
        /// Loads the stats for all projects from disk.
        /// </summary>
        Dictionary<string, ProjectStats> LoadStats();

        /// <summary>
        /// Increments the launch count and updates the last opened time for a project.
        /// </summary>
        void RecordLaunch(string projectName);

        /// <summary>
        /// Loads the recent activity events from disk.
        /// </summary>
        List<HistoryEvent> GetRecentEvents();

        /// <summary>
        /// Adds a new activity event to the history.
        /// </summary>
        void AddEvent(string message);

        /// <summary>
        /// Clears all project statistics and activity history.
        /// </summary>
        void ClearAll();
    }

    /// <summary>
    /// Service for managing persistent project intelligence and context (Resumes/Plans).
    /// </summary>
    public interface IContextService
    {
        /// <summary>
        /// Loads the context for a specific project from its .gemini folder.
        /// </summary>
        ProjectContext LoadContext(string projectPath);

        /// <summary>
        /// Saves the context for a specific project.
        /// </summary>
        void SaveContext(string projectPath, ProjectContext context);
    }

    /// <summary>
    /// Service for scanning and managing workspace repositories.
    /// </summary>
    public interface IProjectService
    {
        /// <summary>
        /// Gets all visible neural tracks (projects) from the repository root.
        /// </summary>
        List<ProjectInfo> GetProjects();
        
        /// <summary>
        /// Refreshes the local track registry (git status, etc.).
        /// </summary>
        void RefreshTracks();

        /// <summary>
        /// Gets the current git diff for a project.
        /// </summary>
        string GetProjectDiff(string path);

        /// <summary>
        /// Performs a git pull --rebase and git push for a project.
        /// </summary>
        void SyncProject(string path);
    }

    /// <summary>
    /// Service for synchronizing Conductor metadata to the cloud.
    /// </summary>
    public interface ICloudSyncService
    {
        /// <summary>
        /// Synchronizes the local conductor folder to the cloud storage.
        /// </summary>
        void SyncToCloud();

        /// <summary>
        /// Synchronizes the cloud storage back to the local conductor folder.
        /// </summary>
        void SyncFromCloud();
    }

    /// <summary>
    /// Service for orchestrating external Gemini sessions.
    /// </summary>
    public interface ISessionOrchestrator
    {
        /// <summary>
        /// Spawns a new Gemini CLI session for the specified project.
        /// </summary>
        void LaunchGemini(string name, string path, string args = "");

        /// <summary>
        /// Determines if a specific project has an active open session window.
        /// </summary>
        bool IsSessionActive(string name);
    }

    /// <summary>
    /// Service for consistent dashboard layout and headers.
    /// </summary>
    public interface ILayoutService
    {
        /// <summary>
        /// Clears the console and draws the main Hero Header and Strategic Intelligence panel.
        /// </summary>
        void RefreshHeader();

        /// <summary>
        /// Returns the top-level workspace tab bar for multi-tasking navigation.
        /// </summary>
        IRenderable GetTabBar(List<string> tabs, int activeIndex);

        /// <summary>
        /// Returns the Hero Header (ASCII art and version).
        /// </summary>
        IRenderable GetHeroHeader();

        /// <summary>
        /// Returns the Strategic Intelligence panel.
        /// </summary>
        IRenderable GetStrategicFocus();

        /// <summary>
        /// Returns the deep intelligence briefing for a specific project.
        /// </summary>
        IRenderable GetProjectBriefing(ProjectInfo project);
    }

    /// <summary>
    /// Service for calling the local Gemini CLI directly.
    /// </summary>
    public interface ICliExecutionService
    {
        /// <summary>
        /// Executes a headless prompt using the local gemini CLI and yields output incrementally.
        /// </summary>
        IAsyncEnumerable<string> StreamPromptAsync(string workingDirectory, string prompt, string extraArgs = "");
        
        /// <summary>
        /// Executes a headless prompt and returns the full output.
        /// </summary>
        Task<string> ExecutePromptAsync(string workingDirectory, string prompt, string extraArgs = "");
    }

    /// <summary>
    /// Service for managing the CLI dashboard and user interactions.
    /// </summary>
    public interface IUserInterface
    {
        /// <summary>
        /// Starts the main application loop.
        /// </summary>
        void Run();
    }

    /// <summary>
    /// Service for maintaining the markdown tracks registry.
    /// </summary>
    public interface IRegistryService
    {
        /// <summary>
        /// Updates the conductor/tracks.md file with the latest project status.
        /// </summary>
        void UpdateRegistry(List<ProjectInfo> projects);
    }

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
}
