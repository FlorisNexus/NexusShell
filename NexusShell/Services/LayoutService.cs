using System;
using Spectre.Console;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    /// <summary>
    /// Implementation of the layout service using Spectre.Console.
    /// </summary>
    public class LayoutService(NexusSettings settings) : ILayoutService
    {
        private readonly NexusSettings _settings = settings;

        /// <inheritdoc />
        public void RefreshHeader()
        {
            Console.Clear();
            DrawHeroHeader();
            DrawStrategicFocus();
        }

        /// <inheritdoc />
        public void DrawHeroHeader()
        {
            var figlet = new FigletText("FLORISNEXUS").Centered().Color(Color.Cyan1);
            AnsiConsole.Write(figlet);
            AnsiConsole.Write(new Rule($"[bold white]AI-AUGMENTED COMMAND CENTER {_settings.Version}[/]").RuleStyle("cyan dim"));
        }

        /// <inheritdoc />
        public void DrawStrategicFocus()
        {
            var grid = new Grid();
            grid.AddColumn();
            grid.AddRow("[bold green]🏗️ BRANCH 1: LOCAL DIGITAL PRESENCE (LOCAL HERO)[/] [grey]Showcase sites | Vite+React+Tailwind | Azure SPA (GitHub Actions)[/]");
            grid.AddRow("[bold green]🌍 BRANCH 2: INTERNATIONAL SAAS (BUILD IN PUBLIC)[/] [grey]Global SaaS | .NET 10+Blazor+Azure | Finova & Continuum[/]");

            AnsiConsole.Write(new Panel(grid)
                .Header("[bold magenta] STRATEGIC INTELLIGENCE [/]")
                .BorderColor(Color.Magenta)
                .Padding(1, 1));
        }
    }
}
