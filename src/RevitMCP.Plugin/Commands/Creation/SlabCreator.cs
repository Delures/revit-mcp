using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands.Creation
{
    public static class SlabCreator
    {
        public static (long id, string summary) CreateSlab(Document doc, CreateSlabSpec spec)
        {
            if (spec.BoundaryPoints == null || spec.BoundaryPoints.Count < 3)
            {
                throw new InvalidOperationException("Slab boundary must have at least 3 points.");
            }

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
                throw new InvalidOperationException("No valid Level found in the project for slab.");
            }

            FloorType? floorType = null;
            if (spec.FloorTypeId.HasValue && spec.FloorTypeId.Value > 0)
            {
                floorType = doc.GetElement(ElementIdExtensions.FromLongId(spec.FloorTypeId.Value)) as FloorType;
            }
            if (floorType == null)
            {
                floorType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .FirstOrDefault(ft => !ft.IsFoundationSlab) ??
                    new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().FirstOrDefault();
            }
            if (floorType == null)
            {
                throw new InvalidOperationException("No valid FloorType found in the project.");
            }

            var profileLoops = new List<CurveLoop>();
            var outerLoop = new CurveLoop();
            var pts = spec.BoundaryPoints
                .Select(p => new XYZ(
                    RevitUnitConverter.MmToFeet(p.X),
                    RevitUnitConverter.MmToFeet(p.Y),
                    RevitUnitConverter.MmToFeet(p.Z)))
                .ToList();

            for (int i = 0; i < pts.Count; i++)
            {
                var pStart = pts[i];
                var pEnd = pts[(i + 1) % pts.Count];
                if (pStart.DistanceTo(pEnd) > 1e-5)
                {
                    outerLoop.Append(Line.CreateBound(pStart, pEnd));
                }
            }
            profileLoops.Add(outerLoop);

            if (spec.Holes != null)
            {
                foreach (var holeLoopPts in spec.Holes)
                {
                    if (holeLoopPts != null && holeLoopPts.Count >= 3)
                    {
                        var holeLoop = new CurveLoop();
                        var hPts = holeLoopPts
                            .Select(p => new XYZ(
                                RevitUnitConverter.MmToFeet(p.X),
                                RevitUnitConverter.MmToFeet(p.Y),
                                RevitUnitConverter.MmToFeet(p.Z)))
                            .ToList();
                        for (int i = 0; i < hPts.Count; i++)
                        {
                            var pStart = hPts[i];
                            var pEnd = hPts[(i + 1) % hPts.Count];
                            if (pStart.DistanceTo(pEnd) > 1e-5)
                            {
                                holeLoop.Append(Line.CreateBound(pStart, pEnd));
                            }
                        }
                        profileLoops.Add(holeLoop);
                    }
                }
            }

            var floor = FloorCreator.CreateFloor(doc, profileLoops, floorType.Id, level.Id, spec.IsStructural);

            if (Math.Abs(spec.HeightOffset) > 1e-5)
            {
                var offsetParam = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                if (offsetParam != null && !offsetParam.IsReadOnly)
                {
                    offsetParam.Set(RevitUnitConverter.MmToFeet(spec.HeightOffset));
                }
            }

            var idVal = floor.Id.ToLongId();
            return (idVal, $"Slab created with type '{floorType.Name}' on level '{level.Name}' (ID: {idVal}).");
        }
    }
}
