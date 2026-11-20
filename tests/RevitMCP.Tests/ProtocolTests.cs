using System.Text.Json;
using RevitMCP.Shared.Protocol;
using Xunit;

namespace RevitMCP.Tests
{
    public class ProtocolTests
    {
        [Fact]
        public void JsonRpcResponse_SuccessSerializesCorrectly()
        {
            var resp = JsonRpcResponse.Success(10, new { status = "ok" });
            string json = JsonSerializer.Serialize(resp);

            Assert.Contains("\"jsonrpc\":\"2.0\"", json);
            Assert.Contains("\"id\":10", json);
            Assert.Contains("\"result\":", json);
            Assert.DoesNotContain("\"error\":", json);
        }

        [Fact]
        public void JsonRpcResponse_ErrorSerializesCorrectly()
        {
            var resp = JsonRpcResponse.CreateError(10, JsonRpcErrorCodes.RevitUiBusy, "Revit is in sketch mode");
            string json = JsonSerializer.Serialize(resp);

            Assert.Contains("\"code\":-32001", json);
            Assert.Contains("Revit is in sketch mode", json);
            Assert.DoesNotContain("\"result\":", json);
        }
    }
}
