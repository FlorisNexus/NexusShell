using System;
using System.Collections.Generic;
using Spectre.Console;
using Spectre.Console.Rendering;
using NexusShell.Interfaces;
using NexusShell.Models;

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
            var layout = new Grid().AddColumn();
            layout.AddRow(GetHeroHeader());
            layout.AddRow(GetStrategicFocus());
            AnsiConsole.Write(layout);
        }

        /// <inheritdoc />
        public IRenderable GetHeroHeader()
        {
            var grid = new Grid().AddColumn();
            var figlet = new FigletText("FLORISNEXUS").Centered().Color(Color.Cyan1);
            grid.AddRow(figlet);
            grid.AddRow(new Rule($"[bold white]AI-AUGMENTED COMMAND CENTER {_settings.Version}[/]").RuleStyle("cyan dim"));
            return grid;
        }

        /// <inheritdoc />
        public IRenderable GetTabBar(List<string> tabs, int activeIndex)
        {
            var grid = new Grid();
            grid.AddColumn(new GridColumn().Centered());

            var tabRow = new Grid();
            for (int i = 0; i < tabs.Count; i++) tabRow.AddColumn(new GridColumn().NoWrap());

            var renderables = new List<IRenderable>();
            for (int i = 0; i < tabs.Count; i++)
            {
                string text = Markup.Escape(tabs[i]);
                bool isActive = (i == activeIndex);

                string color = isActive ? "cyan1" : "grey";
                string decor = isActive ? "[bold cyan1]▼[/]" : " ";
                string bracketOpen = isActive ? "[bold white][[[/]" : "[grey][[[/]";
                string bracketClose = isActive ? "[bold white]]][/]" : "[grey]]][/]";

                var container = new Grid();
                container.AddColumn();
                container.AddRow(new Markup($"  {decor}  "));
                container.AddRow(new Markup($"{bracketOpen}[{color}]{text}[/]{bracketClose}"));

                renderables.Add(container);
            }

            tabRow.AddRow(renderables.ToArray());

            return new Panel(tabRow)
                .BorderColor(Color.Grey23)
                .Header("[bold grey] VIRTUAL WORKSPACES [/]")
                .Expand();
        }


        /// <inheritdoc />
        public IRenderable GetStrategicFocus()
        {
            var grid = new Grid();
            grid.AddColumn();
            grid.AddRow("[bold green]🏗️ BRANCH 1: LOCAL DIGITAL PRESENCE (LOCAL HERO)[/] [grey]Showcase sites | Vite+React+Tailwind | Azure SPA (GitHub Actions)[/]");
            grid.AddRow("[bold green]🌍 BRANCH 2: INTERNATIONAL SAAS (BUILD IN PUBLIC)[/] [grey]Global SaaS | .NET 10+Blazor+Azure | Finova & Continuum[/]");

            return new Panel(grid)
                .Header("[bold magenta] STRATEGIC INTELLIGENCE [/]")
                .BorderColor(Color.Magenta)
                .Padding(1, 1);
        }

        /// <inheritdoc />
        public IRenderable GetProjectBriefing(ProjectInfo p)
        {
            var grid = new Grid();
            grid.AddColumn(new GridColumn().NoWrap());
            grid.AddColumn(new GridColumn());

            grid.AddRow("[cyan]Objective:[/]", $"[white]{p.Context.Objective}[/]");
            grid.AddRow("[cyan]Brainstorm:[/]", $"[grey]{p.Context.Brainstorm}[/]");
            grid.AddRow("[cyan]Status:[/]", $"[bold yellow]{p.Context.AgentStatus}[/]");
            grid.AddRow("[cyan]Neural Mesh:[/]", $"[blue]{p.Context.ContextTokens:N0} tokens[/] [grey](Last update: {p.Context.LastUpdated:MMM dd HH:mm})[/]");

            var resumeTable = new Table().Border(TableBorder.None).HideHeaders();
            resumeTable.AddColumn("Entry");
            foreach (var r in p.Context.Resume.Take(5))
            {
                resumeTable.AddRow($"[grey]• {r}[/]");
            }

            var layout = new Grid();
            layout.AddColumn();
            layout.AddRow(new Panel(grid).Header($"[bold cyan] {p.Name.ToUpper()} INTELLIGENCE [/]").BorderColor(Color.Cyan1));
            layout.AddRow(new Panel(resumeTable).Header("[bold blue] LIVE RESUME [/]").BorderColor(Color.Blue1));

            return layout;
        }
    }
}
