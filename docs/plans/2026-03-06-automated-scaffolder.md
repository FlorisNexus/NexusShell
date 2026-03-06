# Automated Scaffolder Implementation Plan (v16.3)

## Objective
Evolve the "NEW PROJECT" virtual workspace into an active **Execution Wizard**. Instead of just talking to the AI about templates, the Hub will ask specific questions, collect the parameters, physically scaffold the project on disk, initialize the repository, and then hand over the context to the AI for the final `GEMINI.md` generation.

## Wizard Flow (`StartScaffolderWizard`)
1. **Trigger:** User selects `🏗️ NEW PROJECT` from the Hub.
2. **Step 1 (Name):** "What is the name of the new project?"
3. **Step 2 (Track/Branch):** "Which branch does this belong to? (1) LOCAL HERO (React/Vite) or (2) GLOBAL SAAS (.NET 10)"
4. **Step 3 (Description):** "Provide a one-sentence description of the project's goal."
5. **Execution (`FinalizeScaffolder`):**
   - Create project folder in `_settings.ReposRoot`.
   - If LOCAL (1): Run `npm create vite@latest <name> -- --template react-ts` (or equivalent non-interactive).
   - If SAAS (2): Run `dotnet new webapi -n <name>` and copy Bicep templates from `conductor/templates/saas` if they exist.
   - Run `git init`.
   - Write an initial `index.md` for the conductor registry.
   - Write the provided description into an initial `GEMINI.md`.
6. **AI Handoff:** Trigger the Gemini CLI to review the newly scaffolded folder and suggest the first development steps.

## Implementation Steps
1. Modify `UserInterface.cs` `ExecuteSelection()` to call `StartScaffolderWizard()` instead of generic `InitializeWorkspace`.
2. Add `StartScaffolderWizard()` to initialize the session and set `WizardStep = 1`.
3. Update `ProcessWizardStep` to handle `session.ProjectName == "SCAFFOLDER"`.
4. Implement `FinalizeScaffolder(NeuralSession s)` to run the physical scaffolding commands using `System.Diagnostics.Process` or by delegating to a shell script/commands.
5. Provide UI feedback during the physical execution so the user knows it's building.
6. Trigger the AI summary at the end.
