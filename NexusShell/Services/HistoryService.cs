using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Spectre.Console;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Implementation of the persistent history service using JSON on disk.
    /// Thread-safe for multi-process event logging with high-precision ordering.
    /// </summary>
    public class HistoryService(string conductorRoot) : IHistoryService
    {
        private readonly string _statsFile = Path.Combine(conductorRoot, "nexus_stats.json");
        private readonly string _historyFile = Path.Combine(conductorRoot, "nexus_history.json");
        private const int MAX_HISTORY_EVENTS = 50;
        private static readonly object _fileLock = new();

        /// <inheritdoc />
        public Dictionary<string, ProjectStats> LoadStats()
        {
            lock (_fileLock)
            {
                return LoadStatsInternal();
            }
        }

        /// <inheritdoc />
        public void RecordLaunch(string projectName)
        {
            lock (_fileLock)
            {
                var stats = LoadStatsInternal();
                if (!stats.TryGetValue(projectName, out var s))
                {
                    s = new ProjectStats();
                    stats[projectName] = s;
                }
                
                s.OpenCount++;
                s.LastOpened = DateTime.Now;
                
                SaveStatsInternal(stats);
            }
        }

        /// <inheritdoc />
        public List<HistoryEvent> GetRecentEvents()
        {
            lock (_fileLock)
            {
                return GetRecentEventsInternal();
            }
        }

        /// <inheritdoc />
        public void AddEvent(string message)
        {
            lock (_fileLock)
            {
                var events = GetRecentEventsInternal();
                events.Insert(0, new HistoryEvent(DateTime.Now, message));
                
                // Ensure correct ordering using full precision and take latest
                var trimmedEvents = events
                    .OrderByDescending(e => e.Timestamp)
                    .Take(MAX_HISTORY_EVENTS)
                    .ToList();
                
                try {
                    string json = JsonSerializer.Serialize(trimmedEvents, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_historyFile, json);
                } catch (Exception ex) {
                    AnsiConsole.MarkupLine($"[red]Error saving history:[/] {ex.Message}");
                }
            }
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            lock (_fileLock)
            {
                if (File.Exists(_statsFile)) File.Delete(_statsFile);
                if (File.Exists(_historyFile)) File.Delete(_historyFile);
            }
        }

        private Dictionary<string, ProjectStats> LoadStatsInternal()
        {
            if (!File.Exists(_statsFile)) return new Dictionary<string, ProjectStats>();
            try {
                string json = File.ReadAllText(_statsFile);
                return JsonSerializer.Deserialize<Dictionary<string, ProjectStats>>(json) ?? new Dictionary<string, ProjectStats>();
            } catch { return new Dictionary<string, ProjectStats>(); }
        }

        private List<HistoryEvent> GetRecentEventsInternal()
        {
            if (!File.Exists(_historyFile)) return new List<HistoryEvent>();
            try {
                string json = File.ReadAllText(_historyFile);
                var list = JsonSerializer.Deserialize<List<HistoryEvent>>(json) ?? new List<HistoryEvent>();
                // Always ensure high-precision sorting when loading
                return list.OrderByDescending(e => e.Timestamp).ToList();
            } catch { return new List<HistoryEvent>(); }
        }

        private void SaveStatsInternal(Dictionary<string, ProjectStats> stats)
        {
            try {
                string json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_statsFile, json);
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[red]Error saving stats:[/] {ex.Message}");
            }
        }
    }
}
