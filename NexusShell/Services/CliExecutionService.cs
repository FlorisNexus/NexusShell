using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    public class CliExecutionService : ICliExecutionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _kernelUrl = "http://localhost:5005";
        private bool _isKernelActive = true;

        public CliExecutionService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        private async Task<bool> CheckKernelAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_kernelUrl}/health");
                _isKernelActive = response.IsSuccessStatusCode;
                return _isKernelActive;
            }
            catch
            {
                _isKernelActive = false;
                return false;
            }
        }

        public async IAsyncEnumerable<string> StreamPromptAsync(string workingDirectory, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default, string extraArgs = "")
        {
            // 1. Try to use the high-speed Nexus Kernel
            if (await CheckKernelAsync())
            {
                var requestBody = new {
                    prompt = prompt,
                    systemInstruction = "You are the Nexus Neural OS. Be concise and technical.",
                    history = new object[] {} // Passed via UserInterface in prompt string for now
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_kernelUrl}/prompt")
                {
                    Content = JsonContent.Create(requestBody)
                };

                HttpResponseMessage? response = null;
                bool success = false;
                try
                {
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        success = true;
                    }
                }
                catch
                {
                    // Fallback to CLI if network fails
                }

                if (success && response != null)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    char[] buffer = new char[128];
                    int bytesRead;
                    while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        yield return new string(buffer, 0, bytesRead);
                    }
                    yield break; // Success! We don't need to fallback to the CLI.
                }
            }

            // 2. Fallback to local Gemini CLI
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
            
            if (!process.Start())
            {
                yield return "[red]Failed to start Gemini CLI process.[/]";
                yield break;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false));
                    await writer.WriteAsync(prompt);
                    await writer.FlushAsync();
                }
                catch { }
            });

            using var processReader = process.StandardOutput;
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await processReader.ReadLineAsync();
                if (line == null) break;

                line = line.TrimEnd();
                if (string.IsNullOrEmpty(line) || 
                    line.StartsWith("Loading") || 
                    line.StartsWith("Server") || 
                    line.Contains("supports tool updates") ||
                    line.Contains("supports prompt updates"))
                {
                    continue; 
                }

                // Append newline since ReadLine strips it
                yield return line + "\n";
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
