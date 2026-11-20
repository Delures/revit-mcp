using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using RevitMCP.Shared.Commands;

namespace RevitMCP.Shared.Scripting
{
    public class ExecuteScriptRequest : IAgentTransactionAction
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public Dictionary<string, object>? Arguments { get; set; }

        [JsonPropertyName("actionSummary")]
        public string? ActionSummary { get; set; }

        [JsonPropertyName("agentName")]
        public string? AgentName { get; set; }

        [JsonPropertyName("transactionName")]
        public string? TransactionName { get; set; }
    }

    public class WarningRecord
    {
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "Warning";

        [JsonPropertyName("definitionId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DefinitionId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("elementIds")]
        public List<long> ElementIds { get; set; } = new List<long>();

        [JsonPropertyName("actionTaken")]
        public string ActionTaken { get; set; } = "AutoSuppressed";
    }

    public class CapturedDialogRecord
    {
        [JsonPropertyName("dialogId")]
        public string DialogId { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("actionTaken")]
        public string ActionTaken { get; set; } = "ClickedOk";

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ExecuteScriptResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("returnValue")]
        public object? ReturnValue { get; set; }

        [JsonPropertyName("consoleOutput")]
        public string? ConsoleOutput { get; set; }

        [JsonPropertyName("executionTimeMs")]
        public long ExecutionTimeMs { get; set; }

        [JsonPropertyName("errorCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("errorMessage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("affectedElementIds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<long>? AffectedElementIds { get; set; }

        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<WarningRecord>? Warnings { get; set; }

        [JsonPropertyName("capturedDialogs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<CapturedDialogRecord>? CapturedDialogs { get; set; }
    }
}
