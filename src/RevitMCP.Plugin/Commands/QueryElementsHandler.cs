using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;

namespace RevitMCP.Plugin.Commands
{
    public static class QueryElementsHandler
    {
        private const double MmToFeet = 1.0 / 304.8;
        private const double FeetToMm = 304.8;

        public static QueryElementsResult Execute(Document doc, QueryElementsRequest request)
        {
            if (doc == null)
            {
                throw new InvalidOperationException("No active document.");
            }

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            // 1. Category Filter
            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                var catId = ResolveCategoryId(doc, request.Category!);
                if (catId != null)
                {
                    collector.OfCategoryId(catId);
                }
            }

            // 2. Level Filter
            if (!string.IsNullOrWhiteSpace(request.Level))
            {
                var levelId = ResolveLevelId(doc, request.Level!);
                if (levelId != null)
                {
                    var levelFilter = new ElementLevelFilter(levelId);
                    collector.WherePasses(levelFilter);
                }
            }

            // 3. Bounding Box Filter
            if (request.BoundingBox != null &&
                request.BoundingBox.Min?.Length == 3 &&
                request.BoundingBox.Max?.Length == 3)
            {
                var min = new XYZ(
                    request.BoundingBox.Min[0] * MmToFeet,
                    request.BoundingBox.Min[1] * MmToFeet,
                    request.BoundingBox.Min[2] * MmToFeet);

                var max = new XYZ(
                    request.BoundingBox.Max[0] * MmToFeet,
                    request.BoundingBox.Max[1] * MmToFeet,
                    request.BoundingBox.Max[2] * MmToFeet);

                var outline = new Outline(min, max);
                var bbFilter = new BoundingBoxIntersectsFilter(outline);
                collector.WherePasses(bbFilter);
            }

            IEnumerable<Element> elements = collector;

            // 4. Parameter Filters (optionally also look at type parameters)
            if (request.Parameters != null && request.Parameters.Count > 0)
            {
                elements = elements.Where(e => MatchesParameterFilters(doc, e, request.Parameters, request.IncludeTypeParameters));
            }

            var elementList = elements.ToList();
            int totalCount = elementList.Count;

            bool summaryOnly = string.Equals(request.Mode, "summary", StringComparison.OrdinalIgnoreCase);

            int offset = Math.Max(0, request.Offset);
            bool includeParameters = request.IncludeParameters ?? true;

            // limit ceiling: without parameters allow larger result sets
            int limitCeiling = includeParameters ? 500 : 2000;
            int limit = request.Limit <= 0 ? (summaryOnly ? 0 : 50) : Math.Min(request.Limit, limitCeiling);

            var result = new QueryElementsResult
            {
                TotalCount = totalCount,
                Offset = offset
            };

            // Group counts (exact, over the full filtered set — never truncated at 200)
            if (request.GroupBy != null && request.GroupBy.Count > 0)
            {
                result.Groups = BuildGroups(doc, elementList, request.GroupBy);
            }

            // Parameter fill-rate audit
            if (request.ParameterAudit != null && request.ParameterAudit.Count > 0)
            {
                result.ParameterAudit = BuildParameterAudit(doc, elementList, request.ParameterAudit, request.IncludeTypeParameters);
            }

            // Level quality check
            if (request.CheckLevelQuality)
            {
                result.LevelQuality = BuildLevelQuality(doc, elementList);
            }

            if (!summaryOnly && limit > 0)
            {
                var paged = elementList.Skip(offset).Take(limit).ToList();
                result.ReturnedCount = paged.Count;
                result.Elements = paged
                    .Select(e => MapElementDto(doc, e, request.Fields, includeParameters, request.ParameterNames))
                    .ToList();
            }

            return result;
        }

