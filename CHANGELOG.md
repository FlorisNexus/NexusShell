# Changelog — NexusShell

All notable changes to NexusShell are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) | Versioning: [SemVer](https://semver.org/)
Maintained by: Gemini CLI and Claude (both agents update this file).

Note: NexusShell version must stay in sync with `APP_VERSION` in `Program.cs` and the PowerShell profile.

---

## [v19.0.0] - 2026-03-09

### Added
- `P` keyboard shortcut on Hub dashboard opens a plan wizard that scaffolds a plan + Gemini prompt pair in `conductor/plans/[project]/` and appends a pending entry to `conductor/NEXT.md`.
- New `IPlanService` interface and `PlanService` implementation.

## [Unreleased]

### Added
- Project metadata: `GEMINI.md`, `CLAUDE.md`, `CHANGELOG.md`.

### Changed
### Fixed
### Removed

---

_History prior to changelog adoption not tracked._
