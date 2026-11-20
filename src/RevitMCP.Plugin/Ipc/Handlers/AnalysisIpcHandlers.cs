using System;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Plugin.Commands;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Utils;

namespace RevitMCP.Plugin.Ipc.Handlers
{
    public class InspectSchemaIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public InspectSchemaIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "inspect_schema";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<InspectSchemaRequest>(request, Method, out var schemaReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(schemaReq.ActionSummary, schemaReq.Category, "inspect_schema");

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null) throw new InvalidOperationException("No active document in Revit.");
                    return InspectSchemaHandler.Execute(doc, schemaReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class QueryElementsIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public QueryElementsIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "query_elements";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<QueryElementsRequest>(request, Method, out var queryReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(queryReq.ActionSummary, queryReq.Category, "query_elements");

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null) throw new InvalidOperationException("No active document in Revit.");
                    return QueryElementsHandler.Execute(doc, queryReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class InspectElementGeometryIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public InspectElementGeometryIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "inspect_element_geometry";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<InspectElementGeometryRequest>(request, Method, out var geomReq, out var errorResponse))
            {
                return errorResponse!;
            }

            if (geomReq == null || geomReq.ElementId <= 0)
            {
                return JsonRpcResponse.CreateError(request.Id, JsonRpcErrorCodes.InvalidParams, "elementId is required and must be greater than 0.");
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(geomReq.ActionSummary, null, "inspect_element_geometry", geomReq.ElementId);

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var handler = new InspectElementGeometryHandler();
                    return handler.Execute(app, geomReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class CheckIntersectionsIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public CheckIntersectionsIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "check_intersections";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<CheckIntersectionsRequest>(request, Method, out var clashReq, out var errorResponse))
            {
                return errorResponse!;
            }

            bool isElementMode = string.IsNullOrEmpty(clashReq.Mode) || string.Equals(clashReq.Mode, "element", StringComparison.OrdinalIgnoreCase);

            if (isElementMode && clashReq.TargetElementId <= 0)
            {
                return JsonRpcResponse.CreateError(request.Id, JsonRpcErrorCodes.InvalidParams, "targetElementId is required and must be > 0 for mode 'element'.");
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(clashReq.ActionSummary, null, "check_intersections", clashReq.TargetElementId ?? 0L);

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null)
                    {
                        return new CheckIntersectionsResult
                        {
                            Success = false,
                            ErrorCode = "NO_ACTIVE_DOCUMENT",
                            ErrorMessage = "No active document open in Revit."
                        };
                    }
                    var handler = new CheckIntersectionsHandler();
                    return handler.Execute(doc, clashReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class ManageSelectionIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public ManageSelectionIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "manage_selection";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<ManageSelectionRequest>(request, Method, out var selReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(selReq.ActionSummary, selReq.Action, "manage_selection", selReq.ElementIds?.Count ?? 0);

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var handler = new ManageSelectionHandler();
                    return handler.Execute(app, selReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}