        private static List<GroupCount> BuildGroups(Document doc, List<Element> elements, List<string> groupBy)
        {
            var result = new List<GroupCount>();

            foreach (var key in groupBy)
            {
                string normalized = key?.Trim().ToLowerInvariant() ?? string.Empty;
                if (normalized.Length == 0) continue;

                IEnumerable<IGrouping<string, Element>> groups;
                switch (normalized)
                {
                    case "category":
                        groups = elements.GroupBy(e => e.Category?.Name ?? "Uncategorized");
                        break;
                    case "level":
                        groups = elements.GroupBy(e => GetLevelName(doc, e));
                        break;
                    case "workset":
                        groups = elements.GroupBy(e => GetWorksetName(doc, e));
                        break;
                    case "family":
                        groups = elements.GroupBy(e => GetFamilyName(doc, e));
                        break;
                    case "type":
                        groups = elements.GroupBy(e => doc.GetElement(e.GetTypeId())?.Name ?? "(no type)");
                        break;
                    default:
                        continue;
                }

                foreach (var g in groups.OrderByDescending(g => g.Count()))
                {
                    result.Add(new GroupCount { Key = $"{normalized}:{g.Key}", Count = g.Count() });
                }
            }

            return result;
        }

        private static string GetLevelName(Document doc, Element e)
        {
            if (e.LevelId != null && e.LevelId != ElementId.InvalidElementId)
            {
                var lvl = doc.GetElement(e.LevelId);
                if (lvl != null) return lvl.Name;
            }
            return "(no level)";
        }

        private static string GetWorksetName(Document doc, Element e)
        {
            if (!doc.IsWorkshared) return "(not workshared)";
            try
            {
                var ws = e.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (ws != null && ws.AsInteger() > 0)
                {
                    var wsTable = doc.GetWorksetTable();
                    var wsId = new WorksetId(ws.AsInteger());
                    return wsTable.GetWorkset(wsId)?.Name ?? "(unknown)";
                }
            }
            catch { }
            return "(unknown)";
        }

        private static string GetFamilyName(Document doc, Element e)
        {
            var typeElem = doc.GetElement(e.GetTypeId());
            if (typeElem is FamilySymbol fs && fs.Family != null)
            {
                return fs.Family.Name;
            }
            if (typeElem is ElementType et && !string.IsNullOrWhiteSpace(et.FamilyName))
            {
                return et.FamilyName;
            }
            return "(no family)";
        }

        private static List<ParameterAuditResult> BuildParameterAudit(Document doc, List<Element> elements, List<string> paramNames, bool includeTypeParameters)
        {
            var results = new List<ParameterAuditResult>();

            foreach (var name in paramNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                var audit = new ParameterAuditResult { Name = name };
                audit.Total = elements.Count;
                int filledInstance = 0;
                int filledType = 0;
                int filledAny = 0;
                int emptyCount = 0;
                int emptyExamplesCollected = 0;

                foreach (var elem in elements)
                {
                    bool instFilled = IsParameterFilled(elem, name);
                    bool typeFilled = false;

                    if (includeTypeParameters)
                    {
                        var typeElem = doc.GetElement(elem.GetTypeId());
                        if (typeElem != null)
                        {
                            typeFilled = IsParameterFilled(typeElem, name);
                        }
                    }

                    if (instFilled) filledInstance++;
                    if (typeFilled) filledType++;
                    if (instFilled || typeFilled) filledAny++;
                    else
                    {
                        emptyCount++;
                        if (emptyExamplesCollected < 5)
                        {
                            audit.EmptyExamples.Add(elem.Id.ToLongId());
                            emptyExamplesCollected++;
                        }
                    }
                }

                audit.FilledInstance = filledInstance;
                audit.FilledType = filledType;
                audit.FilledAny = filledAny;
                audit.EmptyCount = emptyCount;
                audit.FillRatePercent = audit.Total > 0 ? Math.Round((double)filledAny / audit.Total * 100.0, 2) : 0.0;

                results.Add(audit);
            }

            return results;
        }

        private static bool IsParameterFilled(Element elem, string name)
        {
            var p = elem.LookupParameter(name);
            if (p == null) return false;
            return p.HasValue && !string.IsNullOrWhiteSpace(GetParameterStringValue(p));
        }

