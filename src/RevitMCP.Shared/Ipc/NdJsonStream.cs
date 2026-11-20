using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RevitMCP.Shared.Ipc
{
    public static class NamedPipeUtils
    {
        public static string PipePrefix => McpStorageContext.PipePrefix;

        public static string GetPipeName(string revitYear, int processId)
        {
            return $"{PipePrefix}{revitYear}-{processId}";
        }

        public static string GetFullPipePath(string pipeName)
        {
            return $@"\\.\pipe\{pipeName}";
        }
    }

    public class NdJsonStreamReader
    {
        private readonly StreamReader _reader;
        private readonly JsonSerializerOptions _jsonOptions;

        public NdJsonStreamReader(Stream stream, JsonSerializerOptions? options = null)
        {
            _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
            _jsonOptions = options ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<T?> ReadMessageAsync<T>(CancellationToken cancellationToken = default)
        {
            string? line;
#if NET8_0_OR_GREATER
            line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#else
            line = await _reader.ReadLineAsync().ConfigureAwait(false);
#endif
            if (line == null)
            {
                return default;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(line, _jsonOptions);
        }

        public async Task<string?> ReadRawLineAsync(CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            return await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#else
            return await _reader.ReadLineAsync().ConfigureAwait(false);
#endif
        }
    }

    public class NdJsonStreamWriter
    {
        private readonly StreamWriter _writer;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        public NdJsonStreamWriter(Stream stream, JsonSerializerOptions? options = null)
        {
            _writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 8192, leaveOpen: true);
            _jsonOptions = options ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task WriteMessageAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            string json = JsonSerializer.Serialize(message, _jsonOptions);
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
#if NET8_0_OR_GREATER
                await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
                await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
                await _writer.WriteLineAsync(json).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
#endif
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
