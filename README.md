# Revit MCP

An MCP server for Autodesk Revit, written entirely in C#. It lets an AI agent (Claude Desktop, Claude Code, Cursor, Antigravity, VS Code and other MCP clients) read the model, create elements, check clashes and run arbitrary C# code inside a running Revit session.

Supported Revit versions: **2019–2027**.

[![CI](https://github.com/Delures/revit-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/Delures/revit-mcp/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Delures/revit-mcp)](https://github.com/Delures/revit-mcp/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## Installation

1. Download `RevitMCP-Setup-<version>.exe` from [Releases](/releases/latest).
2. Run the installer. It deploys the binaries to `%LocalAppData%\RevitMCP\` and creates a `RevitMCP.addin` manifest for every supported Revit version (2019–2027).
3. Start Revit — a **Revit MCP** tab with the agent pane button appears on the ribbon.
4. Point your MCP client at `%LocalAppData%\RevitMCP\bin\cli\RevitMCP.Cli.exe` (see below).

Nothing is written into the Revit installation folder: only a manifest file (`.addin`, under 1 KB) lands there. All binaries and data stay isolated in `%LocalAppData%\RevitMCP\`.

---

## Connecting an AI client

The server speaks MCP over stdio, so the client configuration needs an absolute path to `RevitMCP.Cli.exe`:

```json
{
  "mcpServers": {
    "revit": {
      "command": "%LOCALAPPDATA%\\RevitMCP\\bin\\cli\\RevitMCP.Cli.exe",
      "args": []
    }
  }
}
```

Most clients expand `%LOCALAPPDATA%` in the `command` field. If yours does not, replace it with the expanded path, e.g. `C:\Users\<you>\AppData\Local`.

| Client | Configuration file |
| :--- | :--- |
| Claude Desktop | `%APPDATA%\Claude\claude_desktop_config.json` |
| Claude Code | `%USERPROFILE%\.claude.json` or `claude mcp add revit -- <path to exe>` |
| Cursor | `%USERPROFILE%\.cursor\mcp.json` |
| Antigravity | `%USERPROFILE%\.gemini\config\mcp_config.json` |
| VS Code | `%APPDATA%\Code\User\mcp.json` (key `servers`) |
| Cline / Roo Code | `%APPDATA%\Code\User\globalStorage\<extension>\settings\cline_mcp_settings.json` |

Restart the client after editing the configuration: the tool list is cached at startup. Revit must be running — without it the server still answers, but model tools report "no active session".

---

## What you get

| Aspect | Revit MCP (C#) |
| :--- | :--- |
| **Stack** | **100% C#**: .NET 8 CLI + multi-target plugin (net48 / net8.0-windows / net10.0-windows) |
| **IPC transport** | **Windows Named Pipes** (`\\.\pipe\RevitMCP-{Year}-{PID}`) + NDJSON, no size limit |
| **Several Revit instances** | **Yes**, routed by PID |
| **Free-form code** | **Full `IRevitScriptContext`**: `Doc`, `App`, `UIDoc`, opening/closing documents, `WithTransaction`, `WithTransactionGroup` |
| **Roslyn** | **Isolated `AssemblyLoadContext`** (.NET 8), SHA-256 delegate cache (net48), no leak between calls |
| **UI** | **WPF DockablePane** with a live action log, safe element selection and readiness status |
| **BIM-Map** | **SQLite WAL + FTS5** with LRU auto-cleanup |
| **Structural design** | **SP 63 primitives**: slabs with openings, beams, columns, rebar meshes and bars in millimetres |
| **Clashes** | **`check_intersections`**: 3D solid intersections, volume in m³, extents |

---

## MCP tools (22)

| Tool | Purpose |
| :--- | :--- |
| `ping` | Connectivity and Named Pipe latency check |
| `list_revit_instances` | Discover running Revit processes |
| `select_revit_instance` | Switch the active process |
| `execute_csharp` | Compile and run C# through Roslyn with Revit API access |
| `inspect_schema` | Categories, families, types, document parameters |
| `query_elements` | Select elements by category and AABB (mm) |
| `record_lesson` | Store a rule or lesson in the knowledge base |
| `query_knowledge` | Search the knowledge base |
| `manage_parameters` | Batch read/write parameters with unit conversion |
| `export_schedule` | View schedules, rebar schedules and room lists as JSON |
| `manage_documents` | list / open / save / save_as / close / sync_with_central |
| `get_bim_map` | Topological model snapshot from the SQLite cache |
| `search_bim_map` | Full-text FTS5 search over elements and parameters |
| `rebuild_bim_map` | Background re-index of the model |
| `manage_cache` | SQLite disk budget: status, cleanup |
| `export_view_image` | Export a view to Base64 PNG/JPEG for vision models |
| `transform_elements` | move / rotate / copy / delete with offsets in mm and degrees |
| `create_elements` | Walls, levels, grids, families, rooms, lines; slabs with openings, beams, columns, `area_reinforcement`, `rebar` |
| `check_intersections` | 3D solid clashes via `ElementIntersectsElementFilter` with AABB fallback, intersection volume |
| `manage_selection` | Read and set the Revit UI selection |
| `inspect_element_geometry` | Analytical geometry in mm: contours, openings, thicknesses, centroid |
| `visual_override` | Transparency, colours, isolate/hide, 3D Solid mode for rebar |

Resources: `revit://sessions`, `revit://knowledge/rules`, `revit://model/bim_map`.

---

## Building from source

Requirements:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — required
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — only for the `R2027` configuration
- Inno Setup 6 — only for building the installer

```powershell
# tests
dotnet test tests/RevitMCP.Tests/RevitMCP.Tests.csproj -c R2025

# build and deploy the add-in for a specific Revit version
.\deploy-addin.ps1 -RevitYear 2025

# full distribution (all versions + installer)
.\build-installer.ps1 -AppVersion 1.0.0
```

### Supported Revit versions

| Revit | Target framework | Configuration | Revit API package |
| :--- | :--- | :--- | :--- |
| 2019 | net48 | `R2019` | 2019.0.1 |
| 2020 | net48 | `R2020` | 2020.0.1 |
| 2021 | net48 | `R2021` | 2021.1.9 |
| 2022 | net48 | `R2022` | 2022.1.0 |
| 2023 | net48 | `R2023` | 2023.0.0 |
| 2024 | net48 | `R2024` | 2024.2.0 |
| 2025 | net8.0-windows | `R2025` | 2025.0.0 |
| 2026 | net8.0-windows | `R2026` | 2026.0.0 |
| 2027 | net10.0-windows | `R2027` | 2027.0.0 |

The API package version is pinned in `Directory.Build.props` (`RevitApiVersion`): a floating `2026.*` range pulls 2026.5.0, whose assemblies reference `System.Runtime 10.0.0.0` and break the .NET 8 build. Version-specific API differences live behind `#if REVIT_2019` (schema inspection, pane helpers) and `#if REVIT_2026 || REVIT_2027` (rebar terminations) sections.

---

## Project layout

```
├── Directory.Build.props      # R2019..R2027, TFM and API package pins
├── RevitMCP.sln
├── deploy-addin.ps1           # build + deploy the add-in
├── build-installer.ps1        # distribution + Inno Setup installer
├── installer/RevitMCP.iss
├── docs/
└── src/
    ├── RevitMCP.Shared/       # JSON-RPC, NDJSON IPC, DTOs, Roslyn engine, SQLite
    ├── RevitMCP.Plugin/       # Revit add-in: pane, Named Pipe server, model operations
    └── RevitMCP.Cli/          # MCP stdio server
```

## Storage layout

| Path | Contents |
| :--- | :--- |
| `%APPDATA%\Autodesk\Revit\Addins\<year>\RevitMCP.addin` | the only file inside the Revit folder |
| `%LocalAppData%\RevitMCP\bin\<year>\` | plugin assemblies |
| `%LocalAppData%\RevitMCP\bin\cli\` | `RevitMCP.Cli.exe` |
| `%LocalAppData%\RevitMCP\models\` | SQLite BIM-Map (WAL) |
| `%LocalAppData%\RevitMCP\knowledge\` | agent knowledge base (Markdown) |
| `%LocalAppData%\RevitMCP\sessions\` | files of active Revit sessions |

---

## Documentation

- [Living specification](docs/specs/LIVING_SPECIFICATION.md)
- [Testing protocol](docs/specs/TESTING_PROTOCOL.md)
- [Tool audit and roster](docs/specs/TOOLS_AUDIT_AND_ROSTER.md)
- [Structural primitives](docs/specs/STRUCTURAL_PRIMITIVES.md)
- [MCP vs generative C#](docs/architecture/MCP_VS_CSHARP_ENGINE.md)
- [Custom install directory and storage](docs/specs/SPEC_CUSTOM_INSTALL_DIR_STORAGE.md)
- [Deployment and cleanup](docs/guides/DEPLOYMENT_AND_CLEANUP.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)

## License

MIT — see [LICENSE](LICENSE).
