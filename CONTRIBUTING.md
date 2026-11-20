# Contributing

Thanks for helping out. Below is the minimum set of rules that keeps changes easy to review and merge.

## Requirements

| Component | Why |
|---|---|
| .NET SDK 8.0+ | builds the plugin for Revit 2025–2026, the CLI and the tests |
| .NET SDK 10.0 | required only for Revit 2027 |
| Revit 2019–2027 (whichever you have) | live verification of changes |
| Inno Setup 6 | building the installer |

## Build and test

```powershell
# build the plugin for a specific Revit version
dotnet build src\RevitMCP.Plugin\RevitMCP.Plugin.csproj -c R2025

# build every configuration
2019..2027 | ForEach-Object { dotnet build src\RevitMCP.Plugin\RevitMCP.Plugin.csproj -c "R$_" }

# tests
dotnet test tests\RevitMCP.Tests\RevitMCP.Tests.csproj -c R2025

# installer
pwsh -NoProfile -File .\build-installer.ps1 -AppVersion 1.0.0
```

Before opening a pull request, make sure that:

- `dotnet test` passes completely (currently 101 tests);
- the plugin builds for every Revit version the change touches;
- no new compiler warnings are introduced: every configuration must build with `0 warnings, 0 errors`. `NU1903` for `SQLitePCLRaw.lib.e_sqlite3` is suppressed explicitly (see `NuGetAuditSuppress` in `src/RevitMCP.Plugin/RevitMCP.Plugin.csproj`), so do not mute it globally.

## Code conventions

- UI strings, code comments and documentation are written in English; identifiers are English as well.
- C# style is defined in `.editorconfig`; format with `dotnet format`.
- A new MCP tool is a registry entry in `src/RevitMCP.Cli/Mcp/McpToolRegistry.cs`, an IPC handler in `src/RevitMCP.Plugin/Ipc/Handlers/` and a test.
- Version-dependent Revit API code goes behind `#if REVIT_2019`, `#if REVIT_2027` and similar constants defined in `Directory.Build.props`.
- Public APIs and IPC contracts must stay compatible: the CLI and the plugin may run different versions.

## Commits

Format: a short imperative subject line (up to 72 characters), then a blank line and the details (what and why).

```
Fix pipe reconnect after Revit restart

The socket was closed before the response was sent when Revit restarted
in the middle of a call. The error now reaches the client, and a retry
waits for the new instance.
```

Do not commit build artefacts, local AI-client settings or anything listed in `.gitignore`.

## Documentation changes

- Links inside the repository are relative (`../../src/...`), never `file:///`.
- User folder paths use a placeholder (`C:\Users\<you>\...`).
- Notable changes go into `CHANGELOG.md`.

## Reporting bugs

Please include:

- Revit version and server version (`RevitMCP.Cli.exe --version`);
- the log from `%LocalAppData%\RevitMCP\logs\`;
- the MCP client and its version;
- a minimal reproduction (a tool call or an `execute_csharp` script is ideal).
