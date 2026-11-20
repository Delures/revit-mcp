using System;
using System.IO;

namespace RevitMCP.Shared
{
    /// <summary>
    /// Centralized storage context ensuring isolated user data directories and IPC channel prefixes
    /// under %LocalAppData%\&lt;McpName&gt;\ based on the configured McpName (e.g. RevitMCP, BimCore, MyMCP).
    /// </summary>
    public static class McpStorageContext
    {
        public const string DefaultMcpName = "RevitMCP";
        private static string _mcpName = DefaultMcpName;

        /// <summary>
        /// Gets or sets the name/brand of the MCP instance (e.g. "RevitMCP", "BimCore").
        /// </summary>
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

        /// <summary>
        /// Resets the context back to DefaultMcpName ("RevitMCP"). Primarily for unit tests.
        /// </summary>
        public static void Reset()
        {
            _mcpName = DefaultMcpName;
        }

        /// <summary>
        /// Initializes the storage context using explicit parameter, environment variable,
        /// or auto-detecting the root directory relative to the current binary location.
        /// </summary>
        public static void Initialize(string? mcpName = null)
        {
            if (!string.IsNullOrWhiteSpace(mcpName))
            {
                McpName = mcpName!;
                return;
            }

            // 1. Environment variable override
            string? envName = Environment.GetEnvironmentVariable("REVIT_MCP_NAME");
            if (!string.IsNullOrWhiteSpace(envName))
            {
                McpName = envName!;
                return;
            }

            // 2. Auto-detection from assembly / application base directory
            // Typically installed at: %LocalAppData%\<McpName>\bin\cli\ or %LocalAppData%\<McpName>\bin\2022\
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir))
                {
                    var parent = Directory.GetParent(baseDir);
                    if (parent != null)
                    {
                        if (parent.Name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                        {
                            var rootCandidate = parent.Parent?.Name;
                            if (!string.IsNullOrWhiteSpace(rootCandidate))
                            {
                                McpName = rootCandidate!;
                                return;
                            }
                        }
                        else if (baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                        .EndsWith(Path.DirectorySeparatorChar + "bin", StringComparison.OrdinalIgnoreCase))
                        {
                            var rootCandidate = parent.Name;
                            if (!string.IsNullOrWhiteSpace(rootCandidate))
                            {
                                McpName = rootCandidate!;
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default
            }
        }

        /// <summary>
        /// Gets the Named Pipe prefix for IPC communication, e.g. "RevitMCP-" or "BimCore-".
        /// </summary>
        public static string PipePrefix => $"{McpName}-";

        /// <summary>
        /// Root directory in %LocalAppData%\&lt;McpName&gt;\
        /// </summary>
        public static string RootDirectory
        {
            get
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return EnsureDir(Path.Combine(localApp, McpName));
            }
        }

        /// <summary>
        /// Directory for SQLite BIM-Map database files (*.db, *.db-wal).
        /// </summary>
        public static string ModelsDirectory => EnsureDir(Path.Combine(RootDirectory, "models"));

        /// <summary>
        /// Directory for active Revit session manifests ({Year}-{PID}.json).
        /// </summary>
        public static string SessionsDirectory => EnsureDir(Path.Combine(RootDirectory, "sessions"));

        /// <summary>
        /// Directory for agent knowledge lessons and index.
        /// </summary>
        public static string KnowledgeDirectory => EnsureDir(Path.Combine(RootDirectory, "knowledge"));

        /// <summary>
        /// Directory for audit and diagnostics logs.
        /// </summary>
        public static string LogsDirectory => EnsureDir(Path.Combine(RootDirectory, "logs"));

        /// <summary>
        /// Directory for user-created dynamic tools and external scripts.
        /// </summary>
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
