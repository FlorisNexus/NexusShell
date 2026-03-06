using System;

namespace NexusShell.Models
{
    /// <summary>
    /// Represents an active neural chat session within a workspace.
    /// </summary>
    public class NeuralSession
    {
        /// <summary>
        /// Lock object for thread-safe access to History and Turns.
        /// </summary>
        public readonly object Lock = new();

        /// <summary>
        /// Gets or sets the name of the project associated with this session.
        /// </summary>
        public required string ProjectName { get; set; }

        /// <summary>
        /// Gets or sets the absolute path to the project.
        /// </summary>
        public required string ProjectPath { get; set; }

        /// <summary>
        /// Gets or sets any extra arguments (e.g. --include-directories).
        /// </summary>
        public string ExtraArgs { get; set; } = "";

        /// <summary>
        /// Gets or sets an optional system prompt override injected before conversation history.
        /// Used for non-project workspaces (Journal, Scaffolder, Marketing).
        /// </summary>
        public string SystemPrompt { get; set; } = "";

        /// <summary>
        /// Gets or sets the chronological chat history for this session.
        /// </summary>
        public List<string> History { get; set; } = new();

        /// <summary>
        /// Structured conversation turns used for context injection and persistence.
        /// </summary>
        public List<ConversationTurn> Turns { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of resumable sessions found via gemini --list-sessions.
        /// </summary>
        public List<string> ResumableSessions { get; set; } = new();

        /// <summary>
        /// Gets or sets the current step in a static wizard flow (0 = none/chat).
        /// </summary>
        public int WizardStep { get; set; } = 0;

        /// <summary>
        /// Temporary data storage for wizard steps.
        /// </summary>
        public Dictionary<string, string> WizardData { get; set; } = new();

        /// <summary>
        /// Stores the user's current input text to preserve it across tab switches.
        /// </summary>
        public System.Text.StringBuilder InputBuffer { get; set; } = new();

        private volatile bool _isProcessing = false;

        /// <summary>
        /// Gets or sets a value indicating whether the session is currently waiting for an AI response.
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set => _isProcessing = value;
        }
    }

    /// <summary>
    /// Represents a single turn in a Gemini conversation.
    /// </summary>
    public class ConversationTurn
    {
        /// <summary>
        /// Gets or sets the role of the speaker. Either "user" or "ai".
        /// </summary>
        public string Role { get; set; } = "user";

        /// <summary>
        /// Gets or sets the text content of this turn.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Gets or sets the time this turn was recorded.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

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
    /// Represents the deep neural intelligence and persistent context for an enterprise project.
    /// This is stored in each repository's .gemini/nexus_context.json.
    /// </summary>
    public class ProjectContext
    {
        /// <summary>
        /// Gets or sets the primary goal or mission for this project.
        /// </summary>
        public string Objective { get; set; } = "No objective defined.";

        /// <summary>
        /// Gets or sets the current architectural or strategic plan.
        /// </summary>
        public string Brainstorm { get; set; } = "No plan brainstormed yet.";

        /// <summary>
        /// Gets or sets a chronological list of significant actions taken.
        /// </summary>
        public List<string> Resume { get; set; } = new();

        /// <summary>
        /// Gets or sets the status of any active background subagents.
        /// </summary>
        public string AgentStatus { get; set; } = "Idle";

        /// <summary>
        /// Gets or sets the timestamp of the last update to this context.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the total token count of the project's codebase as of last scan.
        /// </summary>
        public int ContextTokens { get; set; } = 0;
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
        /// Gets or sets the project's persistent context and intelligence.
        /// </summary>
        public ProjectContext Context { get; set; } = new();

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

        /// <summary>
        /// Gets or sets the test status (e.g. "Pass", "Fail", "None").
        /// </summary>
        public string TestStatus { get; set; } = "None";

        /// <summary>
        /// Gets or sets the test coverage percentage.
        /// </summary>
        public string Coverage { get; set; } = "";
    }
}
