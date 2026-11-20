using System;
using System.Reflection;

namespace RevitMCP.Cli.Mcp
{
    /// <summary>
    ///     Single source of truth for the server version shown by the CLI and the MCP handshake.
    /// </summary>
    public static class VersionInfo
    {
        /// <summary>
        ///     Version taken from the assembly informational version (set from the MSBuild Version property).
        /// </summary>
        public static string Current { get; } = Resolve();

        private static string Resolve()
        {
            var assembly = typeof(VersionInfo).Assembly;
            string? informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // Strip the source-revision suffix ("1.0.0+abc123") added by the SDK.
                int plus = informational.IndexOf('+');
                return plus > 0 ? informational.Substring(0, plus) : informational;
            }

            return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        }
    }
}
