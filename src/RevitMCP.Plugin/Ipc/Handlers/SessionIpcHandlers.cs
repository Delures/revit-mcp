using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Plugin.Ipc.Handlers
{
    public class PingIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public PingIpcHandler(IpcContext context)
        {
            _context = context;
        }

        public string Method => "ping";
        public bool RequiresIdleUi => false;

        public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            var session = _context.SessionInfoProvider();
            var result = new
            {
                status = "pong",
                revitVersion = session.RevitVersion,
                processId = session.ProcessId,
                uiState = session.UiState,
                activeDoc = session.ActiveDocumentTitle
            };

            return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
        }
    }

    public class GetSessionInfoIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public GetSessionInfoIpcHandler(IpcContext context)
        {
            _context = context;
        }

        public string Method => "get_session_info";
        public bool RequiresIdleUi => false;

        public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            var session = _context.SessionInfoProvider();
            return Task.FromResult(JsonRpcResponse.Success(request.Id, session));
        }
    }
}
