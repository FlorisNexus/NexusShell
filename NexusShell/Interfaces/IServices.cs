using System;
using System.Collections.Generic;
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
        /// Draws only the Hero Header (ASCII art and version).
        /// </summary>
        void DrawHeroHeader();

        /// <summary>
        /// Draws only the Strategic Intelligence panel.
        /// </summary>
        void DrawStrategicFocus();
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
    /// Service for the Marketing Command Center feature.
    /// </summary>
    public interface IMarketingService
    {
        /// <summary>
        /// Executes the marketing assistant interactive flow.
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Service for the Founder's Daily Journal feature.
    /// </summary>
    public interface IJournalService
    {
        /// <summary>
        /// Executes the journal entry interactive flow.
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Service for the Enterprise Scaffolding feature.
    /// </summary>
    public interface INewProjectService
    {
        /// <summary>
        /// Executes the project scaffolding interactive flow.
        /// </summary>
        void Execute();
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
}
