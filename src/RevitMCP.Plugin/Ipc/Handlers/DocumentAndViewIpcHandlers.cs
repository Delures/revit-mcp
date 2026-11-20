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
    public class ManageDocumentsIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public ManageDocumentsIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "manage_documents";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<ManageDocumentsRequest>(request, Method, out var docReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(docReq.ActionSummary, docReq.Action, "manage_documents");

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var handler = new ManageDocumentsHandler();
                    return handler.Execute(app, docReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class ExportViewImageIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public ExportViewImageIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "export_view_image";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<ExportViewImageRequest>(request, Method, out var exportReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(exportReq.ActionSummary, null, "export_view_image");

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var handler = new ExportViewImageHandler();
                    return handler.Execute(app, exportReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public class ExportScheduleIpcHandler : IIpcHandler
    {
        private readonly IpcContext _context;

        public ExportScheduleIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "export_schedule";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<ExportScheduleRequest>(request, Method, out var schedReq, out var errorResponse))
            {
                return errorResponse!;
            }

            string summary = HumanFriendlyNaming.ResolveActivitySummary(schedReq.ActionSummary, schedReq.ScheduleName, "export_schedule");

            return await IpcRequestHelper.DispatchSimpleAsync(
                _context,
                request.Id,
                summary,
                app =>
                {
                    var handler = new ExportScheduleHandler();
                    return handler.Execute(app, schedReq);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}
