using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Utils
{
    /// <summary>
    /// Universal dispatcher for creating FamilyInstance elements across all Revit API overloads.
    /// Handles Unhosted, Level-hosted, Host-hosted, Face-hosted, Curve-based, and View-based placements
    /// with intelligent fallback and geometry reference resolution.
    /// </summary>
    public static class FamilyInstanceDispatcher
    {
        public static FamilyInstance CreateInstance(Document doc, FamilySymbol symbol, CreateFamilyInstanceSpec spec)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            // Ensure symbol is active in project
            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            // 1. Resolve StructuralType
            StructuralType structuralType = StructuralType.NonStructural;
            if (!string.IsNullOrEmpty(spec.StructuralType))
            {
                if (Enum.TryParse<StructuralType>(spec.StructuralType, true, out var parsed))
                {
                    structuralType = parsed;
                }
            }

            // 2. Resolve Level
            Level? level = null;
            if (spec.LevelId.HasValue && spec.LevelId.Value > 0)
            {
                level = doc.GetElement(ElementIdExtensions.FromLongId(spec.LevelId.Value)) as Level;
            }

            // 3. Resolve Host element
            Element? host = null;
            if (spec.HostElementId.HasValue && spec.HostElementId.Value > 0)
            {
                host = doc.GetElement(ElementIdExtensions.FromLongId(spec.HostElementId.Value));
            }

            // 4. Resolve View
            View? view = null;
            if (spec.ViewId.HasValue && spec.ViewId.Value > 0)
            {
                view = doc.GetElement(ElementIdExtensions.FromLongId(spec.ViewId.Value)) as View;
            }

            // 5. Resolve Location Point
            var loc = new XYZ(
                RevitUnitConverter.MmToFeet(spec.Location.X),
                RevitUnitConverter.MmToFeet(spec.Location.Y),
                RevitUnitConverter.MmToFeet(spec.Location.Z));

            // 6. Resolve Reference Direction
            XYZ? refDir = null;
            if (spec.ReferenceDirection != null)
            {
                var dir = new XYZ(spec.ReferenceDirection.X, spec.ReferenceDirection.Y, spec.ReferenceDirection.Z);
                if (!dir.IsAlmostEqualTo(XYZ.Zero))
                {
                    refDir = dir.Normalize();
                }
            }

            FamilyInstance? instance = null;
            FamilyPlacementType placementType = symbol.Family.FamilyPlacementType;

            // Scenario A: Linear / Curve-based family
            if ((placementType == FamilyPlacementType.CurveBased || placementType == FamilyPlacementType.CurveBasedDetail)
                || (spec.StartPoint != null && spec.EndPoint != null))
            {
                var p1 = spec.StartPoint != null
                    ? new XYZ(RevitUnitConverter.MmToFeet(spec.StartPoint.X), RevitUnitConverter.MmToFeet(spec.StartPoint.Y), RevitUnitConverter.MmToFeet(spec.StartPoint.Z))
                    : loc;
                var p2 = spec.EndPoint != null
                    ? new XYZ(RevitUnitConverter.MmToFeet(spec.EndPoint.X), RevitUnitConverter.MmToFeet(spec.EndPoint.Y), RevitUnitConverter.MmToFeet(spec.EndPoint.Z))
                    : loc + XYZ.BasisX * 3.28084; // 1000 mm fallback

                if (p1.DistanceTo(p2) > 1e-5)
                {
                    var curve = Line.CreateBound(p1, p2);
                    if (level == null)
                    {
                        level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();
                    }

                    try
                    {
                        instance = doc.Create.NewFamilyInstance(curve, symbol, level, structuralType);
                        return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                    }
                    catch
                    {
                        // Continue to fallbacks
                    }
                }
            }

            // Scenario B: View-based family (annotations, detail items, drafting components)
            if (placementType == FamilyPlacementType.ViewBased || view != null)
            {
                if (view != null)
                {
                    try
                    {
                        instance = doc.Create.NewFamilyInstance(loc, symbol, view);
                        return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                    }
                    catch
                    {
                        // Continue to fallbacks
                    }
                }
            }

            // Scenario C: Face-hosted family (WorkPlaneBased or face on host)
            if (placementType == FamilyPlacementType.WorkPlaneBased && host != null)
            {
                var face = FindClosestFace(host, loc);
                if (face != null)
                {
                    try
                    {
                        var dir = refDir ?? XYZ.BasisX;
                        instance = doc.Create.NewFamilyInstance(face, loc, dir, symbol);
                        return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                    }
                    catch
                    {
                        // Continue to fallbacks
                    }
                }
            }

            // Scenario D: Host with ReferenceDirection (e.g. windows/doors with specific wall orientation)
            if (host != null && refDir != null)
            {
                try
                {
                    instance = doc.Create.NewFamilyInstance(loc, symbol, refDir, host, structuralType);
                    return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                }
                catch
                {
                    // Continue to fallbacks
                }
            }

            // Scenario E: Host + Level
            if (host != null && level != null)
            {
                try
                {
                    instance = doc.Create.NewFamilyInstance(loc, symbol, host, level, structuralType);
                    return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                }
                catch
                {
                    // Continue to fallbacks
                }
            }

            // Scenario F: Host only
            if (host != null)
            {
                try
                {
                    instance = doc.Create.NewFamilyInstance(loc, symbol, host, structuralType);
                    return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                }
                catch
                {
                    // Continue to fallbacks
                }
            }

            // Scenario G: Level only
            if (level != null)
            {
                try
                {
                    instance = doc.Create.NewFamilyInstance(loc, symbol, level, structuralType);
                    return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                }
                catch
                {
                    // Continue to fallbacks
                }
            }

            // Scenario H: Unhosted Point
            try
            {
                instance = doc.Create.NewFamilyInstance(loc, symbol, structuralType);
                return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
            }
            catch (Exception ex)
            {
                // Final fallback: if level was null, query lowest level and retry
                if (level == null)
                {
                    var fallbackLevel = new FilteredElementCollector(doc)
                        .OfClass(typeof(Level))
                        .Cast<Level>()
                        .OrderBy(l => l.Elevation)
                        .FirstOrDefault();

                    if (fallbackLevel != null)
                    {
                        try
                        {
                            instance = doc.Create.NewFamilyInstance(loc, symbol, fallbackLevel, structuralType);
                            return RotateIfNeeded(doc, instance, loc, spec.RotationDegrees);
                        }
                        catch { }
                    }
                }

                throw new InvalidOperationException(
                    $"Failed to create FamilyInstance '{symbol.FamilyName}:{symbol.Name}' with placement type '{placementType}'. Revit error: {ex.Message}", ex);
            }
        }

        private static Face? FindClosestFace(Element element, XYZ point)
        {
            var opt = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            var geom = element.get_Geometry(opt);
            if (geom == null) return null;

            Face? bestFace = null;
            double minDistance = double.MaxValue;

            foreach (var obj in geom)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        var res = face.Project(point);
                        if (res != null && res.Distance < minDistance)
                        {
                            minDistance = res.Distance;
                            bestFace = face;
                        }
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    var instGeom = gi.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        foreach (var subObj in instGeom)
                        {
                            if (subObj is Solid s && s.Faces.Size > 0)
                            {
                                foreach (Face face in s.Faces)
                                {
                                    var res = face.Project(point);
                                    if (res != null && res.Distance < minDistance)
                                    {
                                        minDistance = res.Distance;
                                        bestFace = face;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return bestFace;
        }

        private static FamilyInstance RotateIfNeeded(Document doc, FamilyInstance instance, XYZ loc, double rotationDegrees)
        {
            if (instance != null && Math.Abs(rotationDegrees) > 1e-5)
            {
                var axis = Line.CreateBound(loc, loc + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, instance.Id, axis, RevitUnitConverter.DegreesToRadians(rotationDegrees));
            }
            return instance!;
        }
    }
}
