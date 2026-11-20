using System;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.BimMap;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Scripting;
using RevitMCP.Shared.Session;

namespace RevitMCP.Cli.Client
{
    public class RevitMcpClient : IDisposable
    {
        private readonly string _pipeName;
        private NamedPipeClientStream? _pipeClient;
        private NdJsonStreamReader? _reader;
        private NdJsonStreamWriter? _writer;
        private long _requestId = 0;

        public RevitMcpClient(string pipeName)
        {
            _pipeName = pipeName;
        }

        public async Task ConnectAsync(int timeoutMs = 3000, CancellationToken cancellationToken = default)
        {
            _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
#if NET8_0_OR_GREATER
            await _pipeClient.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
#else
            _pipeClient.Connect(timeoutMs);
#endif
            _reader = new NdJsonStreamReader(_pipeClient);
            _writer = new NdJsonStreamWriter(_pipeClient);
        }

        public async Task<JsonRpcResponse> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken = default)
        {
            if (_pipeClient == null || !_pipeClient.IsConnected || _writer == null || _reader == null)
            {
                await ConnectAsync(3000, cancellationToken).ConfigureAwait(false);
            }

            long id = Interlocked.Increment(ref _requestId);
            var request = new JsonRpcRequest
            {
                Id = id,
                Method = method,
                Params = parameters
            };

            await _writer!.WriteMessageAsync(request, cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var response = await _reader!.ReadMessageAsync<JsonRpcResponse>(cancellationToken).ConfigureAwait(false);
                if (response == null)
                {
                    throw new InvalidOperationException("Disconnected from Revit plugin.");
                }

                // Match response ID
                if (response.Id?.ToString() == id.ToString())
                {
                    return response;
                }
            }

            throw new OperationCanceledException(cancellationToken);
        }

        public async Task<ExecuteScriptResult> ExecuteCSharpAsync(ExecuteScriptRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("execute_csharp", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                return new ExecuteScriptResult
                {
                    Success = false,
                    ErrorCode = response.Error.Code.ToString(),
                    ErrorMessage = response.Error.Message
                };
            }

            if (response.Result == null)
            {
                return new ExecuteScriptResult { Success = true };
            }

            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<ExecuteScriptResult>(json) ?? new ExecuteScriptResult { Success = true };
        }

        public async Task<InspectSchemaResult> InspectSchemaAsync(InspectSchemaRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("inspect_schema", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"inspect_schema failed: {response.Error.Message}");
            }

            if (response.Result == null) return new InspectSchemaResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<InspectSchemaResult>(json) ?? new InspectSchemaResult();
        }

        public async Task<QueryElementsResult> QueryElementsAsync(QueryElementsRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("query_elements", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"query_elements failed: {response.Error.Message}");
            }

            if (response.Result == null) return new QueryElementsResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<QueryElementsResult>(json) ?? new QueryElementsResult();
        }

        public async Task<ManageParametersResult> ManageParametersAsync(ManageParametersRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("manage_parameters", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"manage_parameters failed: {response.Error.Message}");
            }

            if (response.Result == null) return new ManageParametersResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<ManageParametersResult>(json) ?? new ManageParametersResult();
        }

        public async Task<ManageDocumentsResult> ManageDocumentsAsync(ManageDocumentsRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("manage_documents", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"manage_documents failed: {response.Error.Message}");
            }

            if (response.Result == null) return new ManageDocumentsResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<ManageDocumentsResult>(json) ?? new ManageDocumentsResult();
        }

        public async Task<BimMapResult> GetBimMapAsync(GetBimMapRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("get_bim_map", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"get_bim_map failed: {response.Error.Message}");
            }

            if (response.Result == null) return new BimMapResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<BimMapResult>(json) ?? new BimMapResult();
        }

        public async Task<SearchBimMapResult> SearchBimMapAsync(SearchBimMapRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("search_bim_map", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"search_bim_map failed: {response.Error.Message}");
            }

            if (response.Result == null) return new SearchBimMapResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<SearchBimMapResult>(json) ?? new SearchBimMapResult();
        }

        public async Task<RebuildBimMapResult> RebuildBimMapAsync(RebuildBimMapRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("rebuild_bim_map", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"rebuild_bim_map failed: {response.Error.Message}");
            }

            if (response.Result == null) return new RebuildBimMapResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<RebuildBimMapResult>(json) ?? new RebuildBimMapResult();
        }

        public async Task<ManageCacheResult> ManageCacheAsync(ManageCacheRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("manage_cache", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"manage_cache failed: {response.Error.Message}");
            }

            if (response.Result == null) return new ManageCacheResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<ManageCacheResult>(json) ?? new ManageCacheResult();
        }

        public async Task<ExportViewImageResult> ExportViewImageAsync(ExportViewImageRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("export_view_image", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"export_view_image failed: {response.Error.Message}");
            }

            if (response.Result == null) return new ExportViewImageResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<ExportViewImageResult>(json) ?? new ExportViewImageResult();
        }

        public async Task<TransformElementsResult> TransformElementsAsync(TransformElementsRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("transform_elements", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"transform_elements failed: {response.Error.Message}");
            }

            if (response.Result == null) return new TransformElementsResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<TransformElementsResult>(json) ?? new TransformElementsResult();
        }

        public async Task<CreateElementsResult> CreateElementsAsync(CreateElementsRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("create_elements", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"create_elements failed: {response.Error.Message}");
            }

            if (response.Result == null) return new CreateElementsResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<CreateElementsResult>(json) ?? new CreateElementsResult();
        }

        public async Task<CheckIntersectionsResult> CheckIntersectionsAsync(CheckIntersectionsRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("check_intersections", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"check_intersections failed: {response.Error.Message}");
            }

            if (response.Result == null) return new CheckIntersectionsResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<CheckIntersectionsResult>(json) ?? new CheckIntersectionsResult();
        }

        public async Task<ManageSelectionResult> ManageSelectionAsync(ManageSelectionRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("manage_selection", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"manage_selection failed: {response.Error.Message}");
            }

            if (response.Result == null) return new ManageSelectionResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<ManageSelectionResult>(json) ?? new ManageSelectionResult();
        }

        public async Task<InspectElementGeometryResult> InspectElementGeometryAsync(InspectElementGeometryRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("inspect_element_geometry", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"inspect_element_geometry failed: {response.Error.Message}");
            }

            if (response.Result == null) return new InspectElementGeometryResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<InspectElementGeometryResult>(json) ?? new InspectElementGeometryResult();
        }

        public async Task<VisualOverrideResult> VisualOverrideAsync(VisualOverrideRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("visual_override", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"visual_override failed: {response.Error.Message}");
            }

            if (response.Result == null) return new VisualOverrideResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<VisualOverrideResult>(json) ?? new VisualOverrideResult();
        }

        public async Task<ExportScheduleResult> ExportScheduleAsync(ExportScheduleRequest request, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync("export_schedule", request, cancellationToken).ConfigureAwait(false);
            if (response.Error != null)
            {
                throw new InvalidOperationException($"export_schedule failed: {response.Error.Message}");
            }

            if (response.Result == null) return new ExportScheduleResult();
            string json = JsonSerializer.Serialize(response.Result);
            return JsonSerializer.Deserialize<ExportScheduleResult>(json) ?? new ExportScheduleResult();
        }

        public void Dispose()
        {
            _pipeClient?.Dispose();
        }
    }
}
