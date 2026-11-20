using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands.Creation
{
    public static class ReinforcementCreator
    {
        public static (long id, string summary) CreateAreaReinforcement(Document doc, CreateAreaReinforcementSpec spec)
        {
            var hostElem = doc.GetElement(ElementIdExtensions.FromLongId(spec.HostElementId));
            if (hostElem == null)
            {
                throw new InvalidOperationException($"Host element with ID {spec.HostElementId} not found.");
            }

            var curves = new List<Curve>();
            if (spec.BoundaryCurves != null && spec.BoundaryCurves.Count >= 3)
            {
                var bPts = spec.BoundaryCurves
                    .Select(p => new XYZ(
                        RevitUnitConverter.MmToFeet(p.X),
                        RevitUnitConverter.MmToFeet(p.Y),
                        RevitUnitConverter.MmToFeet(p.Z)))
                    .ToList();

                for (int i = 0; i < bPts.Count; i++)
                {
                    var pStart = bPts[i];
                    var pEnd = bPts[(i + 1) % bPts.Count];
                    if (pStart.DistanceTo(pEnd) > 1e-5)
                    {
                        curves.Add(Line.CreateBound(pStart, pEnd));
                    }
                }
            }
            else
            {
                var bbox = hostElem.get_BoundingBox(null);
                if (bbox == null)
                {
                    throw new InvalidOperationException("Host element has no geometry/BoundingBox to infer area reinforcement boundary.");
                }
                double z = bbox.Max.Z;
                var p0 = new XYZ(bbox.Min.X, bbox.Min.Y, z);
                var p1 = new XYZ(bbox.Max.X, bbox.Min.Y, z);
                var p2 = new XYZ(bbox.Max.X, bbox.Max.Y, z);
                var p3 = new XYZ(bbox.Min.X, bbox.Max.Y, z);
                curves.Add(Line.CreateBound(p0, p1));
                curves.Add(Line.CreateBound(p1, p2));
                curves.Add(Line.CreateBound(p2, p3));
                curves.Add(Line.CreateBound(p3, p0));
            }

            var majorDir = new XYZ(
                spec.MajorDirection.X,
                spec.MajorDirection.Y,
                spec.MajorDirection.Z);
            if (majorDir.IsAlmostEqualTo(XYZ.Zero))
            {
                majorDir = XYZ.BasisX;
            }
            majorDir = majorDir.Normalize();

            ElementId areaTypeId = ElementId.InvalidElementId;
            if (spec.AreaReinforcementTypeId.HasValue && spec.AreaReinforcementTypeId.Value > 0)
            {
                areaTypeId = ElementIdExtensions.FromLongId(spec.AreaReinforcementTypeId.Value);
            }
            else
            {
                var defaultTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.AreaReinforcementType);
                if (defaultTypeId != ElementId.InvalidElementId)
                {
                    areaTypeId = defaultTypeId;
                }
                else
                {
                    var firstAreaType = new FilteredElementCollector(doc)
                        .OfClass(typeof(AreaReinforcementType))
                        .FirstOrDefault();
                    if (firstAreaType != null)
                    {
                        areaTypeId = firstAreaType.Id;
                    }
                }
            }

            ElementId barTypeId = ElementId.InvalidElementId;
            if (spec.TopMajorBarTypeId.HasValue && spec.TopMajorBarTypeId.Value > 0)
            {
                barTypeId = ElementIdExtensions.FromLongId(spec.TopMajorBarTypeId.Value);
            }
            else
            {
                var firstBarType = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .FirstOrDefault();
                if (firstBarType != null)
                {
                    barTypeId = firstBarType.Id;
                }
            }

            if (barTypeId == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException("No RebarBarType found in the project. Please load rebar bar types first.");
            }

            var areaReinf = AreaReinforcement.Create(
                doc,
                hostElem,
                curves,
                majorDir,
                areaTypeId,
                barTypeId,
                ElementId.InvalidElementId);

            if (areaReinf == null)
            {
                throw new InvalidOperationException("Failed to create AreaReinforcement in the specified host.");
            }

            var pTop1 = areaReinf.get_Parameter(BuiltInParameter.REBAR_SYSTEM_SPACING_TOP_DIR_1);
            if (pTop1 != null && !pTop1.IsReadOnly && spec.TopMajorSpacing > 0)
            {
                pTop1.Set(RevitUnitConverter.MmToFeet(spec.TopMajorSpacing));
            }
            var pTop2 = areaReinf.get_Parameter(BuiltInParameter.REBAR_SYSTEM_SPACING_TOP_DIR_2);
            if (pTop2 != null && !pTop2.IsReadOnly && spec.TopMinorSpacing > 0)
            {
                pTop2.Set(RevitUnitConverter.MmToFeet(spec.TopMinorSpacing));
            }
            var pBot1 = areaReinf.get_Parameter(BuiltInParameter.REBAR_SYSTEM_SPACING_BOTTOM_DIR_1);
            if (pBot1 != null && !pBot1.IsReadOnly && spec.BottomMajorSpacing > 0)
            {
                pBot1.Set(RevitUnitConverter.MmToFeet(spec.BottomMajorSpacing));
            }
            var pBot2 = areaReinf.get_Parameter(BuiltInParameter.REBAR_SYSTEM_SPACING_BOTTOM_DIR_2);
            if (pBot2 != null && !pBot2.IsReadOnly && spec.BottomMinorSpacing > 0)
            {
                pBot2.Set(RevitUnitConverter.MmToFeet(spec.BottomMinorSpacing));
            }

            var idVal = areaReinf.Id.ToLongId();
            return (idVal, $"AreaReinforcement created on host ID {spec.HostElementId} (ID: {idVal}).");
        }

        public static (long id, string summary) CreateRebar(Document doc, CreateRebarSpec spec)
        {
            var hostElem = doc.GetElement(ElementIdExtensions.FromLongId(spec.HostElementId));
            if (hostElem == null)
            {
                throw new InvalidOperationException($"Host element with ID {spec.HostElementId} not found.");
            }

            RebarBarType? barType = null;
            if (spec.BarTypeId.HasValue && spec.BarTypeId.Value > 0)
            {
                barType = doc.GetElement(ElementIdExtensions.FromLongId(spec.BarTypeId.Value)) as RebarBarType;
            }
            if (barType == null)
            {
                barType = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .FirstOrDefault();
            }
            if (barType == null)
            {
                throw new InvalidOperationException("No RebarBarType found in project.");
            }

            var curves = new List<Curve>();
            if (spec.CurveSegments != null)
            {
                foreach (var seg in spec.CurveSegments)
                {
                    if (seg != null && seg.Count >= 2)
                    {
                        var p1 = new XYZ(
                            RevitUnitConverter.MmToFeet(seg[0].X),
                            RevitUnitConverter.MmToFeet(seg[0].Y),
                            RevitUnitConverter.MmToFeet(seg[0].Z));
                        var p2 = new XYZ(
                            RevitUnitConverter.MmToFeet(seg[1].X),
                            RevitUnitConverter.MmToFeet(seg[1].Y),
                            RevitUnitConverter.MmToFeet(seg[1].Z));
                        if (p1.DistanceTo(p2) > 1e-5)
                        {
                            curves.Add(Line.CreateBound(p1, p2));
                        }
                    }
                }
            }

            if (curves.Count == 0)
            {
                throw new InvalidOperationException("Rebar curve segments cannot be empty.");
            }

            var normal = new XYZ(
                spec.Normal.X,
                spec.Normal.Y,
                spec.Normal.Z);
            if (normal.IsAlmostEqualTo(XYZ.Zero))
            {
                normal = XYZ.BasisZ;
            }
            normal = normal.Normalize();

#if REVIT_2026 || REVIT_2027
            var terminations = new BarTerminationsData(doc)
            {
                TerminationOrientationAtStart = RebarTerminationOrientation.Right,
                TerminationOrientationAtEnd = RebarTerminationOrientation.Right
            };

            var rebar = Rebar.CreateFromCurves(
                doc,
                RebarStyle.Standard,
                barType,
                hostElem,
                normal,
                curves,
                terminations,
                true,
                true);
#else
            var rebar = Rebar.CreateFromCurves(
                doc,
                RebarStyle.Standard,
                barType,
                null,
                null,
                hostElem,
                normal,
                curves,
                RebarHookOrientation.Right,
                RebarHookOrientation.Right,
                true,
                true);
#endif

            if (rebar == null)
            {
                throw new InvalidOperationException("Revit failed to create Rebar from the provided curves.");
            }

            if (!string.Equals(spec.LayoutRule, "Single", StringComparison.OrdinalIgnoreCase))
            {
                var accessor = rebar.GetShapeDrivenAccessor();
                if (accessor != null)
                {
                    double spacingFeet = RevitUnitConverter.MmToFeet(spec.Spacing > 0 ? spec.Spacing : 200);
                    double arrayLenFeet = RevitUnitConverter.MmToFeet(spec.ArrayLength > 0 ? spec.ArrayLength : 1000);
                    int qty = spec.Quantity > 0 ? spec.Quantity : 2;

                    switch (spec.LayoutRule.ToLowerInvariant())
                    {
                        case "maximumspacing":
                            accessor.SetLayoutAsMaximumSpacing(spacingFeet, arrayLenFeet, true, true, true);
                            break;
                        case "fixednumber":
                            accessor.SetLayoutAsFixedNumber(qty, arrayLenFeet, true, true, true);
                            break;
                        case "numberwithspacing":
                            accessor.SetLayoutAsNumberWithSpacing(qty, spacingFeet, true, true, true);
                            break;
                    }
                }
            }

            var idVal = rebar.Id.ToLongId();
            return (idVal, $"Rebar created with bar type '{barType.Name}' on host ID {spec.HostElementId} (ID: {idVal}).");
        }
    }
}
