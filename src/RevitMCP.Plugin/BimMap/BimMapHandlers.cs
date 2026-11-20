using System;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Shared.BimMap;

namespace RevitMCP.Plugin.BimMap
{
    public static class BimMapHandlers
    {
        public static BimMapResult HandleGetBimMap(UIApplication uiapp, GetBimMapRequest req)
        {
            var doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null)
            {
                return new BimMapResult { Success = false, ErrorMessage = "No active Revit document." };
            }

            string projectGuid = BimMapIndexer.GetProjectGuid(doc);
            string versionGuid = BimMapIndexer.GetVersionGuid(doc);

            if (req.ForceRefresh || !BimMapDatabase.Exists(projectGuid))
            {
                BimMapIndexer.IndexDocument(doc, forceRebuild: req.ForceRefresh);
            }

            using (var db = new BimMapDatabase(projectGuid))
            {
                string? cachedVersion = db.GetMetadata("version_guid");
                if (!string.Equals(cachedVersion, versionGuid, StringComparison.OrdinalIgnoreCase))
                {
                    BimMapIndexer.IndexDocument(doc, forceRebuild: false);
                }

                string scope = (req.Scope ?? "overview").ToLowerInvariant();
                var result = new BimMapResult
                {
                    Success = true,
                    ProjectGuid = projectGuid,
                    VersionGuid = versionGuid,
                    DocumentTitle = doc.Title,
                    Scope = scope,
                    LastIndexedUtc = DateTime.TryParse(db.GetMetadata("last_indexed_utc"), out var dt) ? dt : DateTime.UtcNow
                };

                if (scope == "overview" || scope == "all")
                {
                    result.Topology = db.GetTopology();
                }

                if (scope == "spatial" || scope == "all")
                {
                    result.Spatial = db.GetSpatial(req.LevelId);
                }

                return result;
            }
        }

        public static SearchBimMapResult HandleSearchBimMap(UIApplication uiapp, SearchBimMapRequest req)
        {
            var sw = Stopwatch.StartNew();
            var doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null)
            {
                return new SearchBimMapResult { Success = false, ErrorMessage = "No active Revit document." };
            }

            string projectGuid = BimMapIndexer.GetProjectGuid(doc);
            if (!BimMapDatabase.Exists(projectGuid))
            {
                BimMapIndexer.IndexDocument(doc, forceRebuild: false);
            }

            using (var db = new BimMapDatabase(projectGuid))
            {
                var items = db.Search(req.Query, req.Category, req.LevelId, req.Limit);
                sw.Stop();

                return new SearchBimMapResult
                {
                    Success = true,
                    Query = req.Query,
                    TotalFound = items.Count,
                    Items = items,
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }
        }

        public static RebuildBimMapResult HandleRebuildBimMap(UIApplication uiapp, RebuildBimMapRequest req)
        {
            var doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null)
            {
                return new RebuildBimMapResult { Success = false, ErrorMessage = "No active Revit document." };
            }

            return BimMapIndexer.IndexDocument(doc, forceRebuild: true);
        }
    }
}