        private static LevelQualityInfo BuildLevelQuality(Document doc, List<Element> elements)
        {
            var info = new LevelQualityInfo();
            int missing = 0;

            foreach (var elem in elements)
            {
                bool hasLevel = elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId;
                if (!hasLevel)
                {
                    // try fallback resolution
                    hasLevel = ResolveElementLevelId(doc, elem) != null;
                }
                if (!hasLevel)
                {
                    missing++;
                }
            }

            info.MissingLevelCount = missing;

            // Detect multi-level walls/columns (base + top constraint differing)
            try
            {
                var multiLevel = new HashSet<long>();
                foreach (var elem in elements)
                {
                    ElementId? baseLevel = null;
                    ElementId? topLevel = null;

                    if (elem is Wall)
                    {
                        baseLevel = elem.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId();
                        topLevel = elem.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.AsElementId();
                    }
                    else if (elem is FamilyInstance)
                    {
                        baseLevel = elem.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.AsElementId();
                        if (baseLevel == null || baseLevel == ElementId.InvalidElementId)
                        {
                            baseLevel = elem.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId();
                        }
                        topLevel = elem.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.AsElementId();
                    }

                    if (baseLevel != null && topLevel != null &&
                        baseLevel != ElementId.InvalidElementId && topLevel != ElementId.InvalidElementId &&
                        baseLevel != topLevel)
                    {
                        multiLevel.Add(elem.Id.ToLongId());
                    }
                }
                info.MultiLevelElements = multiLevel.Take(200).ToList();
            }
            catch
            {
                // optional best-effort
            }

            return info;
        }

        private static ElementId? ResolveCategoryId(Document doc, string catName)
        {
            return CategoryResolver.ResolveCategoryId(doc, catName);
        }

        private static ElementId? ResolveLevelId(Document doc, string levelIdentifier)
        {
            if (long.TryParse(levelIdentifier, out long idVal))
            {
                return ElementIdExtensions.FromLongId(idVal);
            }

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>();

            var match = levels.FirstOrDefault(l => string.Equals(l.Name, levelIdentifier, StringComparison.OrdinalIgnoreCase));
            return match?.Id;
        }

        /// <summary>
        /// Resolves a level for an element that has no direct LevelId, using the
        /// fallback parameter chain proven in the hospital audit (MSG 73/75).
        /// </summary>
        private static ElementId? ResolveElementLevelId(Document doc, Element elem)
        {
            if (elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId)
            {
                return elem.LevelId;
            }

            var bipCandidates = new[]
            {
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                BuiltInParameter.STAIRS_BASE_LEVEL_PARAM,
                BuiltInParameter.RBS_START_LEVEL_PARAM,
                BuiltInParameter.WALL_BASE_CONSTRAINT,
                BuiltInParameter.ROOF_BASE_LEVEL_PARAM,
                BuiltInParameter.FAMILY_BASE_LEVEL_PARAM
            };

            foreach (var bip in bipCandidates)
            {
                try
                {
                    var p = elem.get_Parameter(bip);
                    if (p != null && p.HasValue && p.StorageType == StorageType.ElementId)
                    {
                        var levelId = p.AsElementId();
                        if (levelId != null && levelId != ElementId.InvalidElementId)
                        {
                            return levelId;
                        }
                    }
                }
                catch { }
            }

            // Host fallback (e.g. door/window hosted on a wall that has a level)
            try
            {
                if (elem is FamilyInstance fi && fi.Host != null && fi.Host != elem)
                {
                    return ResolveElementLevelId(doc, fi.Host);
                }
            }
            catch { }

            // StairsRun / StairsLanding -> parent Stairs
            try
            {
                if (elem is StairsRun run && run.GetStairs() != null)
                {
                    return ResolveElementLevelId(doc, run.GetStairs());
                }
                if (elem is StairsLanding landing && landing.GetStairs() != null)
                {
                    return ResolveElementLevelId(doc, landing.GetStairs());
                }
            }
            catch { }

            return null;
        }

