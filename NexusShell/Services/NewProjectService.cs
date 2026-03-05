using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Spectre.Console;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    /// <summary>
    /// Implementation of the multi-project scaffolding service.
    /// </summary>
    public class NewProjectService(NexusSettings settings, ILayoutService layoutService) : INewProjectService
    {
        private readonly NexusSettings _settings = settings;
        private readonly ILayoutService _layoutService = layoutService;

        /// <inheritdoc />
        public void Execute()
        {
            _layoutService.RefreshHeader();
            AnsiConsole.Write(new Rule("[cyan]--- 🛠️  NEW PROJECT WORKFLOW ---[/]").RuleStyle("cyan dim"));

            var typeChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select project type:")
                    .AddChoices(new[] {
                        "1. Local Digital Presence (Vite/Tailwind/React)",
                        "2. International SaaS (.NET 10/Blazor/Azure)",
                        "Back"
                    }));

            if (typeChoice == "Back") return;

            var name = AnsiConsole.Ask<string>("[cyan]Enter the name of the new project:[/]");
            var projectPath = Path.Combine(_settings.ReposRoot, name);

            if (Directory.Exists(projectPath))
            {
                AnsiConsole.MarkupLine($"[red]❌ Project {name} already exists at {projectPath}![/]");
                AnsiConsole.MarkupLine("\n[bold grey]» PRESS ANY KEY TO RETURN...[/]");
                Console.ReadKey();
                return;
            }

            bool enableAppConfig = false;
            string customDomain = "";

            if (typeChoice.StartsWith("2"))
            {
                enableAppConfig = AnsiConsole.Confirm("Include Azure App Configuration?", false);
                customDomain = AnsiConsole.Ask<string>("Enter base domain name (e.g., example.com) or leave blank:", "");
            }

            Directory.CreateDirectory(projectPath);
            
            string projectType = "";
            string projectStack = "";

            if (typeChoice.StartsWith("1"))
            {
                ScaffoldLocalSite(projectPath, name);
                projectType = "Local Digital Presence";
                projectStack = "Vite + React + TS + Tailwind";
            }
            else
            {
                ScaffoldSaaS(projectPath, name, enableAppConfig, customDomain);
                projectType = "International SaaS";
                projectStack = ".NET 10 + Blazor WASM + Azure (Enterprise-Ready)";
            }

            CreateCommonFiles(projectPath, name, projectType, projectStack);
            InitializeGit(projectPath, name, projectType);

            AnsiConsole.MarkupLine($"\n[green]✅ Project {name} created with Enterprise standards![/]");
            AnsiConsole.MarkupLine($"[grey]Next Steps: cd {name}; gemini[/]");
            AnsiConsole.MarkupLine("\n[bold grey]» PRESS ANY KEY TO RETURN...[/]");
            Console.ReadKey();
        }

        private void ScaffoldLocalSite(string path, string name)
        {
            AnsiConsole.MarkupLine($"[green]🚀 Scaffolding Local Site: {name}...[/]");
            RunCommand("npm", $"create vite@latest . -- --template react-ts", path);
            RunCommand("npm", "install tailwindcss postcss autoprefixer", path);
            RunCommand("npx", "tailwindcss init -p", path);
        }

        private void ScaffoldSaaS(string path, string name, bool enableAppConfig, string customDomain)
        {
            AnsiConsole.MarkupLine($"[green]🌍 Scaffolding International SaaS: {name}...[/]");
            
            // 1. Dotnet scaffold
            RunCommand("dotnet", "new blazorwasm -o . --no-restore", path);
            Directory.CreateDirectory(Path.Combine(path, "src", "Infrastructure"));
            Directory.CreateDirectory(Path.Combine(path, "src", "Domain"));
            Directory.CreateDirectory(Path.Combine(path, "src", "Web"));
            Directory.CreateDirectory(Path.Combine(path, "src", "Functions"));

            // 2. Cloud Infrastructure
            AnsiConsole.MarkupLine("[cyan]☁️  Adding Cloud Infrastructure (KeyVault, VNet, Managed Identity)...[/]");
            var templatesDir = Path.Combine(_settings.ConductorRoot, "templates", "saas");
            var infraDir = Path.Combine(path, "infra");
            Directory.CreateDirectory(infraDir);

            if (Directory.Exists(templatesDir))
            {
                File.Copy(Path.Combine(templatesDir, "main.bicep"), Path.Combine(infraDir, "main.bicep"), true);

                foreach (var env in new[] { "staging", "prod" })
                {
                    var sourceParam = Path.Combine(templatesDir, $"{env}.bicepparam");
                    if (File.Exists(sourceParam))
                    {
                        var content = File.ReadAllText(sourceParam);
                        content = content.Replace("param projectName = 'saas-project'", $"param projectName = '{name}'");
                        content = content.Replace("param enableAppConfig = false", $"param enableAppConfig = {enableAppConfig.ToString().ToLower()}");
                        content = content.Replace("param enableAppConfig = true", $"param enableAppConfig = {enableAppConfig.ToString().ToLower()}");
                        
                        if (!string.IsNullOrEmpty(customDomain))
                        {
                            var domainPrefix = env == "staging" ? "staging." : "";
                            content = content.Replace("param customDomain = ''", $"param customDomain = '{domainPrefix}{customDomain}'");
                        }
                        
                        File.WriteAllText(Path.Combine(infraDir, $"{env}.bicepparam"), content);
                    }
                }
            }

            // 3. GitHub Actions
            AnsiConsole.MarkupLine("[cyan]🤖 Adding Deployment Workflows...[/]");
            var workflowDir = Path.Combine(path, ".github", "workflows");
            Directory.CreateDirectory(workflowDir);
            if (File.Exists(Path.Combine(templatesDir, "deploy.yml")))
            {
                File.Copy(Path.Combine(templatesDir, "deploy.yml"), Path.Combine(workflowDir, "deploy.yml"), true);
            }
        }

        private void CreateCommonFiles(string path, string name, string type, string stack)
        {
            var indexContent = $@"# Project: {name}
- Type: {type}
- Stack: {stack}
- Status: Initial Scaffolding

## 🎯 Current Goals
- [ ] Initialize project architecture
- [ ] Set up Azure Secrets in KeyVault
- [ ] Configure VNet integration
";
            File.WriteAllText(Path.Combine(path, "index.md"), indexContent);

            var geminiMd = $@"# 🏢 Project: {name} ({type})

## 📋 Context & Rules
- Follow global rules in `../GEMINI.md`.
- Standards: {stack}.
- Security: Managed Identity (UserAssigned), KeyVault for secrets.
- Networking: VNet integration (delegated subnet for apps).
- Observability: App Insights + Log Analytics.
";
            File.WriteAllText(Path.Combine(path, "GEMINI.md"), geminiMd);
        }

        private void InitializeGit(string path, string name, string type)
        {
            RunCommand("git", "init", path);
            RunCommand("git", "add .", path);
            RunCommand("git", "commit -m \"Initial scaffold: " + name + " (" + type + ") with Enterprise Cloud Standards\"", path);
            
            // Sync tracks
            var syncScript = Path.Combine(_settings.ConductorRoot, "sync-tracks.ps1");
            if (File.Exists(syncScript))
            {
                RunCommand("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{syncScript}\"", _settings.ConductorRoot);
            }
        }

        private void RunCommand(string fileName, string args, string workingDir)
        {
            var psi = new ProcessStartInfo(fileName, args)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
        }
    }
}
