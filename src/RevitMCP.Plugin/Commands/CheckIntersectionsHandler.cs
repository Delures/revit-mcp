using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands
{
    public class CheckIntersectionsHandler
    {
        private const double AabbOverlapToleranceFeet = 0.003;   // ~1 mm
        private const double DefaultMinClashVolumeM3 = 0.0001;   // reject touch-only clashes

        public CheckIntersectionsResult Execute(Document doc, CheckIntersectionsRequest request)
        {
            var sw = Stopwatch.StartNew();
            string mode = (request.Mode ?? "element").Trim().ToLowerInvariant();
            var result = new CheckIntersectionsResult
            {
                Mode = mode,
                TargetElementId = request.TargetElementId
            };

            try
            {
                switch (mode)
                {
                    case "matrix":
                        ExecuteMatrix(doc, request, result);
                        break;
                    case "duplicates":
                        ExecuteDuplicates(doc, request, result);
                        break;
                    case "element":
                    default:
                        ExecuteElement(doc, request, result);
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorCode = "INTERSECTION_CHECK_FAILED";
                result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                sw.Stop();
                result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            }

            return result;
        }

        private void ExecuteElement(Document doc, CheckIntersectionsRequest request, CheckIntersectionsResult result)
        {
            if (!request.TargetElementId.HasValue || request.TargetElementId.Value <= 0)
            {
                result.Success = false;
                result.ErrorCode = "TARGET_REQUIRED";
                result.ErrorMessage = "targetElementId is required and must be > 0 in 'element' mode.";
                return;
            }

            var targetId = ElementIdExtensions.FromLongId(request.TargetElementId.Value);
            var targetElem = doc.GetElement(targetId);
            if (targetElem == null)
            {
                result.Success = false;
                result.ErrorCode = "TARGET_ELEMENT_NOT_FOUND";
                result.ErrorMessage = $"Target element with ID {request.TargetElementId} was not found in the active document.";
                return;
            }

            result.TargetName = targetElem.Name ?? string.Empty;
            result.TargetCategory = targetElem.Category?.Name ?? "Uncategorized";

            // Build intersection filter (prefer 3D solid intersection, fallback to bounding box)
            ElementFilter intersectionFilter;
            try
            {
                intersectionFilter = new ElementIntersectsElementFilter(targetElem);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                var bbox = targetElem.get_BoundingBox(null);
                if (bbox == null)
                {
                    result.Success = false;
                    result.ErrorCode = "TARGET_NO_GEOMETRY";
                    result.ErrorMessage = $"Target element with ID {request.TargetElementId} has no 3D geometry or bounding box.";
                    return;
                }

                var outline = new Outline(bbox.Min, bbox.Max);
                intersectionFilter = new BoundingBoxIntersectsFilter(outline);
            }

            FilteredElementCollector collector = (request.ActiveViewOnly && doc.ActiveView != null)
                ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                : new FilteredElementCollector(doc);

            collector.WhereElementIsNotElementType();
            collector.Excluding(new[] { targetId });

            // Filter by candidate categories if specified
            if (request.CandidateCategories != null && request.CandidateCategories.Count > 0)
            {
                var catIds = new List<ElementId>();
                foreach (var catName in request.CandidateCategories)
                {
                    var cid = CategoryResolver.ResolveCategoryId(doc, catName);
                    if (cid != null) catIds.Add(cid);
                }

                if (catIds.Count == 1)
                {
                    collector.OfCategoryId(catIds[0]);
                }
                else if (catIds.Count > 1)
                {
                    var catFilters = catIds.Select(id => (ElementFilter)new ElementCategoryFilter(id)).ToList();
                    collector.WherePasses(new LogicalOrFilter(catFilters));
                }
                else
                {
                    // User specified categories that don't exist in project -> 0 intersections
                    result.Success = true;
                    result.TotalIntersections = 0;
                    return;
                }
            }

            collector.WherePasses(intersectionFilter);

            var targetCandidateIds = request.CandidateElementIds != null && request.CandidateElementIds.Count > 0
                ? new HashSet<long>(request.CandidateElementIds)
                : null;

            int limit = request.Limit > 0 ? Math.Min(request.Limit, 1000) : 100;
            var foundElements = new List<Element>();

            foreach (Element e in collector)
            {
                if (targetCandidateIds != null && !targetCandidateIds.Contains(e.Id.ToLongId()))
                {
                    continue;
                }

                foundElements.Add(e);
                if (foundElements.Count >= limit) break;
            }

            result.TotalIntersections = foundElements.Count;

            // Extract target solids if clash volume calculation is requested
            List<Solid>? targetSolids = null;
            Options? opt = null;
            if (request.CalculateClashVolume)
            {
                opt = new Options { DetailLevel = ViewDetailLevel.Fine };
                targetSolids = GetElementSolids(targetElem, opt);
            }

            foreach (var e in foundElements)
            {
                var clash = new ClashRecord
                {
                    ElementId = e.Id.ToLongId(),
                    CategoryName = e.Category?.Name ?? "Uncategorized",
                    TypeName = doc.GetElement(e.GetTypeId())?.Name ?? string.Empty,
                    ElementName = e.Name ?? string.Empty
                };

                if (e is FamilyInstance fi && fi.Symbol?.Family != null)
                {
                    clash.FamilyName = fi.Symbol.Family.Name;
                }

                // Bounding box in mm
                var bbox = e.get_BoundingBox(null);
                if (bbox != null)
                {
                    clash.ClashBoundingBoxMm = new ClashBboxMm
                    {
                        Min = new[]
                        {
                            Math.Round(RevitUnitConverter.FeetToMm(bbox.Min.X), 2),
                            Math.Round(RevitUnitConverter.FeetToMm(bbox.Min.Y), 2),
                            Math.Round(RevitUnitConverter.FeetToMm(bbox.Min.Z), 2)
                        },
                        Max = new[]
                        {
                            Math.Round(RevitUnitConverter.FeetToMm(bbox.Max.X), 2),
                            Math.Round(RevitUnitConverter.FeetToMm(bbox.Max.Y), 2),
                            Math.Round(RevitUnitConverter.FeetToMm(bbox.Max.Z), 2)
                        }
                    };
                }

                // Calculate Clash Volume if requested
                if (request.CalculateClashVolume && targetSolids != null && targetSolids.Count > 0 && opt != null)
                {
                    var candSolids = GetElementSolids(e, opt);
                    double clashFeet3 = 0.0;

                    foreach (var ts in targetSolids)
                    {
                        foreach (var cs in candSolids)
                        {
                            try
                            {
                                var interSolid = BooleanOperationsUtils.ExecuteBooleanOperation(ts, cs, BooleanOperationsType.Intersect);
                                if (interSolid != null && interSolid.Volume > 1e-6)
                                {
                                    clashFeet3 += interSolid.Volume;
                                }
                            }
                            catch
                            {
                                // Ignore non-manifold or coplanar facet issues
                            }
                        }
                    }

                    if (clashFeet3 > 0.0)
                    {
                        double m3 = clashFeet3 * RevitUnitConverter.CuFeetToCuMetersRatio;
                        clash.ClashVolumeM3 = Math.Round(m3, 5);
                    }
                }

                result.Intersections.Add(clash);
            }

            result.Success = true;
        }

        private void ExecuteMatrix(Document doc, CheckIntersectionsRequest request, CheckIntersectionsResult result)
        {
            // Source set: sourceElementIds OR all elements in sourceCategories
            var sourceElems = ResolveElementSet(doc, request.SourceElementIds, request.SourceCategories, request.LevelName);
            // Target set: elements in targetCategories (default: same as source)
            var targetCats = request.TargetCategories ?? request.SourceCategories;
            var targetElems = ResolveElementSet(doc, null, targetCats, request.LevelName);

            if (sourceElems.Count == 0 || targetElems.Count == 0)
            {
                result.Success = true;
                result.TotalIntersections = 0;
                return;
            }

            int maxDurationMs = request.MaxDurationMs ?? 25000;
            double minClashM3 = request.MinClashVolumeM3 ?? DefaultMinClashVolumeM3;
            var solidCache = new Dictionary<long, Solid>();
            var opt = new Options { DetailLevel = ViewDetailLevel.Fine };

            // Pre-filter pairs by AABB overlap
            var sourceBoxes = sourceElems.Select(e => new { Elem = e, Box = e.get_BoundingBox(null) }).ToList();
            var targetBoxes = targetElems.Select(e => new { Elem = e, Box = e.get_BoundingBox(null) }).ToList();

            int checkedPairs = 0;
            var matrixCells = new Dictionary<string, MatrixCell>();
            var intersectionRecords = new List<ClashRecord>();
            var sw = Stopwatch.StartNew();
            bool truncated = false;

            foreach (var src in sourceBoxes)
            {
                if (src.Box == null) continue;
                foreach (var tgt in targetBoxes)
                {
                    if (tgt.Box == null || tgt.Elem.Id == src.Elem.Id) continue;

                    if (sw.ElapsedMilliseconds >= maxDurationMs)
                    {
                        truncated = true;
                        break;
                    }

                    if (!BoundingBoxesOverlap(src.Box, tgt.Box, AabbOverlapToleranceFeet)) continue;

                    checkedPairs++;

                    double clashM3 = 0.0;
                    try
                    {
                        var srcSolid = GetCachedSolid(src.Elem, opt, solidCache);
                        var tgtSolid = GetCachedSolid(tgt.Elem, opt, solidCache);
                        if (srcSolid == null || tgtSolid == null) continue;

                        var inter = BooleanOperationsUtils.ExecuteBooleanOperation(srcSolid, tgtSolid, BooleanOperationsType.Intersect);
                        if (inter != null && inter.Volume > 1e-6)
                        {
                            clashM3 = inter.Volume * RevitUnitConverter.CuFeetToCuMetersRatio;
                        }
                    }
                    catch { }

                    if (clashM3 < minClashM3) continue;

                    string srcCat = src.Elem.Category?.Name ?? "Uncategorized";
                    string tgtCat = tgt.Elem.Category?.Name ?? "Uncategorized";
                    string cellKey = srcCat + "|" + tgtCat;
                    if (!matrixCells.TryGetValue(cellKey, out var cell))
                    {
                        cell = new MatrixCell { SourceCategory = srcCat, TargetCategory = tgtCat };
                        matrixCells[cellKey] = cell;
                    }
                    cell.ClashCount++;
                    cell.TotalVolumeM3 += clashM3;

                    intersectionRecords.Add(new ClashRecord
                    {
                        ElementId = tgt.Elem.Id.ToLongId(),
                        CategoryName = tgtCat,
                        FamilyName = (tgt.Elem is FamilyInstance tfi && tfi.Symbol?.Family != null) ? tfi.Symbol.Family.Name : string.Empty,
                        TypeName = doc.GetElement(tgt.Elem.GetTypeId())?.Name ?? string.Empty,
                        ElementName = tgt.Elem.Name ?? string.Empty,
                        ClashVolumeM3 = Math.Round(clashM3, 5)
                    });

                    if (intersectionRecords.Count >= 1000) { truncated = true; break; }
                }
                if (truncated) break;
            }

            result.Matrix = matrixCells.Values.OrderByDescending(c => c.ClashCount).ToList();
            result.TotalIntersections = intersectionRecords.Count;
            result.Intersections = intersectionRecords;
            result.CheckedPairs = checkedPairs;
            result.TotalCandidatePairs = sourceBoxes.Count(s => s.Box != null) * targetBoxes.Count(t => t.Box != null);
            result.Truncated = truncated;
            result.Success = true;
        }

        private void ExecuteDuplicates(Document doc, CheckIntersectionsRequest request, CheckIntersectionsResult result)
        {
            var elems = ResolveElementSet(doc, request.SourceElementIds, request.SourceCategories, request.LevelName);
            if (elems.Count == 0)
            {
                result.Success = true;
                result.TotalIntersections = 0;
                return;
            }

            double toleranceFeet = (request.ToleranceMm ?? 3.0) / 304.8;

            // Group by (categoryId, typeId), then find AABB matches within tolerance
            var groups = new List<DuplicateGroup>();
            var byType = elems
                .Where(e => e.Category != null)
                .GroupBy(e => new { CatId = e.Category!.Id.ToLongId(), TypeId = e.GetTypeId().ToLongId() });

            foreach (var group in byType)
            {
                var items = group.Select(e => new
                {
                    Elem = e,
                    Box = e.get_BoundingBox(null)
                }).Where(x => x.Box != null).ToList();

                if (items.Count < 2) continue;

                var visited = new HashSet<long>();
                foreach (var item in items)
                {
                    if (visited.Contains(item.Elem.Id.ToLongId())) continue;

                    var dupGroup = new List<Element> { item.Elem };
                    visited.Add(item.Elem.Id.ToLongId());

                    foreach (var other in items)
                    {
                        if (visited.Contains(other.Elem.Id.ToLongId())) continue;
                        if (BoundingBoxesEqualWithinTolerance(item.Box!, other.Box!, toleranceFeet))
                        {
                            dupGroup.Add(other.Elem);
                            visited.Add(other.Elem.Id.ToLongId());
                        }
                    }

                    if (dupGroup.Count >= 2)
                    {
                        var typeName = doc.GetElement(dupGroup[0].GetTypeId())?.Name ?? string.Empty;
                        groups.Add(new DuplicateGroup
                        {
                            CategoryId = group.Key.CatId,
                            TypeId = group.Key.TypeId,
                            CategoryName = dupGroup[0].Category?.Name ?? "Uncategorized",
                            TypeName = typeName,
                            Count = dupGroup.Count,
                            ElementIds = dupGroup.Select(e => e.Id.ToLongId()).ToList(),
                            BoundingBoxMm = ToBboxMm(dupGroup[0].get_BoundingBox(null))
                        });
                    }
                }
            }

            result.DuplicateGroups = groups;
            result.TotalIntersections = groups.Sum(g => g.Count);
            result.Success = true;
        }

        private List<Element> ResolveElementSet(Document doc, List<long>? elementIds, List<string>? categories, string? levelName)
        {
            if (elementIds != null && elementIds.Count > 0)
            {
                var list = new List<Element>();
                foreach (var id in elementIds)
                {
                    var elem = doc.GetElement(ElementIdExtensions.FromLongId(id));
                    if (elem != null && elem.Category != null) list.Add(elem);
                }
                return list;
            }

            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            if (categories != null && categories.Count > 0)
            {
                var catIds = new List<ElementId>();
                foreach (var catName in categories)
                {
                    var cid = CategoryResolver.ResolveCategoryId(doc, catName);
                    if (cid != null) catIds.Add(cid);
                }
                if (catIds.Count == 1)
                {
                    collector.OfCategoryId(catIds[0]);
                }
                else if (catIds.Count > 1)
                {
                    var catFilters = catIds.Select(id => (ElementFilter)new ElementCategoryFilter(id)).ToList();
                    collector.WherePasses(new LogicalOrFilter(catFilters));
                }
                else
                {
                    return new List<Element>();
                }
            }
            else
            {
                // all model categories (used by duplicates mode without explicit categories)
                collector.WhereElementIsNotElementType();
                var allElems = collector.Cast<Element>()
                    .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model);
                return allElems.ToList();
            }

            var elems = collector.Cast<Element>().Where(e => e.Category != null).ToList();

            if (!string.IsNullOrWhiteSpace(levelName))
            {
                var levelId = ResolveLevelIdByName(doc, levelName!);
                if (levelId != null)
                {
                    elems = elems.Where(e =>
                        (e.LevelId != null && e.LevelId == levelId) ||
                        (e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)?.AsElementId() == levelId))
                        .ToList();
                }
            }

            return elems;
        }

        private static ElementId? ResolveLevelIdByName(Document doc, string levelName)
        {
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>();
            var match = levels.FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase));
            return match?.Id;
        }

        private Solid? GetCachedSolid(Element elem, Options opt, Dictionary<long, Solid> cache)
        {
            long key = elem.Id.ToLongId();
            if (cache.TryGetValue(key, out var cached)) return cached;

            var solids = GetElementSolids(elem, opt);
            Solid? combined = null;
            if (solids.Count == 1)
            {
                combined = solids[0];
            }
            else if (solids.Count > 1)
            {
                try
                {
                    combined = solids[0];
                    for (int i = 1; i < solids.Count; i++)
                    {
                        combined = BooleanOperationsUtils.ExecuteBooleanOperation(combined, solids[i], BooleanOperationsType.Union);
                    }
                }
                catch
                {
                    combined = solids[0];
                }
            }

            cache[key] = combined!;
            return combined;
        }

        private static bool BoundingBoxesOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b, double tol)
        {
            return a.Min.X <= b.Max.X + tol && a.Max.X >= b.Min.X - tol &&
                   a.Min.Y <= b.Max.Y + tol && a.Max.Y >= b.Min.Y - tol &&
                   a.Min.Z <= b.Max.Z + tol && a.Max.Z >= b.Min.Z - tol;
        }

        private static bool BoundingBoxesEqualWithinTolerance(BoundingBoxXYZ a, BoundingBoxXYZ b, double tol)
        {
            return Math.Abs(a.Min.X - b.Min.X) <= tol && Math.Abs(a.Min.Y - b.Min.Y) <= tol &&
                   Math.Abs(a.Min.Z - b.Min.Z) <= tol && Math.Abs(a.Max.X - b.Max.X) <= tol &&
                   Math.Abs(a.Max.Y - b.Max.Y) <= tol && Math.Abs(a.Max.Z - b.Max.Z) <= tol;
        }

        private static ClashBboxMm? ToBboxMm(BoundingBoxXYZ? bbox)
        {
            if (bbox == null) return null;
            return new ClashBboxMm
            {
                Min = new[]
                {
                    Math.Round(RevitUnitConverter.FeetToMm(bbox.Min.X), 2),
                    Math.Round(RevitUnitConverter.FeetToMm(bbox.Min.Y), 2),
                    Math.Round(RevitUnitConverter.FeetToMm(bbox.Min.Z), 2)
                },
                Max = new[]
                {
                    Math.Round(RevitUnitConverter.FeetToMm(bbox.Max.X), 2),
                    Math.Round(RevitUnitConverter.FeetToMm(bbox.Max.Y), 2),
                    Math.Round(RevitUnitConverter.FeetToMm(bbox.Max.Z), 2)
                }
            };
        }

        private static List<Solid> GetElementSolids(Element elem, Options opt)
        {
            var solids = new List<Solid>();
            var geom = elem.get_Geometry(opt);
            if (geom == null) return solids;

            ExtractSolids(geom, solids);
            return solids;
        }

        private static void ExtractSolids(GeometryElement geomElem, List<Solid> solids)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid s && s.Volume > 1e-6)
                {
                    solids.Add(s);
                }
                else if (obj is GeometryInstance gi)
                {
                    var instGeom = gi.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        ExtractSolids(instGeom, solids);
                    }
                }
            }
        }
    }
}
