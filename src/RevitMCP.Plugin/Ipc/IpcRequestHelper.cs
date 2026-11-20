using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Logging;
using RevitMCP.Shared.Protocol;

namespace RevitMCP.Plugin.Ipc
{
    /// <summary>
    /// Helper utilities for IPC parameter deserialization and structured activity execution.
    /// </summary>
    public static class IpcRequestHelper
    {
        public static bool TryParseParams<T>(
            JsonRpcRequest request,
            string methodName,
            out T result,
            out JsonRpcResponse? errorResponse) where T : class, new()
        {
            if (request.Params == null)
            {
                result = new T();
                errorResponse = null;
                return true;
            }

            try
            {
                if (request.Params is JsonElement je)
                {
                    result = JsonSerializer.Deserialize<T>(je.GetRawText()) ?? new T();
                }
                else
                {
                    string json = JsonSerializer.Serialize(request.Params);
                    result = JsonSerializer.Deserialize<T>(json) ?? new T();
                }

                errorResponse = null;
                return true;
            }
            catch (Exception ex)
            {
                result = null!;
                errorResponse = JsonRpcResponse.CreateError(
                    request.Id,
                    JsonRpcErrorCodes.InvalidParams,
                    $"Invalid {methodName} parameters: {ex.Message}");
                return false;
            }
        }

        public static async Task<JsonRpcResponse> DispatchSimpleAsync<TResult>(
            IpcContext context,
            object? requestId,
            string operationName,
            Func<UIApplication, TResult> work,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await context.Dispatcher.DispatchAsync(
                    app => (object?)work(app),
                    operationName,
                    "MCP",
                    cancellationToken).ConfigureAwait(false);

                return JsonRpcResponse.Success(requestId, result);
            }
            catch (Exception ex)
            {
                return JsonRpcResponse.CreateError(requestId, JsonRpcErrorCodes.InternalError, ex.Message);
            }
        }

        public static async Task<JsonRpcResponse> DispatchWithActivityAsync<TResult>(
            IpcContext context,
            object? requestId,
            string agentName,
            string summary,
            Func<UIApplication, TResult> work,
            Func<TResult, (bool success, List<long>? affectedIds, string? errorMessage)> outcomeExtractor,
            CancellationToken cancellationToken)
        {
            string activityId = context.DockablePane?.BeginActivity(agentName, summary) ?? string.Empty;
            var sw = Stopwatch.StartNew();

            try
            {
                var rawResult = await context.Dispatcher.DispatchAsync(
                    app => (object?)work(app),
                    summary,
                    agentName,
                    cancellationToken).ConfigureAwait(false);

                sw.Stop();

                var typedResult = (TResult)rawResult!;
                var (success, affectedIds, errorMessage) = outcomeExtractor(typedResult);
                context.DockablePane?.CompleteActivity(
                    activityId,
                    success ? "Success" : "Error",
                    sw.ElapsedMilliseconds,
                    affectedIds,
                    errorMessage);

                AuditLogger.LogAction(agentName, summary, success, sw.ElapsedMilliseconds, errorMessage);

                return JsonRpcResponse.Success(requestId, typedResult);
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.DockablePane?.CompleteActivity(activityId, "Error", sw.ElapsedMilliseconds, null, ex.Message);
                AuditLogger.LogAction(agentName, summary, false, sw.ElapsedMilliseconds, ex.Message);

                return JsonRpcResponse.CreateError(requestId, JsonRpcErrorCodes.InternalError, ex.Message);
            }
        }
    }
}
