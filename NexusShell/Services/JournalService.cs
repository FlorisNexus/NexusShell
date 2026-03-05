using System;
using System.IO;
using Spectre.Console;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    /// <summary>
    /// Implementation of the Founder's Daily Journal feature.
    /// </summary>
    public class JournalService(string reposRoot, ILayoutService layoutService) : IJournalService
    {
        private readonly string _reposRoot = reposRoot;

        /// <inheritdoc />
        public void Execute()
        {
            var journalDir = Path.Combine(_reposRoot, "conductor", "journal");
            if (!Directory.Exists(journalDir))
            {
                Directory.CreateDirectory(journalDir);
            }

            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var journalFile = Path.Combine(journalDir, $"{date}.md");

            layoutService.RefreshHeader();
            AnsiConsole.Write(new Rule($"[yellow]--- 📔 FOUNDER'S DAILY JOURNAL ({date}) ---[/]").RuleStyle("yellow dim"));

            var mood = AnsiConsole.Prompt(
                new TextPrompt<int>("[cyan]How are you feeling about the progress today? (1-10)[/]")
                    .ValidationErrorMessage("[red]Please enter a number between 1 and 10[/]")
                    .Validate(n => n >= 1 && n <= 10 ? ValidationResult.Success() : ValidationResult.Error()));

            var thought = AnsiConsole.Ask<string>("[cyan]What's the #1 insight or challenge you faced today?[/]");
            var achievement = AnsiConsole.Ask<string>("[cyan]What's the #1 thing you are proud of today?[/]");

            string content = $@"# Journal entry: {date}
- Mood: {mood}/10
- Key Insight: {thought}
- Achievement: {achievement}

---
## 💡 Automated Post Idea (Ask Gemini):
""Based on my journal for {date} (Insight: {thought}, Achievement: {achievement}), generate a high-impact 'Build in Public' story for LinkedIn.""
";

            File.WriteAllText(journalFile, content);
            
            AnsiConsole.MarkupLine($"\n[green]✅ Journal entry saved to {journalFile}[/]");
            AnsiConsole.MarkupLine("[grey]This will serve as a 'History of the Business' for your future followers.[/]");
            
            AnsiConsole.MarkupLine("\n[bold grey]» PRESS ANY KEY TO RETURN...[/]");
            Console.ReadKey();
        }
    }
}
