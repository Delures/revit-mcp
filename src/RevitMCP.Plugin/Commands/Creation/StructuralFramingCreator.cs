using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands.Creation
{
    public static class StructuralFramingCreator
    {
        public static (long id, string summary) CreateBeam(Document doc, CreateBeamSpec spec)
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
                throw new InvalidOperationException("No valid Level found in project for beam.");
            }

            FamilySymbol? symbol = null;
            if (spec.FamilySymbolId.HasValue && spec.FamilySymbolId.Value > 0)
            {
                symbol = doc.GetElement(ElementIdExtensions.FromLongId(spec.FamilySymbolId.Value)) as FamilySymbol;
            }
            else
            {
                var framingQuery = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .Cast<FamilySymbol>();

                if (!string.IsNullOrEmpty(spec.FamilyName))
                {
                    framingQuery = framingQuery.Where(s => string.Equals(s.FamilyName, spec.FamilyName, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrEmpty(spec.TypeName))
                {
                    framingQuery = framingQuery.Where(s => string.Equals(s.Name, spec.TypeName, StringComparison.OrdinalIgnoreCase));
                }

                symbol = framingQuery.FirstOrDefault() ??
                         new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming).Cast<FamilySymbol>().FirstOrDefault();
            }

            if (symbol == null)
            {
                throw new InvalidOperationException("No Structural Framing (Beam) family symbols found in the project. Please load a beam family first.");
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            var p1 = new XYZ(
                RevitUnitConverter.MmToFeet(spec.StartPoint.X),
                RevitUnitConverter.MmToFeet(spec.StartPoint.Y),
                RevitUnitConverter.MmToFeet(spec.StartPoint.Z));
            var p2 = new XYZ(
                RevitUnitConverter.MmToFeet(spec.EndPoint.X),
                RevitUnitConverter.MmToFeet(spec.EndPoint.Y),
                RevitUnitConverter.MmToFeet(spec.EndPoint.Z));

            if (p1.DistanceTo(p2) < 1e-5)
            {
                throw new InvalidOperationException("Beam start and end points cannot be coincident.");
            }

            var line = Line.CreateBound(p1, p2);
            var beamInstance = doc.Create.NewFamilyInstance(line, symbol, level, StructuralType.Beam);

            var idVal = beamInstance.Id.ToLongId();
            return (idVal, $"Beam '{symbol.FamilyName}: {symbol.Name}' created on level '{level.Name}' (ID: {idVal}).");
        }

        public static (long id, string summary) CreateColumn(Document doc, CreateColumnSpec spec)
        {
            Level? baseLevel = null;
            if (spec.BaseLevelId > 0)
            {
                baseLevel = doc.GetElement(ElementIdExtensions.FromLongId(spec.BaseLevelId)) as Level;
            }
            if (baseLevel == null)
            {
                baseLevel = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .FirstOrDefault();
            }
            if (baseLevel == null)
            {
                throw new InvalidOperationException("No valid Level found in project for column.");
            }

            FamilySymbol? symbol = null;
            if (spec.FamilySymbolId.HasValue && spec.FamilySymbolId.Value > 0)
            {
                symbol = doc.GetElement(ElementIdExtensions.FromLongId(spec.FamilySymbolId.Value)) as FamilySymbol;
            }
            else
            {
                var colQuery = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .Cast<FamilySymbol>();

                if (!string.IsNullOrEmpty(spec.FamilyName))
                {
                    colQuery = colQuery.Where(s => string.Equals(s.FamilyName, spec.FamilyName, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrEmpty(spec.TypeName))
                {
                    colQuery = colQuery.Where(s => string.Equals(s.Name, spec.TypeName, StringComparison.OrdinalIgnoreCase));
                }

                symbol = colQuery.FirstOrDefault() ??
                         new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralColumns).Cast<FamilySymbol>().FirstOrDefault();
            }

            if (symbol == null)
            {
                throw new InvalidOperationException("No Structural Column family symbols found in the project. Please load a column family first.");
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            var loc = new XYZ(
                RevitUnitConverter.MmToFeet(spec.Location.X),
                RevitUnitConverter.MmToFeet(spec.Location.Y),
                RevitUnitConverter.MmToFeet(spec.Location.Z));

            var colInstance = doc.Create.NewFamilyInstance(loc, symbol, baseLevel, StructuralType.Column);

            if (spec.TopLevelId.HasValue && spec.TopLevelId.Value > 0)
            {
                var topParam = colInstance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                if (topParam != null && !topParam.IsReadOnly)
                {
                    topParam.Set(ElementIdExtensions.FromLongId(spec.TopLevelId.Value));
                }
            }

            if (Math.Abs(spec.BaseOffset) > 1e-5)
            {
                var baseOffsetParam = colInstance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                if (baseOffsetParam != null && !baseOffsetParam.IsReadOnly)
                {
                    baseOffsetParam.Set(RevitUnitConverter.MmToFeet(spec.BaseOffset));
                }
            }
            if (Math.Abs(spec.TopOffset) > 1e-5)
            {
                var topOffsetParam = colInstance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                {
                    topOffsetParam.Set(RevitUnitConverter.MmToFeet(spec.TopOffset));
                }
            }

            if (Math.Abs(spec.RotationDegrees) > 1e-5)
            {
                var axis = Line.CreateBound(loc, loc + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, colInstance.Id, axis, RevitUnitConverter.DegreesToRadians(spec.RotationDegrees));
            }

            var idVal = colInstance.Id.ToLongId();
            return (idVal, $"Column '{symbol.FamilyName}: {symbol.Name}' created on base level '{baseLevel.Name}' (ID: {idVal}).");
        }
    }
}
