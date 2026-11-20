using System;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands.Creation
{
    public static class WallCreator
    {
        public static (long id, string summary) CreateWall(Document doc, CreateWallSpec spec)
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
                throw new InvalidOperationException("No valid Level found in the project to host the wall.");
            }

            WallType? wallType = null;
            if (spec.WallTypeId.HasValue && spec.WallTypeId.Value > 0)
            {
                wallType = doc.GetElement(ElementIdExtensions.FromLongId(spec.WallTypeId.Value)) as WallType;
            }

            if (wallType == null)
            {
                wallType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .FirstOrDefault(wt => wt.Kind == WallKind.Basic);
            }

            if (wallType == null)
            {
                throw new InvalidOperationException("No Basic WallType found in the project.");
            }

            var heightFeet = RevitUnitConverter.MmToFeet(spec.Height > 0 ? spec.Height : 3000);
            var offsetFeet = RevitUnitConverter.MmToFeet(spec.Offset);

            var wall = Wall.Create(doc, line, wallType.Id, level.Id, heightFeet, offsetFeet, false, spec.IsStructural);

            var idVal = wall.Id.ToLongId();
            return (idVal, $"Wall created with type '{wallType.Name}' on level '{level.Name}' (ID: {idVal}).");
        }
    }
}
