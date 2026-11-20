using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Plugin.Core;
using RevitMCP.Plugin.Ipc.Handlers;
using RevitMCP.Plugin.Logging;
using RevitMCP.Plugin.UI;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Session;

namespace RevitMCP.Plugin.Ipc
{
    /// <summary>
    /// Asynchronous Named Pipe IPC server managing connection lifecycles and delegating
    /// protocol requests to the <see cref="JsonRpcDispatcher"/>.
    /// </summary>
    public class NamedPipeIpcServer : IDisposable
    {
        private readonly string _pipeName;
        private readonly AgentActivityDockablePane? _dockablePane;
        private readonly JsonRpcDispatcher _rpcDispatcher;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        public NamedPipeIpcServer(
            string pipeName,
            RevitTaskDispatcher dispatcher,
            AgentActivityDockablePane? dockablePane,
            Func<RevitSessionInfo> sessionInfoProvider)
        {
            _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
            _dockablePane = dockablePane;

            var context = new IpcContext(dispatcher, dockablePane, sessionInfoProvider);
            _rpcDispatcher = CreateDefaultDispatcher(context);
        }

        private static JsonRpcDispatcher CreateDefaultDispatcher(IpcContext context)
        {
            var dispatcher = new JsonRpcDispatcher(context.SessionInfoProvider);

            // Session
            dispatcher.Register(new PingIpcHandler(context));
            dispatcher.Register(new GetSessionInfoIpcHandler(context));

            // Scripting
            dispatcher.Register(new ScriptingIpcHandler(context));

            // Modeling
            dispatcher.Register(new CreateElementsIpcHandler(context));
            dispatcher.Register(new TransformElementsIpcHandler(context));
            dispatcher.Register(new VisualOverrideIpcHandler(context));
            dispatcher.Register(new ManageParametersIpcHandler(context));

            // Analysis & Query
            dispatcher.Register(new InspectSchemaIpcHandler(context));
            dispatcher.Register(new QueryElementsIpcHandler(context));
            dispatcher.Register(new InspectElementGeometryIpcHandler(context));
            dispatcher.Register(new CheckIntersectionsIpcHandler(context));
            dispatcher.Register(new ManageSelectionIpcHandler(context));

            // Document & View
            dispatcher.Register(new ManageDocumentsIpcHandler(context));
            dispatcher.Register(new ExportViewImageIpcHandler(context));
            dispatcher.Register(new ExportScheduleIpcHandler(context));

            // BIM-Map & Cache
            dispatcher.Register(new GetBimMapIpcHandler(context));
            dispatcher.Register(new SearchBimMapIpcHandler(context));
            dispatcher.Register(new RebuildBimMapIpcHandler(context));
            dispatcher.Register(new ManageCacheIpcHandler(context));

            return dispatcher;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _serverTask = Task.Run(() => ServerLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                // Connect a dummy client to unblock WaitForConnectionAsync if waiting
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
                client.Connect(200);
            }
            catch { }

            try
            {
                _serverTask?.Wait(1000);
            }
            catch { }

            _rpcDispatcher.Dispose();
            _cts?.Dispose();
        }

        private async Task ServerLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream? serverStream = null;
                try
                {
                    serverStream = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    _dockablePane?.UpdatePipeStatus("Waiting for client");

                    await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                    _dockablePane?.UpdatePipeStatus("Client connected");

                    await HandleClientSessionAsync(serverStream, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AuditLogger.LogAction("Server", "IPC Server Loop Error", false, 0, ex.Message);
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (serverStream != null)
                    {
                        try
                        {
                            if (serverStream.IsConnected) serverStream.Disconnect();
                            serverStream.Dispose();
                        }
                        catch { }
                    }
                }
            }
        }

        private async Task HandleClientSessionAsync(NamedPipeServerStream pipeStream, CancellationToken cancellationToken)
        {
            var reader = new NdJsonStreamReader(pipeStream);
            var writer = new NdJsonStreamWriter(pipeStream);

            while (!cancellationToken.IsCancellationRequested && pipeStream.IsConnected)
            {
                try
                {
                    var request = await reader.ReadMessageAsync<JsonRpcRequest>(cancellationToken).ConfigureAwait(false);
                    if (request == null)
                    {
                        break; // End of stream or disconnect
                    }

                    var response = await _rpcDispatcher.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
                    await writer.WriteMessageAsync(response, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    try
                    {
                        var errResp = JsonRpcResponse.CreateError(null, JsonRpcErrorCodes.InternalError, ex.Message);
                        await writer.WriteMessageAsync(errResp, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }
                }
            }

            _dockablePane?.UpdatePipeStatus("Waiting for client");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
