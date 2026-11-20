using System.Collections.Generic;

namespace RevitMCP.Shared.Commands
{
    public class ColorRgb
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public ColorRgb() { }
        public ColorRgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    public class VisualOverrideRequest : IAgentTransactionAction
    {
        public string Action { get; set; } = "override_graphics"; // "override_graphics", "reset_graphics", "isolate", "hide", "unhide", "reset_isolate", "set_rebar_presentation"
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
        public string? TransactionName { get; set; }
        public List<int> ElementIds { get; set; } = new List<int>();
        public int? ViewId { get; set; }
        public int? Transparency { get; set; } // 0..100
        public ColorRgb? Color { get; set; }
        public bool? Halftone { get; set; }
        public bool? SolidIn3D { get; set; }
        public bool? UnobscuredInView { get; set; }
    }

    public class VisualOverrideResult
    {
        public bool Success { get; set; }
        public string Action { get; set; } = "override_graphics";
        public List<int> AffectedElementIds { get; set; } = new List<int>();
        public int ViewId { get; set; }
        public string? ViewName { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
