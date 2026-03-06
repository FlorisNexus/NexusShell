using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Persists per-project Gemini conversation history to .gemini/chat_history.json.
    /// </summary>
    public class ChatPersistenceService : IChatPersistenceService
    {
        private const string HISTORY_FILE = ".gemini/chat_history.json";
        private const int MAX_STORED_TURNS = 50;
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        /// <inheritdoc />
        public List<ConversationTurn> LoadHistory(string projectPath)
        {
            string path = Path.Combine(projectPath, HISTORY_FILE);
            if (!File.Exists(path)) return new List<ConversationTurn>();
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ConversationTurn>>(json) ?? new();
            }
            catch { return new List<ConversationTurn>(); }
        }

        /// <inheritdoc />
        public void SaveHistory(string projectPath, List<ConversationTurn> turns)
        {
            string fullPath = Path.Combine(projectPath, HISTORY_FILE);
            string? dir = Path.GetDirectoryName(fullPath);
            try
            {
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var toSave = turns.TakeLast(MAX_STORED_TURNS).ToList();
                File.WriteAllText(fullPath, JsonSerializer.Serialize(toSave, _json));
            }
            catch { /* Silently fail — history is non-critical */ }
        }
    }
}
