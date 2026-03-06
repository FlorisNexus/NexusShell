using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    public class CliExecutionService : ICliExecutionService
    {
        public async IAsyncEnumerable<string> StreamPromptAsync(string workingDirectory, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default, string extraArgs = "")
        {
            var psi = new ProcessStartInfo("gemini.cmd")
            {
                Arguments = $"-p \"-\" -o text {extraArgs}", // Force read from stdin
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            
            if (!process.Start())
            {
                yield return "[red]Failed to start Gemini CLI process.[/]";
                yield break;
            }

            // Write prompt to stdin asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    using var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false));
                    await writer.WriteAsync(prompt);
                    await writer.FlushAsync();
                }
                catch { /* Ignore pipe broken */ }
            });

            // Read output line by line
            using var reader = process.StandardOutput;
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                line = line.TrimEnd();
                if (string.IsNullOrEmpty(line) || 
                    line.StartsWith("Loading") || 
                    line.StartsWith("Server") || 
                    line.Contains("supports tool updates") ||
                    line.Contains("supports prompt updates"))
                {
                    continue; // Skip noise
                }

                yield return line;
            }

            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                yield return $"\n[red]Process exited with code {process.ExitCode}:[/] {error}";
            }
        }

        public async Task<string> ExecutePromptAsync(string workingDirectory, string prompt, string extraArgs = "")
        {
            var psi = new ProcessStartInfo("gemini.cmd")
            {
                Arguments = $"-p \"-\" -o text {extraArgs}",
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            
            if (!process.Start()) return "Failed to start Gemini CLI process.";

            using (var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(prompt);
                await writer.FlushAsync();
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var lines = output.Split('\n', StringSplitOptions.TrimEntries)
                .Where(l => !string.IsNullOrEmpty(l) && 
                            !l.StartsWith("Loading") && 
                            !l.StartsWith("Server") && 
                            !l.Contains("supports"));

            return string.Join("\n", lines);
        }

        public IAsyncEnumerable<string> StreamPromptAsync(string workingDirectory, string prompt, string extraArgs = "")
        {
            return StreamPromptAsync(workingDirectory, prompt, CancellationToken.None, extraArgs);
        }
    }
}
