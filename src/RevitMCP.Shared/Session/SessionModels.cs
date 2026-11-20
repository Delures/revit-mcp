using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using RevitMCP.Shared.Ipc;

namespace RevitMCP.Shared.Session
{
    public class RevitSessionInfo
    {
        [JsonPropertyName("processId")]
        public int ProcessId { get; set; }

        [JsonPropertyName("revitVersion")]
        public string RevitVersion { get; set; } = string.Empty;

        [JsonPropertyName("pipeName")]
        public string PipeName { get; set; } = string.Empty;

        [JsonPropertyName("mainWindowHandle")]
        public long MainWindowHandle { get; set; }

        [JsonPropertyName("mainWindowTitle")]
        public string MainWindowTitle { get; set; } = string.Empty;

        [JsonPropertyName("activeDocumentPath")]
        public string? ActiveDocumentPath { get; set; }

        [JsonPropertyName("activeDocumentTitle")]
        public string? ActiveDocumentTitle { get; set; }

        [JsonPropertyName("isDocumentOpen")]
        public bool IsDocumentOpen { get; set; }

        [JsonPropertyName("uiState")]
        public string UiState { get; set; } = RevitUiStates.Ready;

        [JsonPropertyName("startTimeUtc")]
        public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lastHeartbeatUtc")]
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    }

    public static class RevitUiStates
    {
        public const string Ready = "Ready";
        public const string InSketchMode = "InSketchMode";
        public const string ModalActive = "ModalActive";
        public const string Processing = "Processing";
    }

    public static class SessionRegistry
    {
        public static string GetSessionsDirectory()
        {
            return McpStorageContext.SessionsDirectory;
        }

        public static string GetSessionFilePath(string revitVersion, int processId)
        {
            return Path.Combine(GetSessionsDirectory(), $"{revitVersion}-{processId}.json");
        }

        public static void RegisterSession(RevitSessionInfo session)
        {
            string filePath = GetSessionFilePath(session.RevitVersion, session.ProcessId);
            string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public static void UnregisterSession(string revitVersion, int processId)
        {
            try
            {
                string filePath = GetSessionFilePath(revitVersion, processId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Ignore cleanup errors on process exit
            }
        }

        public static List<RevitSessionInfo> GetActiveSessions(bool pruneDeadSessions = true)
        {
            var result = new List<RevitSessionInfo>();
            string dir = GetSessionsDirectory();
            if (!Directory.Exists(dir))
            {
                return result;
            }

            string[] files = Directory.GetFiles(dir, "*.json");
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var session = JsonSerializer.Deserialize<RevitSessionInfo>(json);
                    if (session == null)
                    {
                        continue;
                    }

                    if (pruneDeadSessions)
                    {
                        // Check if pipe or process still exists
                        bool isAlive = IsPipeOrProcessAlive(session.PipeName, session.ProcessId);
                        if (!isAlive)
                        {
                            try { File.Delete(file); } catch { }
                            continue;
                        }
                    }

                    result.Add(session);
                }
                catch
                {
                    // If corrupted file, delete if pruning
                    if (pruneDeadSessions)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }

            return result;
        }

        private static bool IsPipeOrProcessAlive(string pipeName, int processId)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return false;
                }
            }
            catch (ArgumentException)
            {
                return false; // Process does not exist
            }
            catch
            {
                // Access denied or other process query issue
            }

            return true;
        }
    }
}
