using System;
using System.Diagnostics;
using Spectre.Console;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    /// <summary>
    /// Implementation of the Marketing Command Center feature.
    /// </summary>
    public class MarketingService(
        string reposRoot, 
        ILayoutService layoutService,
        ISessionOrchestrator sessionOrchestrator) : IMarketingService
    {
        private readonly string _reposRoot = reposRoot;
        private readonly ILayoutService _layoutService = layoutService;
        private readonly ISessionOrchestrator _sessionOrchestrator = sessionOrchestrator;

        /// <inheritdoc />
        public void Execute()
        {
            _layoutService.RefreshHeader();
            AnsiConsole.Write(new Rule("[yellow]--- 📢 MARKETING COMMAND CENTER ---[/]").RuleStyle("yellow dim"));

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select an option:")
                    .AddChoices(new[] {
                        "1. Generate Daily Post (50-Day Roadmap)",
                        "2. Platform Strategy (Where & How to sell)",
                        "3. Ask Gemini for custom Marketing Advice",
                        "Back"
                    }));

            if (choice.StartsWith("1"))
            {
                var branchChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Select Branch:[/]")
                        .AddChoices(new[] {
                            "1. FlorisNexus (Local - French)",
                            "2. International SaaS (Global - English)"
                        }));

                var branchName = branchChoice.StartsWith("1") ? "FlorisNexus" : "International SaaS";
                var day = AnsiConsole.Ask<int>("[cyan]Which Day (1-50) are we on?[/]");

                var prompt = $"Generate the marketing post for Day {day} for {branchName} using the roadmap in product-guidelines.md and marketing-guidelines.md. Provide the text for the relevant platforms (FB/IG/LI or X/LI).";

                AnsiConsole.MarkupLine($"\n[green]🚀 Launching Gemini for Day {day} ({branchName})...[/]");
                _sessionOrchestrator.LaunchGemini("MARKETING", _reposRoot, $"--prompt \"{prompt}\" -i");
            }
            else if (choice.StartsWith("2"))
            {
                AnsiConsole.Write(new Rule("[cyan]--- 🌍 PLATFORM STRATEGY ---[/]").RuleStyle("cyan dim"));
                
                AnsiConsole.MarkupLine("\n[green]📍 LOCAL (Branch 1):[/]");
                AnsiConsole.MarkupLine("  - [bold]Facebook Groups:[/] Join local business communities in Hainaut/Belgium.");
                AnsiConsole.MarkupLine("  - [bold]Instagram:[/] Post 'Before/After' site refactors and 'Meet the Founder' stories.");
                AnsiConsole.MarkupLine("  - [bold]Google Business:[/] Post weekly updates to stay at the top of local SEO.");
                
                AnsiConsole.MarkupLine("\n[green]📍 SAAS (Branch 2):[/]");
                AnsiConsole.MarkupLine("  - [bold]X (Twitter):[/] Use #BuildInPublic. Share code snippets and 'Ghost Bug' stories.");
                AnsiConsole.MarkupLine("  - [bold]LinkedIn:[/] Write long-form articles about .NET 10 / Azure architecture.");
                AnsiConsole.MarkupLine("  - [bold]Product Hunt:[/] Prepare for a 'Big Launch' once the MVP is stable.");
                AnsiConsole.MarkupLine("  - [bold]Indie Hackers:[/] Share revenue/growth milestones (Transparency).");

                AnsiConsole.MarkupLine("\n[bold grey]» PRESS ANY KEY TO RETURN...[/]");
                Console.ReadKey();
            }
            else if (choice.StartsWith("3"))
            {
                var topic = AnsiConsole.Ask<string>("[cyan]What do you want to sell or discuss today?[/]");
                var prompt = $"As a senior marketing strategist, help me sell '{topic}'. Tell me what to say, which tone to use, and which platforms are best for this specific target.";
                _sessionOrchestrator.LaunchGemini("MARKETING", _reposRoot, $"--prompt \"{prompt}\" -i");
            }
        }
    }
}
