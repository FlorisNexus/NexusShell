using System;

namespace NexusShell.Models
{
    /// <summary>
    /// Represents a single activity event in the Nexus history.
    /// </summary>
    /// <param name="Timestamp">The time the event occurred.</param>
    /// <param name="Message">The description of the event.</param>
    public record HistoryEvent(DateTime Timestamp, string Message);

    /// <summary>
    /// Represents persistent statistics for a project or neural track.
    /// </summary>
    public class ProjectStats
    {
        /// <summary>
        /// Gets or sets the total number of times the session was launched.
        /// </summary>
        public int OpenCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the last time the session was launched.
        /// </summary>
        public DateTime? LastOpened { get; set; }
    }

    /// <summary>
    /// Represents metadata and status for a directory-based project.
    /// </summary>
    public class ProjectInfo
    {
        /// <summary>
        /// Gets or sets the folder name of the project.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the absolute filesystem path.
        /// </summary>
        public required string Path { get; set; }

        /// <summary>
        /// Gets or sets the repository type (Mono, Multi, Folder).
        /// </summary>
        public string Type { get; set; } = "Folder";

        /// <summary>
        /// Gets or sets the strategic track (Branch 1: Local, Branch 2: SaaS, Branch 3: Other).
        /// </summary>
        public string Track { get; set; } = "Other";

        /// <summary>
        /// Gets or sets the active git branch.
        /// </summary>
        public string Branch { get; set; } = "-";

        /// <summary>
        /// Gets or sets a value indicating whether there are uncommitted changes.
        /// </summary>
        public bool HasChanges { get; set; } = false;

        /// <summary>
        /// Gets or sets the remote sync status (e.g. ahead 2, behind 1).
        /// </summary>
        public string RemoteStatus { get; set; } = "";

        /// <summary>
        /// Gets or sets the launch count from persistent stats.
        /// </summary>
        public int OpenCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the last opened time from persistent stats.
        /// </summary>
        public DateTime? LastOpened { get; set; }
    }
}
