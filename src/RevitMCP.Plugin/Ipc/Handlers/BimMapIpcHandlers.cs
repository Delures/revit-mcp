using System;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Plugin.BimMap;
using RevitMCP.Shared.BimMap;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Plugin.Ipc.Handlers
{
    public class GetBimMapIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public GetBimMapIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "get_bim_map";
        public bool RequiresIdleUi => false;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<GetBimMapRequest>(request, Method, out var bimReq, out var errorResponse))
            {
                return errorResponse!;
            }

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                "Building BIM-Map",
                app => BimMapHandlers.HandleGetBimMap(app, bimReq),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class SearchBimMapIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public SearchBimMapIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "search_bim_map";
        public bool RequiresIdleUi => false;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<SearchBimMapRequest>(request, Method, out var searchReq, out var errorResponse))
            {
                return errorResponse!;
            }

            if (searchReq == null || string.IsNullOrWhiteSpace(searchReq.Query))
            {
                return JsonRpcResponse.CreateError(request.Id, JsonRpcErrorCodes.InvalidParams, "Query cannot be empty.");
            }

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                "Searching BIM-Map",
                app => BimMapHandlers.HandleSearchBimMap(app, searchReq),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class RebuildBimMapIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public RebuildBimMapIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "rebuild_bim_map";
        public bool RequiresIdleUi => false;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<RebuildBimMapRequest>(request, Method, out var rebuildReq, out var errorResponse))
            {
                return errorResponse!;
            }

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                "Rebuilding BIM-Map",
                app => BimMapHandlers.HandleRebuildBimMap(app, rebuildReq),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class ManageCacheIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public ManageCacheIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "manage_cache";
        public bool RequiresIdleUi => false;

        public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<ManageCacheRequest>(request, Method, out var cacheReq, out var errorResponse))
            {
                return Task.FromResult(errorResponse!);
            }

            try
            {
                ManageCacheResult result;
                if (string.Equals(cacheReq.Action, "clean", StringComparison.OrdinalIgnoreCase))
                {
                    result = CacheCleaner.Clean(cacheReq.MaxSizeMb, cacheReq.RetentionDays);
                }
                else
                {
                    result = CacheCleaner.GetStatus();
                }

                return Task.FromResult(JsonRpcResponse.Success(request.Id, result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(JsonRpcResponse.CreateError(request.Id, JsonRpcErrorCodes.InternalError, ex.Message));
            }
        }
    }
}
