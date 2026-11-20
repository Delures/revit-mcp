using System.Collections.Generic;

namespace RevitMCP.Shared.Commands
{
    public class InspectElementGeometryRequest : IAgentAction
    {
        public int ElementId { get; set; }
        public bool IncludeFaces { get; set; } = false;
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
    }

    public class Point3DMm
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3DMm() { }
        public Point3DMm(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class BoundingBoxMm
    {
        public Point3DMm Min { get; set; } = new Point3DMm();
        public Point3DMm Max { get; set; } = new Point3DMm();
        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
        public double HeightMm { get; set; }
    }

    public class FaceGeometryInfo
    {
        public string? FaceType { get; set; }
        public Point3DMm? Normal { get; set; }
        public double AreaM2 { get; set; }
        public List<Point3DMm> OuterLoopMm { get; set; } = new List<Point3DMm>();
    }

    public class InspectElementGeometryResult
    {
        public bool Success { get; set; }
        public int ElementId { get; set; }
        public string? CategoryName { get; set; }
        public string? ElementName { get; set; }
        public string? GeometryType { get; set; } // "slab", "wall", "beam", "column", "generic_solid"
        public double? ThicknessMm { get; set; }
        public double? LengthMm { get; set; }
        public double? HeightMm { get; set; }
        public double? VolumeM3 { get; set; }
        public Point3DMm? CentroidMm { get; set; }
        public BoundingBoxMm? BoundingBoxMm { get; set; }
        public Point3DMm? LocationPointMm { get; set; }
        public Point3DMm? StartPointMm { get; set; }
        public Point3DMm? EndPointMm { get; set; }
        public Point3DMm? Orientation { get; set; }
        public List<Point3DMm>? BoundaryPointsMm { get; set; }
        public List<List<Point3DMm>>? HolesMm { get; set; }
        public List<FaceGeometryInfo>? Faces { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
