using System;

namespace RevitMCP.Shared.Utils
{
    /// <summary>
    /// Resolves human-friendly names for Revit transactions (the Undo stack)
    /// and for activity cards in the side pane, in the user's conversation language.
    /// If the AI passes a description (English, German, Russian, etc.), it takes priority.
    /// Otherwise a careful default fallback is used.
    /// </summary>
    public static class HumanFriendlyNaming
    {
        /// <summary>
        /// Resolves the transaction name for the Revit Undo stack.
        /// If the AI passed a transactionName in the user's language, it is used with the "MCP: " prefix.
        /// </summary>
        public static string ResolveTransactionName(string? aiTransactionName, string? action = null, string? category = null)
        {
            if (!string.IsNullOrWhiteSpace(aiTransactionName))
            {
                string trimmed = aiTransactionName!.Trim();
                if (trimmed.StartsWith("MCP:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("RevitMCP:", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed;
                }
                return $"MCP: {trimmed}";
            }

            return (category?.ToLowerInvariant(), action?.ToLowerInvariant()) switch
            {
                ("create", _) => GetCreateTransactionName(action),
                ("transform", _) => GetTransformTransactionName(action),
                ("visual_override", _) => GetVisualOverrideTransactionName(action),
                ("parameters", _) => GetParametersTransactionName(),
                _ => string.IsNullOrWhiteSpace(action) ? "MCP: Command" : $"MCP: {action}"
            };
        }

        /// <summary>
        /// Resolves the action description for the Revit activity side pane.
        /// If the AI passed an actionSummary in the user's language, it is used as is.
        /// </summary>
        public static string ResolveActivitySummary(string? aiSummary, string? action = null, string? category = null, long countOrId = 0)
        {
            if (!string.IsNullOrWhiteSpace(aiSummary))
            {
                return aiSummary!.Trim();
            }

            return (category?.ToLowerInvariant(), action?.ToLowerInvariant()) switch
            {
                ("create", _) => GetCreateActivitySummary(action),
                ("transform", _) => GetTransformActivitySummary(action, (int)countOrId),
                ("visual_override", _) => GetVisualOverrideActivitySummary(action, (int)countOrId),
                ("parameters", _) => GetParametersActivitySummary(action, (int)countOrId),
                ("inspect_element_geometry", _) => countOrId > 0 ? $"Inspect Geometry (ID {countOrId})" : "Inspect Geometry",
                ("check_intersections", _) => countOrId > 0 ? $"Check Intersections (ID {countOrId})" : "Check Intersections",
                ("inspect_schema", _) => string.IsNullOrWhiteSpace(action) ? "Inspect Schema" : $"Inspect Schema ({action})",
                ("query_elements", _) => string.IsNullOrWhiteSpace(action) ? "Query Elements" : $"Query Elements ({action})",
                ("manage_selection", _) => countOrId > 0 ? $"Manage Selection ({countOrId} items)" : "Manage Selection",
                ("manage_documents", _) => string.IsNullOrWhiteSpace(action) ? "Manage Document" : $"Manage Document ({action})",
                ("export_view_image", _) => "Export View Image",
                ("export_schedule", _) => string.IsNullOrWhiteSpace(action) ? "Export Schedule" : $"Export Schedule ({action})",
                _ => string.IsNullOrWhiteSpace(action) ? "Execute Command" : $"Execute ({action})"
            };
        }

        /// <summary>
        /// Resolves the agent name for the activity card (defaults to "MCP").
        /// </summary>
        public static string ResolveAgentName(string? aiAgentName)
        {
            return !string.IsNullOrWhiteSpace(aiAgentName) ? aiAgentName!.Trim() : "MCP";
        }

        /// <summary>
        /// Short element creation name for the activity pane (fallback).
        /// </summary>
        public static string GetCreateActivitySummary(string? action)
        {
            return action?.ToLowerInvariant() switch
            {
                "rebar" => "Creating rebar",
                "area_reinforcement" => "Area reinforcement",
                "wall" => "Creating wall",
                "slab" or "floor" => "Creating floor",
                "beam" => "Creating beam",
                "column" => "Creating column",
                "room" => "Creating room",
                "grid" => "Creating grid",
                "level" => "Creating level",
                "model_curve" => "Creating model line",
                "family_instance" => "Placing family instance",
                _ => string.IsNullOrWhiteSpace(action) ? "Creating element" : $"Creating element ({action})"
            };
        }

        /// <summary>
        /// Transaction name for the Revit Undo stack (fallback).
        /// </summary>
        public static string GetCreateTransactionName(string? action)
        {
            return $"MCP: {GetCreateActivitySummary(action)}";
        }

        /// <summary>
        /// Short transformation name for the activity pane (fallback).
        /// </summary>
        public static string GetTransformActivitySummary(string? action, int count)
        {
            string countStr = count > 0 ? $" ({count} items)" : string.Empty;
            return action?.ToLowerInvariant() switch
            {
                "delete" => $"Deleting{countStr}",
                "move" => $"Moving{countStr}",
                "rotate" => $"Rotating{countStr}",
                "mirror" => $"Mirroring{countStr}",
                "copy" => $"Copying{countStr}",
                _ => string.IsNullOrWhiteSpace(action) ? $"Transforming{countStr}" : $"Transforming ({action}){countStr}"
            };
        }

        /// <summary>
        /// Transformation transaction name for the Revit Undo stack (fallback).
        /// </summary>
        public static string GetTransformTransactionName(string? action)
        {
            return action?.ToLowerInvariant() switch
            {
                "delete" => "MCP: Delete Elements",
                "move" => "MCP: Move Elements",
                "rotate" => "MCP: Rotate Elements",
                "mirror" => "MCP: Mirror Elements",
                "copy" => "MCP: Copy Elements",
                _ => string.IsNullOrWhiteSpace(action) ? "MCP: Transform Elements" : $"MCP: Transform ({action})"
            };
        }

        /// <summary>
        /// Short graphics override name for the activity pane (fallback).
        /// </summary>
        public static string GetVisualOverrideActivitySummary(string? action, int count)
        {
            string countStr = count > 0 ? $" ({count} items)" : string.Empty;
            return action?.ToLowerInvariant() switch
            {
                "override_graphics" => $"Graphic override{countStr}",
                "set_rebar_presentation" => $"Rebar presentation{countStr}",
                "isolate" => $"Isolating elements{countStr}",
                "hide" => $"Hiding elements{countStr}",
                "reset" => $"Resetting graphics{countStr}",
                _ => string.IsNullOrWhiteSpace(action) ? $"View graphics{countStr}" : $"View graphics ({action}){countStr}"
            };
        }

        /// <summary>
        /// Graphics override transaction name for the Revit Undo stack (fallback).
        /// </summary>
        public static string GetVisualOverrideTransactionName(string? action)
        {
            return action?.ToLowerInvariant() switch
            {
                "override_graphics" => "MCP: Graphic Override",
                "set_rebar_presentation" => "MCP: Rebar Presentation",
                "isolate" => "MCP: Isolate Elements",
                "hide" => "MCP: Hide Elements",
                "reset" => "MCP: Reset Graphics",
                _ => string.IsNullOrWhiteSpace(action) ? "MCP: View Graphics" : $"MCP: View Graphics ({action})"
            };
        }

        /// <summary>
        /// Short parameter management name for the activity pane (fallback).
        /// </summary>
        public static string GetParametersActivitySummary(string? action, int count)
        {
            string countStr = count > 0 ? $" ({count} items)" : string.Empty;
            return action?.ToLowerInvariant() == "set"
                ? $"Writing parameters{countStr}"
                : $"Reading parameters{countStr}";
        }

        /// <summary>
        /// Parameter change transaction name for the Revit Undo stack (fallback).
        /// </summary>
        public static string GetParametersTransactionName()
        {
            return "MCP: Set Parameters";
        }
    }
}
