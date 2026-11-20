using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.Commands
{
    public static class InspectSchemaHandler
    {
        private static readonly string[] AllSections =
        {
            "categories", "project_info", "units", "coordinates", "grids_levels",
            "worksets", "links_imports", "shared_params", "warnings", "rooms"
        };

        public static InspectSchemaResult Execute(Document doc, InspectSchemaRequest request)
        {
            if (doc == null)
            {
                throw new InvalidOperationException("No active document.");
            }

            var result = new InspectSchemaResult();
            var sections = ResolveSections(request.Sections);

            if (sections.Contains("categories"))
            {
                BuildCategories(doc, request, result);
            }
            if (sections.Contains("project_info")) result.ProjectInfo = BuildProjectInfo(doc);
            if (sections.Contains("units")) result.Units = BuildUnits(doc);
            if (sections.Contains("coordinates")) result.Coordinates = BuildCoordinates(doc);
            if (sections.Contains("grids_levels")) result.GridsLevels = BuildGridsLevels(doc);
            if (sections.Contains("worksets")) result.Worksets = BuildWorksets(doc);
            if (sections.Contains("links_imports")) result.LinksImports = BuildLinksImports(doc);
            if (sections.Contains("shared_params")) result.SharedParams = BuildSharedParams(doc);
            if (sections.Contains("warnings")) result.Warnings = BuildWarnings(doc);
            if (sections.Contains("rooms")) result.Rooms = BuildRooms(doc);

            return result;
        }

        private static HashSet<string> ResolveSections(List<string>? requested)
        {
            // Backward compatible: no sections -> old behavior (categories only)
            if (requested == null || requested.Count == 0)
            {
                return new HashSet<string> { "categories" };
            }

            var set = new HashSet<string>(requested.Select(s => s.Trim().ToLowerInvariant()));
            if (set.Contains("all"))
            {
                return new HashSet<string>(AllSections);
            }
            return set;
        }

        private static void BuildCategories(Document doc, InspectSchemaRequest request, InspectSchemaResult result)
        {
            var categoriesToInspect = ResolveCategories(doc, request.Category);

            foreach (var cat in categoriesToInspect)
            {
                long catIdLong = cat.Id.ToLongId();
                string builtInCatName = string.Empty;
                try
                {
#if REVIT_2019 || REVIT_2020 || REVIT_2021 || REVIT_2022 || REVIT_2023
                    builtInCatName = ((BuiltInCategory)cat.Id.IntegerValue).ToString();
#else
                    builtInCatName = ((BuiltInCategory)cat.Id.Value).ToString();
#endif
                }
                catch
                {
                    builtInCatName = cat.Name;
                }

                int elementCount = 0;
                try
                {
                    elementCount = new FilteredElementCollector(doc)
                        .OfCategoryId(cat.Id)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                }
                catch { }

                var catInfo = new CategorySchemaInfo
                {
                    Id = catIdLong,
                    Name = cat.Name,
                    BuiltInCategory = builtInCatName,
                    ElementCount = elementCount
                };

                var elementTypes = new FilteredElementCollector(doc)
                    .OfCategoryId(cat.Id)
                    .WhereElementIsElementType()
                    .Cast<ElementType>()
                    .ToList();

                if (!string.IsNullOrWhiteSpace(request.Family))
                {
                    elementTypes = elementTypes
                        .Where(et => string.Equals(GetFamilyName(et), request.Family, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var familyGroups = elementTypes.GroupBy(GetFamilyName);

                foreach (var group in familyGroups)
                {
                    string famName = group.Key;
                    var famInfo = new FamilySchemaInfo
                    {
                        Name = famName
                    };

                    foreach (var symbol in group)
                    {
                        var symInfo = new SymbolSchemaInfo
                        {
                            Id = symbol.Id.ToLongId(),
                            Name = symbol.Name
                        };

                        if (request.IncludeParameters)
                        {
                            symInfo.Parameters = ExtractParameters(symbol);
                        }

                        famInfo.Symbols.Add(symInfo);
                    }

                    if (famInfo.Symbols.Count > 0)
                    {
                        famInfo.Id = famInfo.Symbols[0].Id;
                    }

                    catInfo.Families.Add(famInfo);
                }

                result.Categories.Add(catInfo);
            }
        }

        private static ProjectInfoSection? BuildProjectInfo(Document doc)
        {
            try
            {
                var pi = doc.ProjectInformation;
                var info = new ProjectInfoSection
                {
                    Name = GetProjectInfoString(pi, BuiltInParameter.PROJECT_NAME),
                    Number = GetProjectInfoString(pi, BuiltInParameter.PROJECT_NUMBER),
                    Address = GetProjectInfoString(pi, BuiltInParameter.PROJECT_ADDRESS),
                    ClientName = pi.ClientName,
                    Author = GetProjectInfoString(pi, BuiltInParameter.PROJECT_AUTHOR),
                    Status = GetProjectInfoString(pi, BuiltInParameter.PROJECT_STATUS),
                    DocTitle = doc.Title,
                    PathName = doc.PathName,
                    IsWorkshared = doc.IsWorkshared,
                    IsDetached = doc.IsDetached
                };

                if (doc.IsWorkshared)
                {
                    try
                    {
                        var centralPath = doc.GetWorksharingCentralModelPath();
                        if (centralPath != null)
                        {
                            info.CentralModelPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);
                        }
                    }
                    catch { }
                }

                return info;
            }
            catch
            {
                return null;
            }
        }

        private static string? GetProjectInfoString(Element pi, BuiltInParameter bip)
        {
            try
            {
                var p = pi.get_Parameter(bip);
                return p?.AsString();
            }
            catch
            {
                return null;
            }
        }

        private static UnitsSection? BuildUnits(Document doc)
        {
            var section = new UnitsSection
            {
                Length = GetUnitFormat(doc, "length"),
                Area = GetUnitFormat(doc, "area"),
                Volume = GetUnitFormat(doc, "volume"),
                Angle = GetUnitFormat(doc, "angle"),
                Slope = GetUnitFormat(doc, "slope")
            };
            return section;
        }

        private static UnitFormatInfo? GetUnitFormat(Document doc, string kind)
        {
            try
            {
                var units = doc.GetUnits();
#if REVIT_2019 || REVIT_2020
                UnitType ut;
                switch (kind)
                {
                    case "length": ut = UnitType.UT_Length; break;
                    case "area": ut = UnitType.UT_Area; break;
                    case "volume": ut = UnitType.UT_Volume; break;
                    case "angle": ut = UnitType.UT_Angle; break;
                    case "slope": ut = UnitType.UT_Slope; break;
                    default: return null;
                }
                var fo = units.GetFormatOptions(ut);
                string unitName = fo.DisplayUnits.ToString();
                return new UnitFormatInfo
                {
                    UnitTypeId = unitName,
                    Symbol = unitName,
                    Accuracy = fo.Accuracy,
                    SuppressSpaces = fo.SuppressSpaces
                };
#else
                ForgeTypeId spec;
                switch (kind)
                {
                    case "length": spec = SpecTypeId.Length; break;
                    case "area": spec = SpecTypeId.Area; break;
                    case "volume": spec = SpecTypeId.Volume; break;
                    case "angle": spec = SpecTypeId.Angle; break;
                    case "slope": spec = SpecTypeId.Slope; break;
                    default: return null;
                }
                var fo = units.GetFormatOptions(spec);
                return new UnitFormatInfo
                {
                    UnitTypeId = fo.GetUnitTypeId()?.TypeId,
                    Symbol = fo.GetSymbolTypeId()?.TypeId,
                    Accuracy = fo.Accuracy,
                    SuppressSpaces = fo.SuppressSpaces
                };
#endif
            }
            catch
            {
                return null;
            }
        }

        private static CoordinatesSection? BuildCoordinates(Document doc)
        {
            try
            {
                var section = new CoordinatesSection();

                var siteLoc = doc.SiteLocation;
                if (siteLoc != null)
                {
                    section.LatitudeDeg = RevitUnitConverter.RadiansToDegrees(siteLoc.Latitude);
                    section.LongitudeDeg = RevitUnitConverter.RadiansToDegrees(siteLoc.Longitude);
                    section.TimeZone = siteLoc.TimeZone;
                }

                var projectPos = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
                section.ProjectPosition = new ProjectPositionInfo
                {
                    EastWestMeters = RevitUnitConverter.ConvertFromInternal(projectPos.EastWest, "m"),
                    NorthSouthMeters = RevitUnitConverter.ConvertFromInternal(projectPos.NorthSouth, "m"),
                    ElevationMeters = RevitUnitConverter.ConvertFromInternal(projectPos.Elevation, "m"),
                    AngleTrueNorthDeg = RevitUnitConverter.RadiansToDegrees(projectPos.Angle)
                };

                section.ProjectBasePoint = GetBasePointInfo(doc, true);
                section.SurveyPoint = GetBasePointInfo(doc, false);

                return section;
            }
            catch
            {
                return null;
            }
        }

        private static BasePointInfo? GetBasePointInfo(Document doc, bool projectBasePoint)
        {
            try
            {
                BasePoint? bp;
#if REVIT_2019 || REVIT_2020
                bp = projectBasePoint
                    ? new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ProjectBasePoint).Cast<BasePoint>().FirstOrDefault()
                    : new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_SharedBasePoint).Cast<BasePoint>().FirstOrDefault();
#else
                bp = projectBasePoint
                    ? BasePoint.GetProjectBasePoint(doc)
                    : BasePoint.GetSurveyPoint(doc);
#endif
                if (bp == null) return null;

                double? ew = GetBasePointParam(bp, BuiltInParameter.BASEPOINT_EASTWEST_PARAM);
                double? ns = GetBasePointParam(bp, BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM);
                double? elev = GetBasePointParam(bp, BuiltInParameter.BASEPOINT_ELEVATION_PARAM);
                double? angle = GetBasePointParam(bp, BuiltInParameter.BASEPOINT_ANGLETON_PARAM);

                return new BasePointInfo
                {
                    Id = bp.Id.ToLongId(),
                    Name = bp.Name,
                    EastWestMeters = ew.HasValue ? RevitUnitConverter.ConvertFromInternal(ew.Value, "m") : (double?)null,
                    NorthSouthMeters = ns.HasValue ? RevitUnitConverter.ConvertFromInternal(ns.Value, "m") : (double?)null,
                    ElevationMeters = elev.HasValue ? RevitUnitConverter.ConvertFromInternal(elev.Value, "m") : (double?)null,
                    AngleTrueNorthDeg = angle.HasValue ? RevitUnitConverter.RadiansToDegrees(angle.Value) : (double?)null,
                    Pinned = bp.Pinned
                };
            }
            catch
            {
                return null;
            }
        }

        private static double? GetBasePointParam(BasePoint bp, BuiltInParameter bip)
        {
            try
            {
                var p = bp.get_Parameter(bip);
                if (p != null && p.HasValue) return p.AsDouble();
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static GridsLevelsSection? BuildGridsLevels(Document doc)
        {
            try
            {
                var section = new GridsLevelsSection();

                foreach (Level level in new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation))
                {
                    section.Levels.Add(new LevelInfo
                    {
                        Id = level.Id.ToLongId(),
                        Name = level.Name,
                        ElevationMm = RevitUnitConverter.FeetToMm(level.Elevation)
                    });
                }

                int nonOrthogonal = 0;
                foreach (Grid grid in new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>())
                {
                    bool isOrthogonal = true;
                    double[]? direction = null;

                    try
                    {
                        var curve = grid.Curve;
                        if (curve is Line line)
                        {
                            var d = line.Direction;
                            direction = new[] { d.X, d.Y, d.Z };
                            bool alongX = Math.Abs(Math.Abs(d.X) - 1.0) < 1e-6 && Math.Abs(d.Y) < 1e-6;
                            bool alongY = Math.Abs(Math.Abs(d.Y) - 1.0) < 1e-6 && Math.Abs(d.X) < 1e-6;
                            isOrthogonal = alongX || alongY;
                        }
                        else
                        {
                            isOrthogonal = false; // arc / curved grid
                        }
                    }
                    catch
                    {
                        isOrthogonal = false;
                    }

                    if (!isOrthogonal) nonOrthogonal++;

                    section.Grids.Add(new GridInfo
                    {
                        Id = grid.Id.ToLongId(),
                        Name = grid.Name,
                        IsOrthogonal = isOrthogonal,
                        Direction = direction
                    });
                }

                section.NonOrthogonalGridCount = nonOrthogonal;
                return section;
            }
            catch
            {
                return null;
            }
        }

        private static WorksetsSection? BuildWorksets(Document doc)
        {
            try
            {
                if (!doc.IsWorkshared) return null;

                var section = new WorksetsSection();
                var worksets = new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .ToList();

                foreach (var ws in worksets)
                {
                    section.Worksets.Add(new WorksetInfo
                    {
                        Id = GetWorksetId(ws),
                        Name = ws.Name,
                        IsOpen = ws.IsOpen,
                        IsDefault = IsDefaultWorkset(ws.Name),
                        IsEditable = ws.IsEditable
                    });
                }

                section.Count = section.Worksets.Count;
                return section;
            }
            catch
            {
                return null;
            }
        }

        private static long GetWorksetId(Workset ws)
        {
            try
            {
                // WorksetId.IntegerValue is int-based in all Revit versions.
                return ws.Id.IntegerValue;
            }
            catch
            {
                return -1;
            }
        }

        private static bool IsDefaultWorkset(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name!.Trim();
            return n == "Workset1" || n == "Shared Levels and Grids";
        }

        private static LinksImportsSection? BuildLinksImports(Document doc)
        {
            try
            {
                var section = new LinksImportsSection();

                foreach (RevitLinkInstance link in new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
                {
                    string? status = null;
                    string? path = null;
                    string? attachment = null;
                    bool isNested = false;

                    if (doc.GetElement(link.GetTypeId()) is RevitLinkType lt)
                    {
                        try { status = lt.GetLinkedFileStatus().ToString(); } catch { }
                        try { path = lt.GetExternalFileReference() != null ? ModelPathUtils.ConvertModelPathToUserVisiblePath(lt.GetExternalFileReference().GetAbsolutePath()) : null; } catch { }
                        attachment = lt.AttachmentType.ToString();
                        isNested = lt.IsNestedLink;
                    }

                    section.Links.Add(new LinkInfo
                    {
                        Id = link.Id.ToLongId(),
                        Name = link.Name,
                        Path = path,
                        LinkStatus = status,
                        AttachmentType = attachment,
                        IsNested = isNested
                    });
                }

                int linked = 0, embedded = 0;
                foreach (ImportInstance imp in new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance)).Cast<ImportInstance>())
                {
                    if (doc.GetElement(imp.GetTypeId()) is CADLinkType) linked++;
                    else embedded++;
                }
                section.ImportInstances.Linked = linked;
                section.ImportInstances.Embedded = embedded;
                section.ImportInstances.Total = linked + embedded;

                try
                {
                    section.ImageTypesCount = new FilteredElementCollector(doc)
                        .OfClass(typeof(ImageType)).GetElementCount();
                }
                catch { }

                try
                {
                    section.InPlaceFamilyCount = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.Symbol?.Family != null && fi.Symbol.Family.IsInPlace)
                        .Select(fi => fi.Symbol!.Family!.Id)
                        .Distinct()
                        .Count();
                }
                catch { }

                return section;
            }
            catch
            {
                return null;
            }
        }

        private static SharedParamsSection? BuildSharedParams(Document doc)
        {
            try
            {
                var section = new SharedParamsSection();
                var iterator = doc.ParameterBindings.ForwardIterator();

                while (iterator.MoveNext())
                {
                    Definition def = iterator.Key;
                    var binding = doc.ParameterBindings.get_Item(def);
                    if (binding == null) continue;

                    bool isShared = def is ExternalDefinition;
                    string? guid = null;
                    if (isShared)
                    {
                        guid = ((ExternalDefinition)def).GUID.ToString();
                    }

                    string bindingType = binding is InstanceBinding ? "Instance" : "Type";

                    var categories = new List<string>();
                    if (binding is InstanceBinding ib)
                    {
                        foreach (Category c in ib.Categories)
                        {
                            categories.Add(c.Name);
                        }
                    }

                    section.Bindings.Add(new SharedParamBinding
                    {
                        Name = def.Name,
                        IsShared = isShared,
                        Guid = guid,
                        BindingType = bindingType,
                        Categories = categories
                    });
                }

                section.Count = section.Bindings.Count;
                return section;
            }
            catch
            {
                return null;
            }
        }

        private static WarningsSection? BuildWarnings(Document doc)
        {
            try
            {
                var warnings = doc.GetWarnings();
                if (warnings == null) return null;

                var groups = warnings
                    .GroupBy(w => w.GetDescriptionText())
                    .Select(g => new WarningGroup
                    {
                        DefinitionId = g.First().GetFailureDefinitionId().Guid.ToString(),
                        Message = g.Key,
                        Count = g.Count(),
                        TopElementIds = g.First().GetFailingElements().Take(3).Select(e => e.ToLongId()).ToList()
                    })
                    .OrderByDescending(g => g.Count)
                    .ToList();

                return new WarningsSection
                {
                    TotalCount = warnings.Count,
                    Groups = groups
                };
            }
            catch
            {
                return null;
            }
        }

        private static RoomsSection? BuildRooms(Document doc)
        {
            try
            {
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .ToList();

                int total = rooms.Count;
                int placed = 0;
                int zeroOrNegative = 0;

                foreach (var room in rooms)
                {
                    if (room.Location != null)
                    {
                        placed++;
                        try
                        {
                            if (room.Area <= 0) zeroOrNegative++;
                        }
                        catch { }
                    }
                }

                return new RoomsSection
                {
                    Total = total,
                    Placed = placed,
                    ZeroOrNegativeArea = zeroOrNegative,
                    Unplaced = total - placed
                };
            }
            catch
            {
                return null;
            }
        }

        private static List<Category> ResolveCategories(Document doc, string? categoryFilter)
        {
            var matched = new List<Category>();
            var allCategories = doc.Settings.Categories.Cast<Category>().ToList();

            if (string.IsNullOrWhiteSpace(categoryFilter))
            {
                return allCategories
                    .Where(c => c.CategoryType == CategoryType.Model && !c.IsTagCategory && c.CanAddSubcategory)
                    .OrderBy(c => c.Name)
                    .ToList();
            }

            string filter = categoryFilter!.Trim();

            if (Enum.TryParse<BuiltInCategory>(filter, true, out var bic) ||
                Enum.TryParse<BuiltInCategory>("OST_" + filter, true, out bic))
            {
                var cat = doc.Settings.Categories.get_Item(bic);
                if (cat != null)
                {
                    matched.Add(cat);
                    return matched;
                }
            }

            var byName = allCategories
                .Where(c => string.Equals(c.Name, filter, StringComparison.OrdinalIgnoreCase) ||
                            c.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (byName.Count > 0)
            {
                return byName;
            }

            return matched;
        }

        private static string GetFamilyName(ElementType et)
        {
            if (et is FamilySymbol fs && fs.Family != null)
            {
                return fs.Family.Name;
            }
            return et.FamilyName ?? et.GetType().Name;
        }

        private static List<ParameterSchemaInfo> ExtractParameters(ElementType symbol)
        {
            var list = new List<ParameterSchemaInfo>();
            foreach (Parameter param in symbol.Parameters)
            {
                if (param == null || param.Definition == null) continue;

                string builtInName = string.Empty;
                if (param.Definition is InternalDefinition id)
                {
                    builtInName = id.BuiltInParameter.ToString();
                }

                string unitType = string.Empty;
                try
                {
#if REVIT_2019 || REVIT_2020
                    unitType = param.DisplayUnitType.ToString();
#else
                    unitType = param.GetUnitTypeId()?.TypeId ?? string.Empty;
#endif
                }
                catch { }

                list.Add(new ParameterSchemaInfo
                {
                    Name = param.Definition.Name,
                    BuiltInParameter = string.IsNullOrWhiteSpace(builtInName) || builtInName.StartsWith("INVALID") ? null : builtInName,
                    StorageType = param.StorageType.ToString(),
                    IsReadOnly = param.IsReadOnly,
                    UnitType = string.IsNullOrWhiteSpace(unitType) ? null : unitType
                });
            }

            return list.OrderBy(p => p.Name).ToList();
        }
    }
}

