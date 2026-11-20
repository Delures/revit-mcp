using System;
using System.IO;
using System.Text.Json;
using RevitMCP.Shared;

namespace RevitMCP.Plugin.Logging
{
    public static class AuditLogger
    {
        private static readonly object _lock = new object();

        public static string GetLogDirectory()
        {
            return McpStorageContext.LogsDirectory;
        }

        public static void LogAction(string agentName, string action, bool success, long durationMs, string? error = null)
        {
            try
            {
                string logFile = Path.Combine(GetLogDirectory(), "audit.log");
                var entry = new
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    agent = agentName,
                    action = action,
                    success = success,
                    durationMs = durationMs,
                    error = error
                };

                string line = JsonSerializer.Serialize(entry);
                lock (_lock)
                {
                    File.AppendAllText(logFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never crash the host on logger errors
            }
        }
    }
}
