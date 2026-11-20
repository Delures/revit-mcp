using System;
using System.Collections.Generic;
using RevitMCP.Shared.Scripting;
using Xunit;

namespace RevitMCP.Tests
{
    public class MockScriptContext : IScriptExecutionContext
    {
        private readonly List<string> _logs = new List<string>();
        private readonly List<long> _affectedElementIds = new List<long>();
        private readonly List<WarningRecord> _warnings = new List<WarningRecord>();
        private readonly List<CapturedDialogRecord> _capturedDialogs = new List<CapturedDialogRecord>();

        public void Print(object? message)
        {
            _logs.Add(message?.ToString() ?? "null");
        }

        public void Log(object? message) => Print(message);

        public IReadOnlyList<string> GetLogs() => _logs;

        public void AddAffectedElements(IEnumerable<long> elementIds)
        {
            _affectedElementIds.AddRange(elementIds);
        }

        public IReadOnlyList<long> GetAffectedElementIds() => _affectedElementIds;

        public void AddWarning(WarningRecord warning) => _warnings.Add(warning);
        public IReadOnlyList<WarningRecord> GetWarnings() => _warnings;

        public void AddCapturedDialog(CapturedDialogRecord dialog) => _capturedDialogs.Add(dialog);
        public IReadOnlyList<CapturedDialogRecord> GetCapturedDialogs() => _capturedDialogs;
    }

    public class RoslynScriptEngineTests
    {
        [Fact]
        public void RoslynScriptEngine_ExecutesSimpleCodeAndReturnsValue()
        {
            var engine = new RoslynScriptEngine();
            var context = new MockScriptContext();

            string code = @"
                context.Print(""Hello from test"");
                return 42;
            ";

            var result = engine.Execute(code, context, null);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(42, Convert.ToInt32(result.ReturnValue));
            Assert.Contains("Hello from test", result.ConsoleOutput);
        }

        [Fact]
        public void RoslynScriptEngine_CanReceiveArguments()
        {
            var engine = new RoslynScriptEngine();
            var context = new MockScriptContext();

            var args = new Dictionary<string, object>
            {
                { "name", "BIM Engineer" }
            };

            string code = @"
                string name = (string)args[""name""];
                return $""Greeting: {name}"";
            ";

            var result = engine.Execute(code, context, args);

            Assert.True(result.Success);
            Assert.Equal("Greeting: BIM Engineer", result.ReturnValue?.ToString());
        }

        [Fact]
        public void RoslynScriptEngine_HandlesCompilationErrorsGracefully()
        {
            var engine = new RoslynScriptEngine();
            var context = new MockScriptContext();

            string invalidCode = @"
                This is totally invalid C# syntax !!!;
            ";

            var result = engine.Execute(invalidCode, context, null);

            Assert.False(result.Success);
            Assert.Equal("SCRIPT_COMPILATION_ERROR", result.ErrorCode);
            Assert.Contains("Compilation failed", result.ErrorMessage);
        }

        [Fact]
        public void RoslynScriptEngine_HandlesRuntimeExceptions()
        {
            var engine = new RoslynScriptEngine();
            var context = new MockScriptContext();

            string throwingCode = @"
                throw new InvalidOperationException(""Something broke in Revit!"");
            ";

            var result = engine.Execute(throwingCode, context, null);

            Assert.False(result.Success);
            Assert.Equal("SCRIPT_EXECUTION_ERROR", result.ErrorCode);
            Assert.Contains("Something broke in Revit!", result.ErrorMessage);
        }
    }
}
