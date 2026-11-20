using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Session;

namespace RevitMCP.Shared.Ipc
{
    /// <summary>
    /// Router and middleware pipeline for incoming JSON-RPC requests.
    /// </summary>
    public class JsonRpcDispatcher : IDisposable
    {
        private readonly Dictionary<string, IIpcHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<RevitSessionInfo> _sessionInfoProvider;

        public JsonRpcDispatcher(Func<RevitSessionInfo> sessionInfoProvider)
        {
            _sessionInfoProvider = sessionInfoProvider ?? throw new ArgumentNullException(nameof(sessionInfoProvider));
        }

        public void Register(IIpcHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[handler.Method] = handler;
        }

        public bool TryGetHandler(string method, out IIpcHandler? handler)
        {
            return _handlers.TryGetValue(method, out handler);
        }

        public async Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Method))
            {
                return JsonRpcResponse.CreateError(request?.Id, JsonRpcErrorCodes.InvalidRequest, "Method cannot be empty.");
            }

            if (!_handlers.TryGetValue(request.Method, out var handler))
            {
                return JsonRpcResponse.CreateError(
                    request.Id,
                    JsonRpcErrorCodes.MethodNotFound,
                    $"Unknown method '{request.Method}'");
            }

            // Middleware: Revit UI Busy Check
            if (handler.RequiresIdleUi)
            {
                var session = _sessionInfoProvider();
                if (session.UiState == RevitUiStates.InSketchMode || session.UiState == RevitUiStates.ModalActive)
                {
                    return JsonRpcResponse.CreateError(
                        request.Id,
                        JsonRpcErrorCodes.RevitUiBusy,
                        $"Revit UI is busy (state: {session.UiState}). Exit sketch mode or close open dialogs in Revit before executing.");
                }
            }

            try
            {
                return await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return JsonRpcResponse.CreateError(request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
            }
        }

        public void Dispose()
        {
            foreach (var handler in _handlers.Values)
            {
                if (handler is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch { }
                }
            }
            _handlers.Clear();
        }
    }
}
