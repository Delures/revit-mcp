using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RevitMCP.Shared.Commands
{
    public class QueryElementsRequest : IAgentAction
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("level")]
        public string? Level { get; set; }

        [JsonPropertyName("room")]
        public string? Room { get; set; }

        [JsonPropertyName("boundingBox")]
        public BoundingBoxQueryDto? BoundingBox { get; set; }

        [JsonPropertyName("parameters")]
        public List<ParameterFilterRule>? Parameters { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 50;

        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        [JsonPropertyName("fields")]
        public List<string>? Fields { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; } // "elements" (default) | "summary"

        [JsonPropertyName("groupBy")]
        public List<string>? GroupBy { get; set; } // "category","level","workset","family","type"

        [JsonPropertyName("includeParameters")]
        public bool? IncludeParameters { get; set; } // default true (backward compatible)

        [JsonPropertyName("parameterNames")]
        public List<string>? ParameterNames { get; set; } // if set, return only these params

        [JsonPropertyName("includeTypeParameters")]
        public bool IncludeTypeParameters { get; set; } = false;

        [JsonPropertyName("parameterAudit")]
        public List<string>? ParameterAudit { get; set; } // fill-rate audit param names

        [JsonPropertyName("resolveLevelFallback")]
        public bool? ResolveLevelFallback { get; set; } // default true

        [JsonPropertyName("checkLevelQuality")]
        public bool CheckLevelQuality { get; set; } = false;

        [JsonPropertyName("actionSummary")]
        public string? ActionSummary { get; set; }

        [JsonPropertyName("agentName")]
        public string? AgentName { get; set; }
    }

    public class BoundingBoxQueryDto
    {
        [JsonPropertyName("min")]
        public double[] Min { get; set; } = new double[3]; // [x, y, z] in mm

        [JsonPropertyName("max")]
        public double[] Max { get; set; } = new double[3]; // [x, y, z] in mm
    }

    public class ParameterFilterRule
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = "Equals"; // Equals, Contains, StartsWith, GreaterThan, LessThan, NotEquals

        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }

    public class QueryElementsResult
    {
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("returnedCount")]
        public int ReturnedCount { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("elements")]
        public List<ElementDto> Elements { get; set; } = new List<ElementDto>();

        [JsonPropertyName("groups")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<GroupCount>? Groups { get; set; }

        [JsonPropertyName("parameterAudit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ParameterAuditResult>? ParameterAudit { get; set; }

        [JsonPropertyName("levelQuality")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LevelQualityInfo? LevelQuality { get; set; }
    }

    public class GroupCount
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class ParameterAuditResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("filledInstance")]
        public int FilledInstance { get; set; }

        [JsonPropertyName("filledType")]
        public int FilledType { get; set; }

        [JsonPropertyName("filledAny")]
        public int FilledAny { get; set; }

        [JsonPropertyName("emptyCount")]
        public int EmptyCount { get; set; }

        [JsonPropertyName("fillRatePercent")]
        public double FillRatePercent { get; set; }

        [JsonPropertyName("emptyExamples")]
        public List<long> EmptyExamples { get; set; } = new List<long>();
    }

    public class LevelQualityInfo
    {
        [JsonPropertyName("missingLevelCount")]
        public int MissingLevelCount { get; set; }

        [JsonPropertyName("multiLevelElements")]
        public List<long> MultiLevelElements { get; set; } = new List<long>();
    }

    public class ElementDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("familyName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FamilyName { get; set; }

        [JsonPropertyName("typeName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TypeName { get; set; }

        [JsonPropertyName("level")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Level { get; set; }

        [JsonPropertyName("boundingBox")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BoundingBoxQueryDto? BoundingBox { get; set; }

        [JsonPropertyName("parameters")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object?>? Parameters { get; set; }
    }
}
