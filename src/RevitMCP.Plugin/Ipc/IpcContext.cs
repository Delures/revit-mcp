using System;
using RevitMCP.Plugin.Core;
using RevitMCP.Plugin.UI;
using RevitMCP.Shared.Session;

namespace RevitMCP.Plugin.Ipc
{
    /// <summary>
    /// Shared dependencies and contextual services for IPC request execution.
    /// </summary>
    public class IpcContext
    {
        public RevitTaskDispatcher Dispatcher { get; }
        public AgentActivityDockablePane? DockablePane { get; }
        public Func<RevitSessionInfo> SessionInfoProvider { get; }

        public IpcContext(
            RevitTaskDispatcher dispatcher,
            AgentActivityDockablePane? dockablePane,
            Func<RevitSessionInfo> sessionInfoProvider)
        {
            Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            DockablePane = dockablePane;
            SessionInfoProvider = sessionInfoProvider ?? throw new ArgumentNullException(nameof(sessionInfoProvider));
        }
    }
}
