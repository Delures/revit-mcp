using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands
{
    public class InspectElementGeometryHandler
    {
        public InspectElementGeometryResult Execute(UIApplication app, InspectElementGeometryRequest request)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                return new InspectElementGeometryResult
                {
                    Success = false,
                    ErrorMessage = "No active document found in Revit."
                };
            }

            var elem = doc.GetElement(ElementIdExtensions.FromLongId(request.ElementId));
            if (elem == null)
            {
                return new InspectElementGeometryResult
                {
                    Success = false,
                    ElementId = request.ElementId,
                    ErrorMessage = $"Element with ID {request.ElementId} not found in model."
                };
            }

            var result = new InspectElementGeometryResult
            {
                Success = true,
                ElementId = request.ElementId,
                CategoryName = elem.Category?.Name,
                ElementName = elem.Name
            };

            // 1. Bounding Box in mm
            var bbox = elem.get_BoundingBox(null);
            if (bbox != null)
            {
                double minX = RevitUnitConverter.FeetToMm(bbox.Min.X);
                double minY = RevitUnitConverter.FeetToMm(bbox.Min.Y);
                double minZ = RevitUnitConverter.FeetToMm(bbox.Min.Z);
                double maxX = RevitUnitConverter.FeetToMm(bbox.Max.X);
                double maxY = RevitUnitConverter.FeetToMm(bbox.Max.Y);
                double maxZ = RevitUnitConverter.FeetToMm(bbox.Max.Z);

                result.BoundingBoxMm = new BoundingBoxMm
                {
                    Min = new Point3DMm(minX, minY, minZ),
                    Max = new Point3DMm(maxX, maxY, maxZ),
                    WidthMm = Math.Abs(maxX - minX),
                    LengthMm = Math.Abs(maxY - minY),
                    HeightMm = Math.Abs(maxZ - minZ)
                };
            }

            // 2. Specific archetypes
            if (elem is Wall wall)
            {
                result.GeometryType = "wall";
                if (wall.WallType != null)
                {
                    result.ThicknessMm = Math.Round(RevitUnitConverter.FeetToMm(wall.WallType.Width), 2);
                }

                var heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                if (heightParam != null && heightParam.HasValue)
                {
                    result.HeightMm = Math.Round(RevitUnitConverter.FeetToMm(heightParam.AsDouble()), 2);
                }

                if (wall.Location is LocationCurve locCurve && locCurve.Curve != null)
                {
                    var p0 = locCurve.Curve.GetEndPoint(0);
                    var p1 = locCurve.Curve.GetEndPoint(1);
                    result.StartPointMm = new Point3DMm(RevitUnitConverter.FeetToMm(p0.X), RevitUnitConverter.FeetToMm(p0.Y), RevitUnitConverter.FeetToMm(p0.Z));
                    result.EndPointMm = new Point3DMm(RevitUnitConverter.FeetToMm(p1.X), RevitUnitConverter.FeetToMm(p1.Y), RevitUnitConverter.FeetToMm(p1.Z));
                    result.LengthMm = Math.Round(RevitUnitConverter.FeetToMm(locCurve.Curve.Length), 2);
                }

                try
                {
                    var orient = wall.Orientation;
                    if (orient != null)
                    {
                        result.Orientation = new Point3DMm(orient.X, orient.Y, orient.Z);
                    }
                }
                catch { }
            }
            else if (elem is Floor floor)
            {
                result.GeometryType = "slab";
                if (floor.FloorType != null)
                {
                    var thickParam = floor.FloorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                    if (thickParam != null && thickParam.HasValue)
                    {
                        result.ThicknessMm = Math.Round(RevitUnitConverter.FeetToMm(thickParam.AsDouble()), 2);
                    }
                }

                ExtractSlabBoundaryAndHoles(floor, result);
            }
            else if (elem is FamilyInstance fi)
            {
                if (fi.Location is LocationCurve locCurve && locCurve.Curve != null)
                {
                    result.GeometryType = "beam";
                    var p0 = locCurve.Curve.GetEndPoint(0);
                    var p1 = locCurve.Curve.GetEndPoint(1);
                    result.StartPointMm = new Point3DMm(RevitUnitConverter.FeetToMm(p0.X), RevitUnitConverter.FeetToMm(p0.Y), RevitUnitConverter.FeetToMm(p0.Z));
                    result.EndPointMm = new Point3DMm(RevitUnitConverter.FeetToMm(p1.X), RevitUnitConverter.FeetToMm(p1.Y), RevitUnitConverter.FeetToMm(p1.Z));
                    result.LengthMm = Math.Round(RevitUnitConverter.FeetToMm(locCurve.Curve.Length), 2);
                }
                else if (fi.Location is LocationPoint locPoint && locPoint.Point != null)
                {
                    result.GeometryType = "column";
                    var pt = locPoint.Point;
                    result.LocationPointMm = new Point3DMm(RevitUnitConverter.FeetToMm(pt.X), RevitUnitConverter.FeetToMm(pt.Y), RevitUnitConverter.FeetToMm(pt.Z));
                    try
                    {
                        var facing = fi.FacingOrientation;
                        if (facing != null)
                        {
                            result.Orientation = new Point3DMm(facing.X, facing.Y, facing.Z);
                        }
                    }
                    catch { }
                }
            }

            // 3. Fallback / generic solid analysis
            ExtractSolidsAndFaces(elem, result, request.IncludeFaces);

            return result;
        }

        private static void ExtractSlabBoundaryAndHoles(Floor floor, InspectElementGeometryResult result)
        {
            var opt = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = true };
            var geom = floor.get_Geometry(opt);
            if (geom == null) return;

            PlanarFace? topFace = null;
            double maxZ = double.MinValue;

            foreach (var obj in geom)
            {
                if (obj is Solid solid && solid.Volume > 1e-6)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.Z > 0.8)
                        {
                            if (pf.Origin.Z > maxZ)
                            {
                                maxZ = pf.Origin.Z;
                                topFace = pf;
                            }
                        }
                    }
                }
            }

            if (topFace == null) return;

            var curveLoops = topFace.GetEdgesAsCurveLoops();
            if (curveLoops == null || curveLoops.Count == 0) return;

            // Sort loops by area descending: outer loop is largest
            var loopsList = new List<CurveLoop>();
            foreach (CurveLoop loop in curveLoops)
            {
                loopsList.Add(loop);
            }

            loopsList.Sort((a, b) => Math.Abs(b.GetExactLength()).CompareTo(Math.Abs(a.GetExactLength())));

            // Outer loop
            var outerLoop = loopsList[0];
            result.BoundaryPointsMm = ExtractCurveLoopPoints(outerLoop);

            // Inner loops (holes)
            if (loopsList.Count > 1)
            {
                result.HolesMm = new List<List<Point3DMm>>();
                for (int i = 1; i < loopsList.Count; i++)
                {
                    var holePts = ExtractCurveLoopPoints(loopsList[i]);
                    if (holePts.Count >= 3)
                    {
                        result.HolesMm.Add(holePts);
                    }
                }
            }
        }

        private static List<Point3DMm> ExtractCurveLoopPoints(CurveLoop loop)
        {
            var points = new List<Point3DMm>();
            foreach (Curve curve in loop)
            {
                var tess = curve.Tessellate();
                if (tess != null)
                {
                    for (int i = 0; i < tess.Count - 1; i++) // Skip last point of segment to avoid duplicates
                    {
                        points.Add(new Point3DMm(
                            Math.Round(RevitUnitConverter.FeetToMm(tess[i].X), 2),
                            Math.Round(RevitUnitConverter.FeetToMm(tess[i].Y), 2),
                            Math.Round(RevitUnitConverter.FeetToMm(tess[i].Z), 2)
                        ));
                    }
                }
                else
                {
                    var p = curve.GetEndPoint(0);
                    points.Add(new Point3DMm(
                        Math.Round(RevitUnitConverter.FeetToMm(p.X), 2),
                        Math.Round(RevitUnitConverter.FeetToMm(p.Y), 2),
                        Math.Round(RevitUnitConverter.FeetToMm(p.Z), 2)
                    ));
                }
            }
            return points;
        }

        private static void ExtractSolidsAndFaces(Element elem, InspectElementGeometryResult result, bool includeFaces)
        {
            var opt = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = true };
            var geom = elem.get_Geometry(opt);
            if (geom == null) return;

            double totalVolumeCuFt = 0;
            XYZ weightedCentroid = XYZ.Zero;
            var facesList = includeFaces ? new List<FaceGeometryInfo>() : null;

            void ProcessGeometryElement(GeometryElement ge)
            {
                foreach (var obj in ge)
                {
                    if (obj is Solid solid && solid.Volume > 1e-6)
                    {
                        totalVolumeCuFt += solid.Volume;
                        try
                        {
                            var c = solid.ComputeCentroid();
                            weightedCentroid = weightedCentroid.Add(c.Multiply(solid.Volume));
                        }
                        catch { }

                        if (includeFaces && facesList != null)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                if (face is PlanarFace pf)
                                {
                                    var fInfo = new FaceGeometryInfo
                                    {
                                        FaceType = "planar",
                                        Normal = new Point3DMm(pf.FaceNormal.X, pf.FaceNormal.Y, pf.FaceNormal.Z),
                                        AreaM2 = Math.Round(RevitUnitConverter.SqFeetToSqMeters(pf.Area), 4)
                                    };

                                    try
                                    {
                                        var loops = pf.GetEdgesAsCurveLoops();
                                        if (loops != null && loops.Count > 0)
                                        {
                                            fInfo.OuterLoopMm = ExtractCurveLoopPoints(loops.First());
                                        }
                                    }
                                    catch { }

                                    facesList.Add(fInfo);
                                }
                            }
                        }
                    }
                    else if (obj is GeometryInstance gi)
                    {
                        var instGeom = gi.GetInstanceGeometry();
                        if (instGeom != null)
                        {
                            ProcessGeometryElement(instGeom);
                        }
                    }
                }
            }

            ProcessGeometryElement(geom);

            if (totalVolumeCuFt > 1e-6)
            {
                result.VolumeM3 = Math.Round(RevitUnitConverter.CuFeetToCuMeters(totalVolumeCuFt), 4);
                var c = weightedCentroid.Divide(totalVolumeCuFt);
                result.CentroidMm = new Point3DMm(
                    Math.Round(RevitUnitConverter.FeetToMm(c.X), 2),
                    Math.Round(RevitUnitConverter.FeetToMm(c.Y), 2),
                    Math.Round(RevitUnitConverter.FeetToMm(c.Z), 2)
                );
            }

            if (string.IsNullOrEmpty(result.GeometryType))
            {
                result.GeometryType = "generic_solid";
            }

            if (facesList != null && facesList.Count > 0)
            {
                result.Faces = facesList;
            }
        }
    }
}
