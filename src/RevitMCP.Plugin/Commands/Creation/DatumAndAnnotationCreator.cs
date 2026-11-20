using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands.Creation
{
    public static class DatumAndAnnotationCreator
    {
        public static (long id, string summary) CreateLevel(Document doc, CreateLevelSpec spec)
        {
            var elevFeet = RevitUnitConverter.MmToFeet(spec.Elevation);
            var level = Level.Create(doc, elevFeet);
            if (!string.IsNullOrEmpty(spec.Name))
            {
                level.Name = spec.Name;
            }

            var idVal = level.Id.ToLongId();
            return (idVal, $"Level '{level.Name}' created at elevation {spec.Elevation} mm (ID: {idVal}).");
        }

        public static (long id, string summary) CreateGrid(Document doc, CreateGridSpec spec)
        {
            var p1 = new XYZ(
                RevitUnitConverter.MmToFeet(spec.StartPoint.X),
                RevitUnitConverter.MmToFeet(spec.StartPoint.Y),
                0);
            var p2 = new XYZ(
                RevitUnitConverter.MmToFeet(spec.EndPoint.X),
                RevitUnitConverter.MmToFeet(spec.EndPoint.Y),
                0);

            var line = Line.CreateBound(p1, p2);
            var grid = Grid.Create(doc, line);
            if (!string.IsNullOrEmpty(spec.Name))
            {
                grid.Name = spec.Name;
            }

            var idVal = grid.Id.ToLongId();
            return (idVal, $"Grid '{grid.Name}' created (ID: {idVal}).");
        }

        public static (long id, string summary) CreateRoom(Document doc, CreateRoomSpec spec)
        {
            Level? level = null;
            if (spec.LevelId > 0)
            {
                level = doc.GetElement(ElementIdExtensions.FromLongId(spec.LevelId)) as Level;
            }

            if (level == null)
            {
                level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .FirstOrDefault();
            }

            if (level == null)
            {
                throw new InvalidOperationException("No valid Level found in the project for room.");
            }

            var uv = new UV(
                RevitUnitConverter.MmToFeet(spec.Point.X),
                RevitUnitConverter.MmToFeet(spec.Point.Y));

            var room = doc.Create.NewRoom(level, uv);
            if (room == null)
            {
                throw new InvalidOperationException("Revit failed to create Room at the specified coordinates (unbounded area).");
            }

            if (!string.IsNullOrEmpty(spec.Name))
            {
                room.Name = spec.Name;
            }

            if (!string.IsNullOrEmpty(spec.Number))
            {
                room.Number = spec.Number;
            }

            var idVal = room.Id.ToLongId();
            return (idVal, $"Room '{room.Name}' (#{room.Number}) created on level '{level.Name}' (ID: {idVal}).");
        }

        public static (long id, string summary) CreateModelCurve(Document doc, CreateModelCurveSpec spec)
        {
            var p1 = new XYZ(
                RevitUnitConverter.MmToFeet(spec.StartPoint.X),
                RevitUnitConverter.MmToFeet(spec.StartPoint.Y),
                RevitUnitConverter.MmToFeet(spec.StartPoint.Z));
            var p2 = new XYZ(
                RevitUnitConverter.MmToFeet(spec.EndPoint.X),
                RevitUnitConverter.MmToFeet(spec.EndPoint.Y),
                RevitUnitConverter.MmToFeet(spec.EndPoint.Z));

            var line = Line.CreateBound(p1, p2);

            Plane plane;
            if (Math.Abs(p1.Z - p2.Z) < 1e-6)
            {
                plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, p1.Z));
            }
            else
            {
                var diff = (p2 - p1).Normalize();
                var normal = diff.CrossProduct(XYZ.BasisZ);
                if (normal.IsAlmostEqualTo(XYZ.Zero))
                {
                    normal = diff.CrossProduct(XYZ.BasisX);
                }
                plane = Plane.CreateByNormalAndOrigin(normal.Normalize(), p1);
            }

            var sketchPlane = SketchPlane.Create(doc, plane);
            var modelCurve = doc.Create.NewModelCurve(line, sketchPlane);

            var idVal = modelCurve.Id.ToLongId();
            return (idVal, $"ModelCurve created (ID: {idVal}).");
        }
    }
}
