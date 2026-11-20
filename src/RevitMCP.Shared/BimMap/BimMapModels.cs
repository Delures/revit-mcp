using System;
using System.Collections.Generic;

namespace RevitMCP.Shared.BimMap
{
    public class BimMapLevel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double ElevationMm { get; set; }
    }

    public class BimMapGrid
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsCurved { get; set; }
        public double? StartXMm { get; set; }
        public double? StartYMm { get; set; }
        public double? EndXMm { get; set; }
        public double? EndYMm { get; set; }
    }

    public class BimMapLink
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PathName { get; set; } = string.Empty;
        public bool IsLoaded { get; set; }
    }

    public class BimMapTopology
    {
        public List<BimMapLevel> Levels { get; set; } = new List<BimMapLevel>();
        public List<BimMapGrid> Grids { get; set; } = new List<BimMapGrid>();
        public List<BimMapLink> Links { get; set; } = new List<BimMapLink>();
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    }

    public class BimMapBoundingBox
    {
        public double[] Min { get; set; } = new double[3];
        public double[] Max { get; set; } = new double[3];
    }

    public class BimMapRoom
    {
        public long Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long? LevelId { get; set; }
        public string? LevelName { get; set; }
        public double AreaM2 { get; set; }
        public double VolumeM3 { get; set; }
        public BimMapBoundingBox? BoundingBox { get; set; }
        public int ElementCount { get; set; }
    }

    public class BimMapSpatial
    {
        public List<BimMapRoom> Rooms { get; set; } = new List<BimMapRoom>();
    }

    public class GetBimMapRequest
    {
        public string Scope { get; set; } = "overview"; // "overview", "spatial", "all"
        public long? LevelId { get; set; }
        public bool ForceRefresh { get; set; }
    }

    public class BimMapResult
    {
        public bool Success { get; set; } = true;
        public string ProjectGuid { get; set; } = string.Empty;
        public string VersionGuid { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public DateTime LastIndexedUtc { get; set; }
        public string Scope { get; set; } = "overview";
        public BimMapTopology? Topology { get; set; }
        public BimMapSpatial? Spatial { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SearchBimMapRequest
    {
        public string Query { get; set; } = string.Empty;
        public string? Category { get; set; }
        public long? LevelId { get; set; }
        public int Limit { get; set; } = 50;
    }

    public class BimMapSearchResultItem
    {
        public long ElementId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string? RoomName { get; set; }
        public string? LevelName { get; set; }
        public string? Mark { get; set; }
        public string? Comments { get; set; }
        public string? Snippet { get; set; }
    }

    public class SearchBimMapResult
    {
        public bool Success { get; set; } = true;
        public int TotalFound { get; set; }
        public List<BimMapSearchResultItem> Items { get; set; } = new List<BimMapSearchResultItem>();
        public string Query { get; set; } = string.Empty;
        public long ElapsedMs { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class RebuildBimMapRequest
    {
        public bool Force { get; set; } = true;
    }

    public class RebuildBimMapResult
    {
        public bool Success { get; set; } = true;
        public string ProjectGuid { get; set; } = string.Empty;
        public string VersionGuid { get; set; } = string.Empty;
        public int ElementCount { get; set; }
        public int RoomCount { get; set; }
        public int LevelCount { get; set; }
        public int GridCount { get; set; }
        public string DatabasePath { get; set; } = string.Empty;
        public long DatabaseSizeBytes { get; set; }
        public long ElapsedMs { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ManageCacheRequest
    {
        public string Action { get; set; } = "status"; // "status" or "clean"
        public int MaxSizeMb { get; set; } = 2048;
        public int RetentionDays { get; set; } = 30;
    }

    public class CacheProjectInfo
    {
        public string ProjectGuid { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public double SizeMb => Math.Round(SizeBytes / (1024.0 * 1024.0), 2);
        public DateTime LastModifiedUtc { get; set; }
    }

    public class ManageCacheResult
    {
        public bool Success { get; set; } = true;
        public string Action { get; set; } = "status";
        public double TotalSizeMb { get; set; }
        public int TotalFiles { get; set; }
        public double FreedMb { get; set; }
        public List<string> CleanedFiles { get; set; } = new List<string>();
        public List<CacheProjectInfo> Projects { get; set; } = new List<CacheProjectInfo>();
        public string? Message { get; set; }
    }
}
