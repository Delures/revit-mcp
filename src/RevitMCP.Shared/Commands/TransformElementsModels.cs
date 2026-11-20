using System.Collections.Generic;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Shared.Commands
{
    public class TransformPoint3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public TransformPoint3D() { }
        public TransformPoint3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class TransformElementsRequest : IAgentTransactionAction
    {
        public string Action { get; set; } = "move"; // "move", "rotate", "copy", "delete"
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
        public string? TransactionName { get; set; }
        public List<long> ElementIds { get; set; } = new List<long>();

        // Displacement in millimeters (for move and copy)
        public double Dx { get; set; }
        public double Dy { get; set; }
        public double Dz { get; set; }

        // Rotation in degrees
        public double AngleDegrees { get; set; }
        public TransformPoint3D? AxisOrigin { get; set; } // default: center or (0, 0, 0)
        public TransformPoint3D? AxisDirection { get; set; } // default: (0, 0, 1) vertical Z axis
    }

    public class TransformElementsResult
    {
        public bool Success { get; set; } = true;
        public string Action { get; set; } = string.Empty;
        public List<long> AffectedElementIds { get; set; } = new List<long>();
        public List<long> NewElementIds { get; set; } = new List<long>();
        public int Count { get; set; }
        public List<WarningRecord>? Warnings { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
