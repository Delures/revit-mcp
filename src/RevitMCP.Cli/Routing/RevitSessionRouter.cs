using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RevitMCP.Cli.Client;
using RevitMCP.Shared.Session;

namespace RevitMCP.Cli.Routing
{
    public class RevitSessionRouter
    {
        private int? _selectedPid;
        private RevitMcpClient? _activeClient;
        private string? _activePipeName;

        public void SelectInstance(int pid)
        {
            var sessions = SessionRegistry.GetActiveSessions();
            var target = sessions.FirstOrDefault(s => s.ProcessId == pid);
            if (target == null)
            {
                throw new ArgumentException($"Revit session with PID {pid} was not found among active sessions.");
            }

            _selectedPid = pid;
            if (_activePipeName != target.PipeName)
            {
                _activeClient?.Dispose();
                _activeClient = null;
                _activePipeName = null;
            }
        }

        public List<RevitSessionInfo> GetActiveSessions()
        {
            return SessionRegistry.GetActiveSessions();
        }

        public async Task<RevitMcpClient> GetClientAsync(CancellationToken cancellationToken = default)
        {
            var sessions = SessionRegistry.GetActiveSessions();

            if (sessions.Count == 0)
            {
                throw new InvalidOperationException(
                    "No active Revit instances found with the Revit MCP plugin loaded. Please start Revit and open a project.");
            }

            RevitSessionInfo target;

            if (_selectedPid.HasValue)
            {
                target = sessions.FirstOrDefault(s => s.ProcessId == _selectedPid.Value)
                    ?? throw new InvalidOperationException($"Previously selected Revit instance (PID {_selectedPid.Value}) is no longer running.");
            }
            else if (sessions.Count == 1)
            {
                target = sessions[0];
            }
            else
            {
                string instancesSummary = string.Join(", ", sessions.Select(s => $"PID: {s.ProcessId} (Revit {s.RevitVersion}, Doc: {s.ActiveDocumentTitle ?? "None"})"));
                throw new InvalidOperationException(
                    $"Multiple Revit instances are active ({instancesSummary}). Use tool 'select_revit_instance' with 'processId' to target one.");
            }

            if (_activeClient != null && _activePipeName == target.PipeName)
            {
                return _activeClient;
            }

            _activeClient?.Dispose();
            _activeClient = new RevitMcpClient(target.PipeName);
            await _activeClient.ConnectAsync(3000, cancellationToken).ConfigureAwait(false);
            _activePipeName = target.PipeName;

            return _activeClient;
        }
    }
}
