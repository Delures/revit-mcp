using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Plugin.Failures;
using RevitMCP.Plugin.Logging;
using RevitMCP.Plugin.Scripting;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Scripting;
using RevitMCP.Shared.Utils;

namespace RevitMCP.Plugin.Ipc.Handlers
{
    public class ScriptingIpcHandler : IIpcHandler, IDisposable
    {
        private readonly IpcContext _context;
        private readonly RoslynScriptEngine _scriptEngine = new();
        private SmartDialogManager? _dialogManager;

        public ScriptingIpcHandler(IpcContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public string Method => "execute_csharp";
        public bool RequiresIdleUi => true;

        public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (!IpcRequestHelper.TryParseParams<ExecuteScriptRequest>(request, Method, out var scriptReq, out var errorResponse))
            {
                return errorResponse!;
            }

            if (scriptReq == null || string.IsNullOrWhiteSpace(scriptReq.Code))
            {
                return JsonRpcResponse.CreateError(request.Id, JsonRpcErrorCodes.InvalidParams, "Code cannot be empty.");
            }

            string agentName = HumanFriendlyNaming.ResolveAgentName(scriptReq.AgentName);
            string summary = HumanFriendlyNaming.ResolveActivitySummary(scriptReq.ActionSummary, null, "execute_csharp");

            string activityId = _context.DockablePane?.BeginActivity(agentName, summary) ?? string.Empty;

            try
            {
                object? resultObj = await _context.Dispatcher.DispatchAsync(app =>
                {
                    if (_dialogManager == null)
                    {
                        _dialogManager = new SmartDialogManager(app);
                    }

                    var scriptContext = new RevitScriptContext(app);
                    _dialogManager.SetActiveContext(scriptContext);

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(scriptReq.TransactionName))
                        {
                            string txName = HumanFriendlyNaming.ResolveTransactionName(scriptReq.TransactionName);
                            return scriptContext.WithTransaction(txName, () =>
                            {
                                return _scriptEngine.Execute(scriptReq.Code, scriptContext, scriptReq.Arguments);
                            });
                        }
                        else
                        {
                            return _scriptEngine.Execute(scriptReq.Code, scriptContext, scriptReq.Arguments);
                        }
                    }
                    finally
                    {
                        _dialogManager.SetActiveContext(null);
                    }
                }, summary, agentName, cancellationToken).ConfigureAwait(false);

                var scriptResult = resultObj as ExecuteScriptResult ?? new ExecuteScriptResult
                {
                    Success = true,
                    ReturnValue = resultObj
                };

                _context.DockablePane?.CompleteActivity(
                    activityId,
                    scriptResult.Success ? "Success" : "Error",
                    scriptResult.ExecutionTimeMs,
                    scriptResult.AffectedElementIds,
                    scriptResult.ErrorMessage);

                AuditLogger.LogAction(agentName, summary, scriptResult.Success, scriptResult.ExecutionTimeMs, scriptResult.ErrorMessage);

                return JsonRpcResponse.Success(request.Id, scriptResult);
            }
            catch (Exception ex)
            {
                _context.DockablePane?.CompleteActivity(activityId, "Error", 0, null, ex.Message);
                AuditLogger.LogAction(agentName, summary, false, 0, ex.Message);

                return JsonRpcResponse.CreateError(
                    request.Id,
                    JsonRpcErrorCodes.ScriptExecutionFailed,
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _dialogManager?.Dispose();
        }
    }
}
