using System;
using System.Threading;
using System.Threading.Tasks;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Protocol;
using RevitMCP.Shared.Session;
using Xunit;

namespace RevitMCP.Tests
{
    public class JsonRpcDispatcherTests
    {
        private class TestHandler : IIpcHandler, IDisposable
        {
            public string Method { get; }
            public bool RequiresIdleUi { get; }
            public bool WasExecuted { get; private set; }
            public bool WasDisposed { get; private set; }
            public Func<JsonRpcRequest, JsonRpcResponse>? CustomExecution { get; set; }

            public TestHandler(string method, bool requiresIdleUi)
            {
                Method = method;
                RequiresIdleUi = requiresIdleUi;
            }

            public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
            {
                WasExecuted = true;
                if (CustomExecution != null)
                {
                    return Task.FromResult(CustomExecution(request));
                }
                return Task.FromResult(JsonRpcResponse.Success(request.Id, new { ok = true }));
            }

            public void Dispose()
            {
                WasDisposed = true;
            }
        }

        [Fact]
        public async Task DispatchAsync_NullOrEmptyMethod_ReturnsInvalidRequest()
        {
            var session = new RevitSessionInfo { UiState = RevitUiStates.Ready };
            var dispatcher = new JsonRpcDispatcher(() => session);

            var respNull = await dispatcher.DispatchAsync(new JsonRpcRequest { Method = null! });
            var respEmpty = await dispatcher.DispatchAsync(new JsonRpcRequest { Method = "   " });

            Assert.NotNull(respNull.Error);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, respNull.Error!.Code);

            Assert.NotNull(respEmpty.Error);
            Assert.Equal(JsonRpcErrorCodes.InvalidRequest, respEmpty.Error!.Code);
        }

        [Fact]
        public async Task DispatchAsync_UnknownMethod_ReturnsMethodNotFound()
        {
            var session = new RevitSessionInfo { UiState = RevitUiStates.Ready };
            var dispatcher = new JsonRpcDispatcher(() => session);

            var resp = await dispatcher.DispatchAsync(new JsonRpcRequest { Id = 1, Method = "non_existent_method" });

            Assert.NotNull(resp.Error);
            Assert.Equal(JsonRpcErrorCodes.MethodNotFound, resp.Error!.Code);
            Assert.Contains("Unknown method 'non_existent_method'", resp.Error.Message);
        }

        [Fact]
        public async Task DispatchAsync_ValidHandler_ExecutesAndReturnsSuccess()
        {
            var session = new RevitSessionInfo { UiState = RevitUiStates.Ready };
            var dispatcher = new JsonRpcDispatcher(() => session);
            var handler = new TestHandler("test_method", requiresIdleUi: false);
            dispatcher.Register(handler);

            var resp = await dispatcher.DispatchAsync(new JsonRpcRequest { Id = 42, Method = "test_method" });

            Assert.True(handler.WasExecuted);
            Assert.Null(resp.Error);
            Assert.NotNull(resp.Result);
        }

        [Fact]
        public async Task DispatchAsync_MethodMatching_IsCaseInsensitive()
        {
            var session = new RevitSessionInfo { UiState = RevitUiStates.Ready };
            var dispatcher = new JsonRpcDispatcher(() => session);
            var handler = new TestHandler("create_elements", requiresIdleUi: false);
            dispatcher.Register(handler);

            var resp = await dispatcher.DispatchAsync(new JsonRpcRequest { Id = "abc", Method = "CREATE_ELEMENTS" });

            Assert.True(handler.WasExecuted);
            Assert.Null(resp.Error);
        }

        [Theory]
        [InlineData(RevitUiStates.InSketchMode)]
        [InlineData(RevitUiStates.ModalActive)]
        public async Task DispatchAsync_WhenHandlerRequiresIdleUiAndUiIsBusy_BlocksExecution(string busyState)
        {
            var session = new RevitSessionInfo { UiState = busyState };
            var dispatcher = new JsonRpcDispatcher(() => session);
            var handler = new TestHandler("modify_slab", requiresIdleUi: true);
            dispatcher.Register(handler);

            var resp = await dispatcher.DispatchAsync(new JsonRpcRequest { Id = 10, Method = "modify_slab" });

            Assert.False(handler.WasExecuted);
            Assert.NotNull(resp.Error);
            Assert.Equal(JsonRpcErrorCodes.RevitUiBusy, resp.Error!.Code);
            Assert.Contains("Revit UI is busy", resp.Error.Message);
        }

        [Fact]
        public async Task DispatchAsync_WhenHandlerRequiresIdleUiAndUiIsReady_Executes()
        {
            var session = new RevitSessionInfo { UiState = RevitUiStates.Ready };
            var dispatcher = new JsonRpcDispatcher(() => session);
            var handler = new TestHandler("modify_slab", requiresIdleUi: true);
            dispatcher.Register(handler);

            var resp = await dispatcher.DispatchAsync(new JsonRpcRequest { Id = 10, Method = "modify_slab" });

            Assert.True(handler.WasExecuted);
            Assert.Null(resp.Error);
        }

        [Theory]
        [InlineData(RevitUiStates.InSketchMode)]
        [InlineData(RevitUiStates.ModalActive)]
        public async Task DispatchAsync_WhenHandlerDoesNotRequireIdleUi_ExecutesEvenWhenUiIsBusy(string busyState)
        {
            var session = new RevitSessionInfo { UiState = busyState };
            var dispatcher = new JsonRpcDispatcher(() => session);
            var handler = new TestHandler("ping", requiresIdleUi: false);
            dispatcher.Register(handler);

            var resp = await dispatcher.DispatchAsync(new JsonRpcRequest { Id = 1, Method = "ping" });

            Assert.True(handler.WasExecuted);
            Assert.Null(resp.Error);
        }

        [Fact]
        public async Task DispatchAsync_WhenHandlerThrows_ReturnsInternalError()
        {
            var session = new RevitSessionInfo { UiState = RevitUiStates.Ready };
            var dispatcher = new JsonRpcDispatcher(() => session);
            var handler = new TestHandler("throw_method", requiresIdleUi: false)
            {
                CustomExecution = _ => throw new InvalidOperationException("Simulated crash")
            };
            dispatcher.Register(handler);

            var resp = await dispatcher.DispatchAsync(new JsonRpcRequest { Id = 99, Method = "throw_method" });

            Assert.NotNull(resp.Error);
            Assert.Equal(JsonRpcErrorCodes.InternalError, resp.Error!.Code);
            Assert.Contains("Simulated crash", resp.Error.Message);
        }

        [Fact]
        public void Dispose_DisposesAllDisposableRegisteredHandlers()
        {
            var session = new RevitSessionInfo { UiState = RevitUiStates.Ready };
            var dispatcher = new JsonRpcDispatcher(() => session);
            var handler1 = new TestHandler("h1", false);
            var handler2 = new TestHandler("h2", true);

            dispatcher.Register(handler1);
            dispatcher.Register(handler2);

            Assert.False(handler1.WasDisposed);
            Assert.False(handler2.WasDisposed);

            dispatcher.Dispose();

            Assert.True(handler1.WasDisposed);
            Assert.True(handler2.WasDisposed);
            Assert.False(dispatcher.TryGetHandler("h1", out _));
        }
    }
}
