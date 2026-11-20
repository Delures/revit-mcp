using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Shared.Ipc
{
    /// <summary>
    /// Contract for an IPC request handler processing a specific JSON-RPC method.
    /// </summary>
    public interface IIpcHandler
    {
        /// <summary>
        /// The JSON-RPC method name this handler handles (case-insensitive).
        /// </summary>
        string Method { get; }

        /// <summary>
        /// If true, the dispatcher ensures Revit is not in sketch mode or displaying a modal dialog.
        /// </summary>
        bool RequiresIdleUi { get; }

        /// <summary>
        /// Handles the incoming JSON-RPC request and returns the response.
        /// </summary>
        Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken);
    }
}