        private static bool MatchesParameterFilters(Document doc, Element elem, List<ParameterFilterRule> rules, bool includeTypeParameters)
        {
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Name)) continue;

                Parameter? p = elem.LookupParameter(rule.Name);
                if (p == null && includeTypeParameters)
                {
                    var typeElem = doc.GetElement(elem.GetTypeId());
                    if (typeElem != null)
                    {
                        p = typeElem.LookupParameter(rule.Name);
                    }
                }
                if (p == null) return false;

                string valStr = GetParameterStringValue(p);
                string expectedStr = rule.Value?.ToString() ?? string.Empty;
                string op = rule.Operator?.ToLowerInvariant() ?? "equals";

                switch (op)
                {
                    case "equals":
                        if (!string.Equals(valStr, expectedStr, StringComparison.OrdinalIgnoreCase)) return false;
                        break;
                    case "notequals":
                        if (string.Equals(valStr, expectedStr, StringComparison.OrdinalIgnoreCase)) return false;
                        break;
                    case "contains":
                        if (valStr.IndexOf(expectedStr, StringComparison.OrdinalIgnoreCase) < 0) return false;
                        break;
                    case "startswith":
                        if (!valStr.StartsWith(expectedStr, StringComparison.OrdinalIgnoreCase)) return false;
                        break;
                    case "greaterthan":
                        if (double.TryParse(valStr, out double actualNum) && double.TryParse(expectedStr, out double expNum))
                        {
                            if (actualNum <= expNum) return false;
                        }
                        else return false;
                        break;
                    case "lessthan":
                        if (double.TryParse(valStr, out actualNum) && double.TryParse(expectedStr, out expNum))
                        {
                            if (actualNum >= expNum) return false;
                        }
                        else return false;
                        break;
                }
            }

            return true;
        }

        private static string GetParameterStringValue(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString() ?? string.Empty;
                case StorageType.Integer:
                    return p.AsInteger().ToString();
                case StorageType.Double:
                    return p.AsDouble().ToString("G");
                case StorageType.ElementId:
                    return p.AsElementId()?.ToLongId().ToString() ?? "-1";
                default:
                    return p.AsValueString() ?? string.Empty;
            }
        }

        private static ElementDto MapElementDto(Document doc, Element elem, List<string>? fields, bool includeParameters, List<string>? parameterNames)
        {
            bool includeAll = fields == null || fields.Count == 0;
            bool includeBb = includeAll || (fields != null && fields.Contains("boundingbox"));

            var dto = new ElementDto
            {
                Id = elem.Id.ToLongId(),
                Category = elem.Category?.Name ?? "Uncategorized"
            };

            var typeElem = doc.GetElement(elem.GetTypeId());
            if (typeElem is ElementType et)
            {
                dto.TypeName = et.Name;
                if (et is FamilySymbol fs && fs.Family != null)
                {
                    dto.FamilyName = fs.Family.Name;
                }
            }

            if (elem.LevelId != null && elem.LevelId != ElementId.InvalidElementId)
            {
                var lvl = doc.GetElement(elem.LevelId);
                dto.Level = lvl?.Name;
            }

            if (includeBb)
            {
                var bb = elem.get_BoundingBox(null);
                if (bb != null)
                {
                    dto.BoundingBox = new BoundingBoxQueryDto
                    {
                        Min = new double[] { bb.Min.X * FeetToMm, bb.Min.Y * FeetToMm, bb.Min.Z * FeetToMm },
                        Max = new double[] { bb.Max.X * FeetToMm, bb.Max.Y * FeetToMm, bb.Max.Z * FeetToMm }
                    };
                }
            }

            if (includeParameters)
            {
                var dict = new Dictionary<string, object?>();
                foreach (Parameter p in elem.Parameters)
                {
                    if (p.HasValue && !string.IsNullOrWhiteSpace(p.Definition?.Name))
                    {
                        string pName = p.Definition!.Name;
                        if (parameterNames != null && parameterNames.Count > 0 &&
                            !parameterNames.Any(n => string.Equals(n, pName, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }
                        if (!dict.ContainsKey(pName))
                        {
                            dict[pName] = GetParameterObjectValue(p);
                        }
                    }
                }
                dto.Parameters = dict;
            }

            return dto;
        }

        private static object? GetParameterObjectValue(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString();
                case StorageType.Integer:
                    return p.AsInteger();
                case StorageType.Double:
                    return p.AsDouble();
                case StorageType.ElementId:
                    return p.AsElementId()?.ToLongId();
                default:
                    return p.AsValueString();
            }
        }
    }
}
