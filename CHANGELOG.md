# Changelog

All notable changes to this project. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versioning follows [SemVer](https://semver.org/).

## [1.0.0] - 2026-11-20

First public release: a server written entirely in C#.

### Added
- stdio MCP server (`RevitMCP.Cli`) with 22 native tools.
- Revit add-in (`RevitMCP.Plugin`) with a WPF DockablePane: live agent action log, safe element selection, readiness status.
- Windows Named Pipe IPC with NDJSON: several Revit instances at once, routed by PID.
- Roslyn `execute_csharp` engine with direct access to `Doc`, `App`, `UIDoc`, `WithTransaction`, `WithTransactionGroup`; isolated `AssemblyLoadContext` (.NET 8) and a SHA-256 delegate cache (net48).
- BIM-Map on SQLite WAL + FTS5 with LRU auto-cleanup.
- SP 63 structural primitives: slabs with openings, beams, columns, `area_reinforcement`, `rebar`.
- `check_intersections` — 3D solid clashes with intersection volume in m³.
- `inspect_element_geometry`, `visual_override`, `export_view_image`, `manage_selection`.
- Inno Setup installer: deploys binaries to `%LocalAppData%\RevitMCP\` and writes only an `.addin` manifest into the Revit folder.
- CI (builds every configuration) and a release workflow (installer plus per-version archives).

### Changed
- Revit 2019–2027 support with pinned API package versions: `R2019`–`R2024` → net48, `R2025`–`R2026` → net8.0-windows, `R2027` → net10.0-windows.

### Removed
- The obsolete `tests/commandset` project.
