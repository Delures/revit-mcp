using System;
using System.IO;
using System.Threading.Tasks;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;
using Xunit;

namespace RevitMCP.Tests
{
    public class IpcTests
    {
        [Fact]
        public async Task NdJsonStream_CanReadAndWriteMessagesLargerThan8KB()
        {
            using var ms = new MemoryStream();
            var writer = new NdJsonStreamWriter(ms);

            // Create a payload larger than 64 KB
            string largeData = new string('A', 70000);
            var req = new JsonRpcRequest
            {
                Id = 1,
                Method = "test_large",
                Params = largeData
            };

            await writer.WriteMessageAsync(req);

            ms.Seek(0, SeekOrigin.Begin);
            var reader = new NdJsonStreamReader(ms);

            var readBack = await reader.ReadMessageAsync<JsonRpcRequest>();
            Assert.NotNull(readBack);
            Assert.Equal("test_large", readBack!.Method);
            Assert.Equal(largeData, readBack.Params?.ToString());
        }

        [Fact]
        public async Task NdJsonStream_CanHandleMultipleMessagesSequentially()
        {
            using var ms = new MemoryStream();
            var writer = new NdJsonStreamWriter(ms);

            for (int i = 0; i < 5; i++)
            {
                await writer.WriteMessageAsync(new JsonRpcRequest
                {
                    Id = i,
                    Method = $"method_{i}"
                });
            }

            ms.Seek(0, SeekOrigin.Begin);
            var reader = new NdJsonStreamReader(ms);

            for (int i = 0; i < 5; i++)
            {
                var msg = await reader.ReadMessageAsync<JsonRpcRequest>();
                Assert.NotNull(msg);
                Assert.Equal($"method_{i}", msg!.Method);
            }
        }
    }
}
