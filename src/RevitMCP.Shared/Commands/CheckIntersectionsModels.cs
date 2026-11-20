using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RevitMCP.Shared.Commands
{
    public class CheckIntersectionsRequest : IAgentAction
    {
        [JsonPropertyName("targetElementId")]
        public long? TargetElementId { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; } // "element" (default) | "matrix" | "duplicates"

        [JsonPropertyName("sourceCategories")]
        public List<string>? SourceCategories { get; set; }

        [JsonPropertyName("targetCategories")]
        public List<string>? TargetCategories { get; set; }

        [JsonPropertyName("sourceElementIds")]
        public List<long>? SourceElementIds { get; set; }

        [JsonPropertyName("levelName")]
        public string? LevelName { get; set; }

        [JsonPropertyName("toleranceMm")]
        public double? ToleranceMm { get; set; }

        [JsonPropertyName("minClashVolumeM3")]
        public double? MinClashVolumeM3 { get; set; }

        [JsonPropertyName("maxDurationMs")]
        public int? MaxDurationMs { get; set; }

        [JsonPropertyName("candidateCategories")]
        public List<string>? CandidateCategories { get; set; }

        [JsonPropertyName("candidateElementIds")]
        public List<long>? CandidateElementIds { get; set; }

        [JsonPropertyName("activeViewOnly")]
        public bool ActiveViewOnly { get; set; } = false;

        [JsonPropertyName("calculateClashVolume")]
        public bool CalculateClashVolume { get; set; } = false;

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 100;

        [JsonPropertyName("actionSummary")]
        public string? ActionSummary { get; set; }

        [JsonPropertyName("agentName")]
        public string? AgentName { get; set; }
    }

    public class ClashBboxMm
    {
        [JsonPropertyName("min")]
        public double[] Min { get; set; } = new double[3];

        [JsonPropertyName("max")]
        public double[] Max { get; set; } = new double[3];
    }

    public class ClashRecord
    {
        [JsonPropertyName("elementId")]
        public long ElementId { get; set; }

        [JsonPropertyName("categoryName")]
        public string CategoryName { get; set; } = string.Empty;

        [JsonPropertyName("familyName")]
        public string FamilyName { get; set; } = string.Empty;

        [JsonPropertyName("typeName")]
        public string TypeName { get; set; } = string.Empty;

        [JsonPropertyName("elementName")]
        public string ElementName { get; set; } = string.Empty;

        [JsonPropertyName("clashVolumeM3")]
        public double? ClashVolumeM3 { get; set; }

        [JsonPropertyName("clashBoundingBoxMm")]
        public ClashBboxMm? ClashBoundingBoxMm { get; set; }
    }

    public class MatrixCell
    {
        [JsonPropertyName("sourceCategory")]
        public string SourceCategory { get; set; } = string.Empty;

        [JsonPropertyName("targetCategory")]
        public string TargetCategory { get; set; } = string.Empty;

        [JsonPropertyName("clashCount")]
        public int ClashCount { get; set; }

        [JsonPropertyName("totalVolumeM3")]
        public double TotalVolumeM3 { get; set; }
    }

    public class DuplicateGroup
    {
        [JsonPropertyName("categoryId")]
        public long CategoryId { get; set; }

        [JsonPropertyName("typeId")]
        public long TypeId { get; set; }

        [JsonPropertyName("categoryName")]
        public string CategoryName { get; set; } = string.Empty;

        [JsonPropertyName("typeName")]
        public string TypeName { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("elementIds")]
        public List<long> ElementIds { get; set; } = new List<long>();

        [JsonPropertyName("boundingBoxMm")]
        public ClashBboxMm? BoundingBoxMm { get; set; }
    }

    public class CheckIntersectionsResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "element";

        [JsonPropertyName("targetElementId")]
        public long? TargetElementId { get; set; }

        [JsonPropertyName("targetName")]
        public string TargetName { get; set; } = string.Empty;

        [JsonPropertyName("targetCategory")]
        public string TargetCategory { get; set; } = string.Empty;

        [JsonPropertyName("totalIntersections")]
        public int TotalIntersections { get; set; }

        [JsonPropertyName("intersections")]
        public List<ClashRecord> Intersections { get; set; } = new List<ClashRecord>();

        [JsonPropertyName("matrix")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MatrixCell>? Matrix { get; set; }

        [JsonPropertyName("duplicateGroups")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DuplicateGroup>? DuplicateGroups { get; set; }

        [JsonPropertyName("truncated")]
        public bool Truncated { get; set; }

        [JsonPropertyName("checkedPairs")]
        public int CheckedPairs { get; set; }

        [JsonPropertyName("totalCandidatePairs")]
        public int TotalCandidatePairs { get; set; }

        [JsonPropertyName("executionTimeMs")]
        public long ExecutionTimeMs { get; set; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
