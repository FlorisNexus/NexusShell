using System;
using System.IO;
using System.Text.Json;
using NexusShell.Interfaces;
using NexusShell.Models;

namespace NexusShell.Services
{
    /// <summary>
    /// Implementation of the context service that manages project-specific intelligence files.
    /// Stores data in the .gemini/nexus_context.json file within each repository.
    /// </summary>
    public class ContextService : IContextService
    {
        private const string CONTEXT_FILE = ".gemini/nexus_context.json";
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <inheritdoc />
        public ProjectContext LoadContext(string projectPath)
        {
            string fullPath = Path.Combine(projectPath, CONTEXT_FILE);
            if (!File.Exists(fullPath))
            {
                return new ProjectContext();
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                return JsonSerializer.Deserialize<ProjectContext>(json) ?? new ProjectContext();
            }
            catch
            {
                return new ProjectContext();
            }
        }

        /// <inheritdoc />
        public void SaveContext(string projectPath, ProjectContext context)
        {
            string fullPath = Path.Combine(projectPath, CONTEXT_FILE);
            string? dir = Path.GetDirectoryName(fullPath);

            try
            {
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                context.LastUpdated = DateTime.Now;
                string json = JsonSerializer.Serialize(context, _jsonOptions);
                File.WriteAllText(fullPath, json);
            }
            catch (Exception ex)
            {
                // Silently fail in background or log to console if needed
                Console.WriteLine($"[Error] Could not save context to {fullPath}: {ex.Message}");
            }
        }
    }
}
