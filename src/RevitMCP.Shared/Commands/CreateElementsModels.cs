using System.Collections.Generic;
using RevitMCP.Shared.Scripting;

namespace RevitMCP.Shared.Commands
{
    public class CreateLevelSpec
    {
        public string Name { get; set; } = string.Empty;
        public double Elevation { get; set; } // in mm
    }

    public class CreateGridSpec
    {
        public string Name { get; set; } = string.Empty;
        public TransformPoint3D StartPoint { get; set; } = new TransformPoint3D(); // in mm
        public TransformPoint3D EndPoint { get; set; } = new TransformPoint3D(); // in mm
    }

    public class CreateWallSpec
    {
        public TransformPoint3D StartPoint { get; set; } = new TransformPoint3D(); // in mm
        public TransformPoint3D EndPoint { get; set; } = new TransformPoint3D(); // in mm
        public long LevelId { get; set; }
        public long? WallTypeId { get; set; }
        public double Height { get; set; } = 3000; // in mm
        public double Offset { get; set; } = 0; // in mm
        public bool IsStructural { get; set; } = false;
    }

    public class CreateFamilyInstanceSpec
    {
        public long? FamilySymbolId { get; set; }
        public string? FamilyName { get; set; }
        public string? TypeName { get; set; }
        public TransformPoint3D Location { get; set; } = new TransformPoint3D(); // in mm
        public TransformPoint3D? StartPoint { get; set; } // for linear/curve-based families in mm
        public TransformPoint3D? EndPoint { get; set; } // for linear/curve-based families in mm
        public TransformPoint3D? ReferenceDirection { get; set; } // orientation vector (e.g. { x: 1, y: 0, z: 0 })
        public long? LevelId { get; set; }
        public long? HostElementId { get; set; }
        public long? ViewId { get; set; }
        public string StructuralType { get; set; } = "NonStructural"; // NonStructural, Beam, Column, Bracing, Footing, Unknown
        public double RotationDegrees { get; set; } = 0;
    }

    public class CreateRoomSpec
    {
        public long LevelId { get; set; }
        public TransformPoint3D Point { get; set; } = new TransformPoint3D(); // in mm
        public string? Name { get; set; }
        public string? Number { get; set; }
    }

    public class CreateModelCurveSpec
    {
        public TransformPoint3D StartPoint { get; set; } = new TransformPoint3D(); // in mm
        public TransformPoint3D EndPoint { get; set; } = new TransformPoint3D(); // in mm
    }

    public class CreateSlabSpec
    {
        public List<TransformPoint3D> BoundaryPoints { get; set; } = new List<TransformPoint3D>(); // in mm
        public List<List<TransformPoint3D>>? Holes { get; set; } // optional inner opening loops in mm
        public long LevelId { get; set; }
        public long? FloorTypeId { get; set; }
        public double HeightOffset { get; set; } = 0; // in mm
        public bool IsStructural { get; set; } = true;
    }

    public class CreateBeamSpec
    {
        public TransformPoint3D StartPoint { get; set; } = new TransformPoint3D(); // in mm
        public TransformPoint3D EndPoint { get; set; } = new TransformPoint3D(); // in mm
        public long LevelId { get; set; }
        public long? FamilySymbolId { get; set; }
        public string? FamilyName { get; set; }
        public string? TypeName { get; set; }
    }

    public class CreateColumnSpec
    {
        public TransformPoint3D Location { get; set; } = new TransformPoint3D(); // in mm
        public long BaseLevelId { get; set; }
        public long? TopLevelId { get; set; }
        public double BaseOffset { get; set; } = 0; // in mm
        public double TopOffset { get; set; } = 0; // in mm
        public double RotationDegrees { get; set; } = 0;
        public long? FamilySymbolId { get; set; }
        public string? FamilyName { get; set; }
        public string? TypeName { get; set; }
    }

    public class CreateAreaReinforcementSpec
    {
        public long HostElementId { get; set; }
        public TransformPoint3D MajorDirection { get; set; } = new TransformPoint3D { X = 1, Y = 0, Z = 0 };
        public List<TransformPoint3D>? BoundaryCurves { get; set; } // optional custom boundary vertices in mm
        public long? AreaReinforcementTypeId { get; set; }
        public long? TopMajorBarTypeId { get; set; }
        public long? TopMinorBarTypeId { get; set; }
        public long? BottomMajorBarTypeId { get; set; }
        public long? BottomMinorBarTypeId { get; set; }
        public double TopMajorSpacing { get; set; } = 200; // in mm
        public double TopMinorSpacing { get; set; } = 200; // in mm
        public double BottomMajorSpacing { get; set; } = 200; // in mm
        public double BottomMinorSpacing { get; set; } = 200; // in mm
    }

    public class CreateRebarSpec
    {
        public long HostElementId { get; set; }
        public long? BarTypeId { get; set; }
        public List<List<TransformPoint3D>> CurveSegments { get; set; } = new List<List<TransformPoint3D>>(); // line segments in mm
        public TransformPoint3D Normal { get; set; } = new TransformPoint3D { X = 0, Y = 0, Z = 1 };
        public string LayoutRule { get; set; } = "Single"; // Single, MaximumSpacing, FixedNumber, NumberWithSpacing
        public double Spacing { get; set; } = 200; // in mm
        public int Quantity { get; set; } = 1;
        public double ArrayLength { get; set; } = 0; // in mm
    }

    public class CreateElementsRequest : IAgentTransactionAction
    {
        public string Action { get; set; } = "wall"; // "level", "grid", "wall", "family_instance", "room", "model_curve", "slab", "beam", "column", "area_reinforcement", "rebar"
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
        public string? TransactionName { get; set; }
        public CreateLevelSpec? Level { get; set; }
        public CreateGridSpec? Grid { get; set; }
        public CreateWallSpec? Wall { get; set; }
        public CreateFamilyInstanceSpec? FamilyInstance { get; set; }
        public CreateRoomSpec? Room { get; set; }
        public CreateModelCurveSpec? ModelCurve { get; set; }
        public CreateSlabSpec? Slab { get; set; }
        public CreateBeamSpec? Beam { get; set; }
        public CreateColumnSpec? Column { get; set; }
        public CreateAreaReinforcementSpec? AreaReinforcement { get; set; }
        public CreateRebarSpec? Rebar { get; set; }
    }

    public class CreateElementsResult
    {
        public bool Success { get; set; } = true;
        public string Action { get; set; } = string.Empty;
        public List<long> CreatedElementIds { get; set; } = new List<long>();
        public string? ElementSummary { get; set; }
        public List<WarningRecord>? Warnings { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
