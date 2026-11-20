# Technical specification: Isolated data storage by MCP name (McpName & LocalAppData Isolation)

> **Status:** Approved / In implementation
> **Version:** 1.1.0
> **Project:** RevitMCP / BimCore MCP Fork
> **Stack:** C# 12, .NET 8 / .NET Framework 4.8, Windows Named Pipes, SQLite, Revit API

---

## 1. Concept and goals

### 1.1. Placement decision (Zero-UAC Architecture)
To avoid Windows access-rights issues (UAC, prohibition on writing to `C:\Program Files` for a regular user when SQLite and logs run), the following decision was made:
- All MCP instances are always placed strictly in **`%LocalAppData%\<McpName>`**.
- An arbitrary absolute path is not set — only the **MCP name** (`McpName` / `BrandName`) is configured:
  - Default (Upstream): `"RevitMCP"` $\rightarrow$ `%LocalAppData%\RevitMCP\`
  - For Ivan's fork: `"BimCore"` $\rightarrow$ `%LocalAppData%\BimCore\`
  - For other solutions: `"MyMCP"` $\rightarrow$ `%LocalAppData%\MyMCP\`

### 1.2. Improvement goals
1. Create a single **`McpStorageContext`** class in `RevitMCP.Shared`.
2. Ensure full isolation of data and communication channels between different instances on one PC:
   - Dedicated SQLite model databases in `%LocalAppData%\<McpName>\models\`;
   - Dedicated session manifests in `%LocalAppData%\<McpName>\sessions\`;
   - Dedicated knowledge base in `%LocalAppData%\<McpName>\knowledge\`;
   - Dedicated audit logs in `%LocalAppData%\<McpName>\logs\`;
   - Dedicated storage for generated tools in `%LocalAppData%\<McpName>\tools\`;
   - Dedicated Named Pipe channel prefix: `\\.\pipe\<McpName>-{Year}-{PID}`.
3. Support inheriting 100% of the code in the BimCore fork without editing business logic: the fork changes only `McpName = "BimCore"`.

---

## 2. Solution architecture

### 2.1. Directory structure

```
%LocalAppData%\<McpName>/
├── bin/                       # Executables
│   ├── cli/                   # RevitMCP.Cli.exe (.NET 8)
│   └── 2022/                  # RevitMCP.Plugin.dll (.NET 4.8 / .NET 8)
├── models/                    # BIM-Map SQLite databases (*.db, *.db-wal)
├── sessions/                  # JSON manifests of active Revit sessions ({Year}-{PID}.json)
├── knowledge/                 # Agent knowledge base (lessons.jsonl, index.json)
├── logs/                      # Audit and diagnostic logs (audit.log)
└── tools/                     # Saved dynamic tools (C#, manifests)
```

---

### 2.2. The `McpStorageContext` class (`RevitMCP.Shared`)

```csharp
using System;
using System.IO;

namespace RevitMCP.Shared
{
    public static class McpStorageContext
    {
        private static string _mcpName = "RevitMCP";

        public static string McpName
        {
            get => _mcpName;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _mcpName = value.Trim();
                }
            }
        }

        public static void Initialize(string? mcpName = null)
        {
            if (!string.IsNullOrWhiteSpace(mcpName))
            {
                McpName = mcpName!;
                return;
            }

            // 1. Environment variable
            string? envName = Environment.GetEnvironmentVariable("REVIT_MCP_NAME");
            if (!string.IsNullOrWhiteSpace(envName))
            {
                McpName = envName!;
                return;
            }

            // 2. Auto-detection from the binaries location
            // If the assembly sits in %LocalAppData%\<McpName>\bin\...
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var parent = Directory.GetParent(baseDir);
                if (parent != null && parent.Name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                {
                    var rootCandidate = parent.Parent?.Name;
                    if (!string.IsNullOrWhiteSpace(rootCandidate))
                    {
                        McpName = rootCandidate;
                        return;
                    }
                }
            }
            catch
            {
                // Fallback to the default value
            }
        }

        public static string PipePrefix => $"{McpName}-";

        public static string RootDirectory
        {
            get
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return EnsureDir(Path.Combine(localApp, McpName));
            }
        }

        public static string ModelsDirectory => EnsureDir(Path.Combine(RootDirectory, "models"));
        public static string SessionsDirectory => EnsureDir(Path.Combine(RootDirectory, "sessions"));
        public static string KnowledgeDirectory => EnsureDir(Path.Combine(RootDirectory, "knowledge"));
        public static string LogsDirectory => EnsureDir(Path.Combine(RootDirectory, "logs"));
        public static string ToolsDirectory => EnsureDir(Path.Combine(RootDirectory, "tools"));

        private static string EnsureDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }
}
```

---

## 3. Refactoring points in the codebase

1. **`RevitMCP.Shared/BimMap/BimMapDatabase.cs`**:
   `GetDefaultCacheDirectory()` $\rightarrow$ `McpStorageContext.ModelsDirectory`.
2. **`RevitMCP.Shared/BimMap/CacheCleaner.cs`**:
   `GetDefaultCacheDirectory()` $\rightarrow$ `McpStorageContext.ModelsDirectory`.
3. **`RevitMCP.Shared/Session/SessionModels.cs`**:
   `GetSessionsDirectory()` $\rightarrow$ `McpStorageContext.SessionsDirectory`.
4. **`RevitMCP.Shared/Knowledge/AgentKnowledgeManager.cs`**:
   Default storage $\rightarrow$ `McpStorageContext.KnowledgeDirectory`.
5. **`RevitMCP.Plugin/Logging/AuditLogger.cs`**:
   `GetLogDirectory()` $\rightarrow$ `McpStorageContext.LogsDirectory`.
6. **`RevitMCP.Shared/Ipc/NdJsonStream.cs`**:
   `PipePrefix` $\rightarrow$ `McpStorageContext.PipePrefix`.
7. **`RevitMCP.Cli/Program.cs`**:
   Adding the `--mcp-name <name>` / `--name <name>` argument for explicitly specifying the instance at launch.
8. **`deploy-addin.ps1`**:
   Parameterization of `$McpName = "RevitMCP"` (path `$env:LOCALAPPDATA\$McpName`).
