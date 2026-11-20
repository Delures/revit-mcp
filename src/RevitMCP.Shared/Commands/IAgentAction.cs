namespace RevitMCP.Shared.Commands
{
    /// <summary>
    /// Contract for requests that can be described in human-readable terms in the user's natural language.
    /// </summary>
    public interface IAgentAction
    {
        /// <summary>
        /// Human-readable summary of the action in the user's conversation language for the Revit dockable activity pane.
        /// </summary>
        string? ActionSummary { get; set; }

        /// <summary>
        /// Optional name of the calling agent (e.g. "MCP", "Antigravity", "Structural AI").
        /// </summary>
        string? AgentName { get; set; }
    }

    /// <summary>
    /// Contract for requests that modify the Revit model inside a Transaction affecting the Undo stack.
    /// </summary>
    public interface IAgentTransactionAction : IAgentAction
    {
        /// <summary>
        /// Human-readable transaction name in the user's conversation language for the Revit Undo stack.
        /// </summary>
        string? TransactionName { get; set; }
    }
}
