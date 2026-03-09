using System;
using System.IO;
using NexusShell.Interfaces;

namespace NexusShell.Services
{
    /// <summary>
    /// Scaffolds conductor plan + Gemini prompt pairs and registers them in NEXT.md.
    /// </summary>
    public class PlanService(NexusSettings settings, IHistoryService historyService) : IPlanService
    {
        private readonly NexusSettings _settings = settings;
        private readonly IHistoryService _historyService = historyService;

        /// <inheritdoc />
        public (string planPath, string promptPath) CreatePlanPair(string project, string slug)
        {
            string yyyyMm = DateTime.Now.ToString("yyyy-MM");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            string projectDir = Path.Combine(_settings.ConductorRoot, "plans", project);
            Directory.CreateDirectory(projectDir);

            string planFile = $"{project}-plan-{slug}-{yyyyMm}.md";
            string promptFile = $"{project}-gemini-prompt-{slug}-{yyyyMm}.md";
            string planPath = Path.Combine(projectDir, planFile);
            string promptPath = Path.Combine(projectDir, promptFile);

            // Write plan file
            File.WriteAllText(planPath, BuildPlanTemplate(project, slug, timestamp));

            // Write Gemini prompt file
            File.WriteAllText(promptPath, BuildPromptTemplate(project, slug, planFile, timestamp));

            // Append to NEXT.md
            string nextMd = Path.Combine(_settings.ConductorRoot, "NEXT.md");
            string relativePath = $"conductor/plans/{project}/{promptFile}";
            string entry = $"\n- [ ] Run: `{relativePath}`\n";
            File.AppendAllText(nextMd, entry);

            _historyService.AddEvent($"Plan pair created: {planFile}");
            return (planPath, promptPath);
        }

        private static string BuildPlanTemplate(string project, string slug, string timestamp) => $"""
            # Plan: {project} — {slug}

            **Project:** {project}
            **Date:** {timestamp}
            **Status:** Draft

            ## Goal

            _One sentence: what will be true when this plan is complete._

            ## Context

            _Why this work is needed. Relevant constraints or decisions already made._

            ## Scope

            **In scope:**
            -

            **Out of scope:**
            -

            ## Implementation Steps

            ### Step 1: [Name]

            **What:**
            **Files affected:**
            **Verification:**

            ## Risks & Mitigations

            | Risk | Likelihood | Mitigation |
            |------|-----------|------------|
            |      |           |            |

            ## Definition of Done

            - [ ] All steps complete
            - [ ] Build passes (`dotnet build`)
            - [ ] Tests pass (`dotnet test`)
            - [ ] `CHANGELOG.md` updated under `[Unreleased]`

            """;

        private static string BuildPromptTemplate(string project, string slug, string planFile, string timestamp) => $"""
            # {project}: {slug}

            **Created:** {timestamp}
            **Project:** {project}
            **Plan:** conductor/plans/{project}/{planFile}

            ## Context

            _Fill in context. Link to relevant files._

            ## Your Task

            _Describe what Gemini must implement._

            ## Files to Modify

            _List exact file paths._

            ## Checkpoints

            ### CHECKPOINT 1 — [Name]

            _Steps._

            **Verify:** `dotnet build && dotnet test`

            ## Rules

            - Follow the companion plan at `conductor/plans/{project}/{planFile}`
            - All code must match workspace conventions in root `CLAUDE.md` and `GEMINI.md`
            - XML docs on all new `public`/`internal` members
            - When done: rename this file to `[DONE] {project}-gemini-prompt-{slug}-YYYY-MM.md`, rename its associated plan to `[DONE] {planFile}`, mark the item in `conductor/NEXT.md` as `[x]`, and move all `[DONE] *.md` files in this project's plans folder to its `_done/` subfolder.

            """;
    }
}
