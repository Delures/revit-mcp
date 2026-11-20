using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Plugin.Commands;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Utils;

namespace RevitMCP.Plugin.Ipc.Handlers
{
    public class CreateElementsIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public CreateElementsIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "create_elements";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<CreateElementsRequest>(request, Method, out var createReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(createReq.ActionSummary, createReq.Action, "create");
            string agent = HumanFriendlyNaming.ResolveAgentName(createReq.AgentName);

            return await IpcRequestHelper.DispatchWithActivityAsync(
                _context,
                request.Id,
                agent,
                summary,
                app =>
                {
                    var handler = new CreateElementsHandler();
                    return handler.Execute(app, createReq);
                },
                cRes => (cRes.Success, cRes.CreatedElementIds, cRes.ErrorMessage),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class TransformElementsIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public TransformElementsIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "transform_elements";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<TransformElementsRequest>(request, Method, out var transformReq, out var errorResponse))
            {
                return errorResponse!;
            }

            if (transformReq.ElementIds == null || transformReq.ElementIds.Count == 0)
            {
                return JsonRpcResponse.CreateError(request.Id, JsonRpcErrorCodes.InvalidParams, "Missing or empty elementIds for transform_elements.");
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(transformReq.ActionSummary, transformReq.Action, "transform", transformReq.ElementIds.Count);
            string agent = HumanFriendlyNaming.ResolveAgentName(transformReq.AgentName);

            return await IpcRequestHelper.DispatchWithActivityAsync(
                _context,
                request.Id,
                agent,
                summary,
                app =>
                {
                    var handler = new TransformElementsHandler();
                    return handler.Execute(app, transformReq);
                },
                tRes =>
                {
                    var affected = (tRes.NewElementIds != null && tRes.NewElementIds.Count > 0)
                        ? tRes.NewElementIds
                        : tRes.AffectedElementIds;
                    return (tRes.Success, affected, tRes.ErrorMessage);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class VisualOverrideIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public VisualOverrideIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "visual_override";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<VisualOverrideRequest>(request, Method, out var voReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(voReq.ActionSummary, voReq.Action, "visual_override", voReq.ElementIds?.Count ?? 0);
            string agent = HumanFriendlyNaming.ResolveAgentName(voReq.AgentName);

            return await IpcRequestHelper.DispatchWithActivityAsync(
                _context,
                request.Id,
                agent,
                summary,
                app =>
                {
                    var handler = new VisualOverrideHandler();
                    return handler.Execute(app, voReq);
                },
                voRes => (voRes.Success, voRes.AffectedElementIds?.Select(x => (long)x).ToList(), voRes.ErrorMessage),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class ManageParametersIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public ManageParametersIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "manage_parameters";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<ManageParametersRequest>(request, Method, out var paramReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(paramReq.ActionSummary, paramReq.Action, "parameters", paramReq.Parameters?.Count ?? 0);
            string agent = HumanFriendlyNaming.ResolveAgentName(paramReq.AgentName);

            return await IpcRequestHelper.DispatchWithActivityAsync(
                _context,
                request.Id,
                agent,
                summary,
                app =>
                {
                    var handler = new ManageParametersHandler();
                    return handler.Execute(app, paramReq);
                },
                pRes => (pRes.Success, pRes.Elements?.Select(e => e.ElementId).ToList(), pRes.ErrorMessage),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
