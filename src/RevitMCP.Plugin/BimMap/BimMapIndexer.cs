using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.BimMap;
using RevitMCP.Shared.Units;

namespace RevitMCP.Plugin.BimMap
{
    public static class BimMapIndexer
    {
        public static string GetProjectGuid(Document doc)
        {
            try
            {
                var guid = ExportUtils.GetGBXMLDocumentId(doc);
                if (guid != Guid.Empty)
                {
                    return guid.ToString();
                }
            }
            catch { }

            // Fallback: Deterministic MD5 hash of Title and Path
            string seed = $"{doc.Title}:{doc.PathName ?? string.Empty}";
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
                return new Guid(hash).ToString();
            }
        }

        public static string GetVersionGuid(Document doc)
        {
            try
            {
                var v = Document.GetDocumentVersion(doc);
                if (v.VersionGUID != Guid.Empty)
                {
                    return v.VersionGUID.ToString();
                }
            }
            catch { }

            return Guid.NewGuid().ToString();
        }

        public static RebuildBimMapResult IndexDocument(Document doc, bool forceRebuild = false)
        {
            var sw = Stopwatch.StartNew();
            string projectGuid = GetProjectGuid(doc);
            string versionGuid = GetVersionGuid(doc);

            using (var db = new BimMapDatabase(projectGuid))
            {
                db.InitializeSchema();

                // If not forcing rebuild and version is current, return existing stats
                if (!forceRebuild)
                {
                    string? cachedVersion = db.GetMetadata("version_guid");
                    if (string.Equals(cachedVersion, versionGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        var topo = db.GetTopology();
                        var spat = db.GetSpatial();
                        sw.Stop();

                        long dbSize = 0;
                        if (File.Exists(db.DatabasePath))
                        {
                            dbSize = new FileInfo(db.DatabasePath).Length;
                        }

                        return new RebuildBimMapResult
                        {
                            Success = true,
                            ProjectGuid = projectGuid,
                            VersionGuid = versionGuid,
                            DatabasePath = db.DatabasePath,
                            DatabaseSizeBytes = dbSize,
                            LevelCount = topo.Levels.Count,
                            GridCount = topo.Grids.Count,
                            RoomCount = spat.Rooms.Count,
                            ElementCount = topo.CategoryCounts.Values.Sum(),
                            ElapsedMs = sw.ElapsedMilliseconds
                        };
                    }
                }

                db.ClearAll();

                // 1. Save Metadata
                var metadata = new Dictionary<string, string>
                {
                    ["project_guid"] = projectGuid,
                    ["version_guid"] = versionGuid,
                    ["document_title"] = doc.Title,
                    ["document_path"] = doc.PathName ?? string.Empty,
                    ["revit_version"] = doc.Application.VersionNumber,
                    ["last_indexed_utc"] = DateTime.UtcNow.ToString("o")
                };
                db.SaveMetadata(metadata);

                // 2. Topology: Levels, Grids, Category counts
                var topoData = new BimMapTopology();

                // Levels
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                foreach (var lvl in levels)
                {
                    topoData.Levels.Add(new BimMapLevel
                    {
                        Id = lvl.Id.ToLongId(),
                        Name = lvl.Name,
                        ElevationMm = Math.Round(lvl.Elevation * RevitUnitConverter.FeetToMmRatio, 2)
                    });
                }

                // Grids
                var grids = new FilteredElementCollector(doc)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .OrderBy(g => g.Name)
                    .ToList();

                foreach (var g in grids)
                {
                    var mapGrid = new BimMapGrid
                    {
                        Id = g.Id.ToLongId(),
                        Name = g.Name
                    };

                    Curve? curve = g.Curve;
                    if (curve != null)
                    {
                        mapGrid.IsCurved = !(curve is Line);
                        XYZ p0 = curve.GetEndPoint(0);
                        XYZ p1 = curve.GetEndPoint(1);
                        mapGrid.StartXMm = Math.Round(p0.X * RevitUnitConverter.FeetToMmRatio, 2);
                        mapGrid.StartYMm = Math.Round(p0.Y * RevitUnitConverter.FeetToMmRatio, 2);
                        mapGrid.EndXMm = Math.Round(p1.X * RevitUnitConverter.FeetToMmRatio, 2);
                        mapGrid.EndYMm = Math.Round(p1.Y * RevitUnitConverter.FeetToMmRatio, 2);
                    }

                    topoData.Grids.Add(mapGrid);
                }

                // Links
                var links = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in links)
                {
                    var linkType = doc.GetElement(link.GetTypeId()) as RevitLinkType;
                    topoData.Links.Add(new BimMapLink
                    {
                        Id = link.Id.ToLongId(),
                        Title = link.Name,
                        PathName = linkType != null ? linkType.GetExternalFileReference()?.GetPath()?.ToString() ?? string.Empty : string.Empty,
                        IsLoaded = linkType != null && RevitLinkType.IsLoaded(doc, linkType.Id)
                    });
                }

                // Category counts
                var allInstances = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var catCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var elem in allInstances)
                {
                    if (elem.Category != null && !string.IsNullOrWhiteSpace(elem.Category.Name))
                    {
                        string cName = elem.Category.Name;
                        catCounts[cName] = catCounts.TryGetValue(cName, out int cnt) ? cnt + 1 : 1;
                    }
                }
                topoData.CategoryCounts = catCounts;
                db.SaveTopology(topoData);

                // 3. Spatial: Rooms
                var spatialData = new BimMapSpatial();
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .ToList();

                foreach (var rm in rooms)
                {
                    double volume = rm.Volume;
                    var mapRoom = new BimMapRoom
                    {
                        Id = rm.Id.ToLongId(),
                        Number = rm.Number ?? string.Empty,
                        Name = rm.Name ?? string.Empty,
                        LevelId = rm.LevelId?.ToLongId(),
                        LevelName = rm.Level?.Name,
                        AreaM2 = Math.Round(rm.Area * RevitUnitConverter.SqFeetToSqMetersRatio, 2),
                        VolumeM3 = Math.Round(volume * RevitUnitConverter.CuFeetToCuMetersRatio, 2)
                    };

                    var bb = rm.get_BoundingBox(null);
                    if (bb != null)
                    {
                        mapRoom.BoundingBox = new BimMapBoundingBox
                        {
                            Min = new[]
                            {
                                Math.Round(bb.Min.X * RevitUnitConverter.FeetToMmRatio, 1),
                                Math.Round(bb.Min.Y * RevitUnitConverter.FeetToMmRatio, 1),
                                Math.Round(bb.Min.Z * RevitUnitConverter.FeetToMmRatio, 1)
                            },
                            Max = new[]
                            {
                                Math.Round(bb.Max.X * RevitUnitConverter.FeetToMmRatio, 1),
                                Math.Round(bb.Max.Y * RevitUnitConverter.FeetToMmRatio, 1),
                                Math.Round(bb.Max.Z * RevitUnitConverter.FeetToMmRatio, 1)
                            }
                        };
                    }

                    spatialData.Rooms.Add(mapRoom);
                }
                db.SaveSpatial(spatialData);

                // 4. Elements: Streaming batch indexation
                var batch = new List<BimMapElementRecord>(1000);
                int totalIndexedElements = 0;

                foreach (var elem in allInstances)
                {
                    if (elem.Category == null) continue;

                    string catName = elem.Category.Name;
                    // Skip internal non-model elements
                    if (catName.StartsWith("<") || catName.StartsWith("OST_")) continue;

                    string familyName = string.Empty;
                    string typeName = string.Empty;
                    var typeId = elem.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        var elemType = doc.GetElement(typeId) as ElementType;
                        if (elemType != null)
                        {
                            familyName = elemType.FamilyName ?? string.Empty;
                            typeName = elemType.Name ?? string.Empty;
                        }
                    }

                    long? roomId = null;
                    string? roomName = null;
                    if (elem is FamilyInstance fi)
                    {
                        try
                        {
                            var rm = fi.Room;
                            if (rm != null)
                            {
                                roomId = rm.Id.ToLongId();
                                roomName = rm.Name;
                            }
                        }
                        catch
                        {
                            // Ignore phase mismatch exception: "The target instance does not exist in the given phase."
                        }
                    }

                    string? mark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                    string? comments = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();

                    var record = new BimMapElementRecord
                    {
                        Id = elem.Id.ToLongId(),
                        CategoryName = catName,
                        FamilyName = familyName,
                        TypeName = typeName,
                        LevelId = elem.LevelId?.ToLongId(),
                        LevelName = elem.Document.GetElement(elem.LevelId)?.Name,
                        RoomId = roomId,
                        RoomName = roomName,
                        Mark = mark,
                        Comments = comments
                    };

                    var bb = elem.get_BoundingBox(null);
                    if (bb != null)
                    {
                        record.BoundingBoxMin = new[]
                        {
                            Math.Round(bb.Min.X * RevitUnitConverter.FeetToMmRatio, 1),
                            Math.Round(bb.Min.Y * RevitUnitConverter.FeetToMmRatio, 1),
                            Math.Round(bb.Min.Z * RevitUnitConverter.FeetToMmRatio, 1)
                        };
                        record.BoundingBoxMax = new[]
                        {
                            Math.Round(bb.Max.X * RevitUnitConverter.FeetToMmRatio, 1),
                            Math.Round(bb.Max.Y * RevitUnitConverter.FeetToMmRatio, 1),
                            Math.Round(bb.Max.Z * RevitUnitConverter.FeetToMmRatio, 1)
                        };
                    }

                    batch.Add(record);
                    totalIndexedElements++;

                    if (batch.Count >= 1000)
                    {
                        db.SaveElementsBatch(batch);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    db.SaveElementsBatch(batch);
                    batch.Clear();
                }

                db.OptimizeAndTruncateWal();
                sw.Stop();

                long finalSize = 0;
                if (File.Exists(db.DatabasePath))
                {
                    finalSize = new FileInfo(db.DatabasePath).Length;
                }

                return new RebuildBimMapResult
                {
                    Success = true,
                    ProjectGuid = projectGuid,
                    VersionGuid = versionGuid,
                    DatabasePath = db.DatabasePath,
                    DatabaseSizeBytes = finalSize,
                    LevelCount = topoData.Levels.Count,
                    GridCount = topoData.Grids.Count,
                    RoomCount = spatialData.Rooms.Count,
                    ElementCount = totalIndexedElements,
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }
    }
}
