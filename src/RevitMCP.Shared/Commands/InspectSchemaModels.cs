using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RevitMCP.Shared.Commands
{
    public class InspectSchemaRequest : IAgentAction
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("includeParameters")]
        public bool IncludeParameters { get; set; } = false;

        [JsonPropertyName("sections")]
        public List<string>? Sections { get; set; }

        [JsonPropertyName("actionSummary")]
        public string? ActionSummary { get; set; }

        [JsonPropertyName("agentName")]
        public string? AgentName { get; set; }
    }

    public class InspectSchemaResult
    {
        [JsonPropertyName("categories")]
        public List<CategorySchemaInfo> Categories { get; set; } = new List<CategorySchemaInfo>();

        [JsonPropertyName("projectInfo")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ProjectInfoSection? ProjectInfo { get; set; }

        [JsonPropertyName("units")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public UnitsSection? Units { get; set; }

        [JsonPropertyName("coordinates")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CoordinatesSection? Coordinates { get; set; }

        [JsonPropertyName("gridsLevels")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GridsLevelsSection? GridsLevels { get; set; }

        [JsonPropertyName("worksets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorksetsSection? Worksets { get; set; }

        [JsonPropertyName("linksImports")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LinksImportsSection? LinksImports { get; set; }

        [JsonPropertyName("sharedParams")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SharedParamsSection? SharedParams { get; set; }

        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WarningsSection? Warnings { get; set; }

        [JsonPropertyName("rooms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RoomsSection? Rooms { get; set; }
    }

    // ---- Section payload DTOs ----

    public class ProjectInfoSection
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("number")]
        public string? Number { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("clientName")]
        public string? ClientName { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("docTitle")]
        public string? DocTitle { get; set; }

        [JsonPropertyName("pathName")]
        public string? PathName { get; set; }

        [JsonPropertyName("isWorkshared")]
        public bool IsWorkshared { get; set; }

        [JsonPropertyName("isDetached")]
        public bool IsDetached { get; set; }

        [JsonPropertyName("centralModelPath")]
        public string? CentralModelPath { get; set; }
    }

    public class UnitsSection
    {
        [JsonPropertyName("length")]
        public UnitFormatInfo? Length { get; set; }

        [JsonPropertyName("area")]
        public UnitFormatInfo? Area { get; set; }

        [JsonPropertyName("volume")]
        public UnitFormatInfo? Volume { get; set; }

        [JsonPropertyName("angle")]
        public UnitFormatInfo? Angle { get; set; }

        [JsonPropertyName("slope")]
        public UnitFormatInfo? Slope { get; set; }
    }

    public class UnitFormatInfo
    {
        [JsonPropertyName("unitTypeId")]
        public string? UnitTypeId { get; set; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("accuracy")]
        public double Accuracy { get; set; }

        [JsonPropertyName("suppressSpaces")]
        public bool SuppressSpaces { get; set; }
    }

    public class CoordinatesSection
    {
        [JsonPropertyName("latitudeDeg")]
        public double? LatitudeDeg { get; set; }

        [JsonPropertyName("longitudeDeg")]
        public double? LongitudeDeg { get; set; }

        [JsonPropertyName("timeZone")]
        public double? TimeZone { get; set; }

        [JsonPropertyName("projectPosition")]
        public ProjectPositionInfo? ProjectPosition { get; set; }

        [JsonPropertyName("projectBasePoint")]
        public BasePointInfo? ProjectBasePoint { get; set; }

        [JsonPropertyName("surveyPoint")]
        public BasePointInfo? SurveyPoint { get; set; }
    }

    public class ProjectPositionInfo
    {
        [JsonPropertyName("eastWestMeters")]
        public double EastWestMeters { get; set; }

        [JsonPropertyName("northSouthMeters")]
        public double NorthSouthMeters { get; set; }

        [JsonPropertyName("elevationMeters")]
        public double ElevationMeters { get; set; }

        [JsonPropertyName("angleTrueNorthDeg")]
        public double AngleTrueNorthDeg { get; set; }
    }

    public class BasePointInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("eastWestMeters")]
        public double? EastWestMeters { get; set; }

        [JsonPropertyName("northSouthMeters")]
        public double? NorthSouthMeters { get; set; }

        [JsonPropertyName("elevationMeters")]
        public double? ElevationMeters { get; set; }

        [JsonPropertyName("angleTrueNorthDeg")]
        public double? AngleTrueNorthDeg { get; set; }

        [JsonPropertyName("pinned")]
        public bool Pinned { get; set; }
    }

    public class GridsLevelsSection
    {
        [JsonPropertyName("levels")]
        public List<LevelInfo> Levels { get; set; } = new List<LevelInfo>();

        [JsonPropertyName("grids")]
        public List<GridInfo> Grids { get; set; } = new List<GridInfo>();

        [JsonPropertyName("nonOrthogonalGridCount")]
        public int NonOrthogonalGridCount { get; set; }
    }

    public class LevelInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("elevationMm")]
        public double ElevationMm { get; set; }
    }

    public class GridInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("isOrthogonal")]
        public bool IsOrthogonal { get; set; }

        [JsonPropertyName("direction")]
        public double[]? Direction { get; set; }
    }

    public class WorksetsSection
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("worksets")]
        public List<WorksetInfo> Worksets { get; set; } = new List<WorksetInfo>();
    }

    public class WorksetInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("isOpen")]
        public bool IsOpen { get; set; }

        [JsonPropertyName("isDefault")]
        public bool IsDefault { get; set; }

        [JsonPropertyName("isEditable")]
        public bool IsEditable { get; set; }
    }

    public class LinksImportsSection
    {
        [JsonPropertyName("links")]
        public List<LinkInfo> Links { get; set; } = new List<LinkInfo>();

        [JsonPropertyName("importInstances")]
        public ImportSummary ImportInstances { get; set; } = new ImportSummary();

        [JsonPropertyName("imageTypesCount")]
        public int ImageTypesCount { get; set; }

        [JsonPropertyName("inPlaceFamilyCount")]
        public int InPlaceFamilyCount { get; set; }
    }

    public class LinkInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("linkStatus")]
        public string? LinkStatus { get; set; }

        [JsonPropertyName("attachmentType")]
        public string? AttachmentType { get; set; }

        [JsonPropertyName("isNested")]
        public bool IsNested { get; set; }
    }

    public class ImportSummary
    {
        [JsonPropertyName("linked")]
        public int Linked { get; set; }

        [JsonPropertyName("embedded")]
        public int Embedded { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    public class SharedParamsSection
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("bindings")]
        public List<SharedParamBinding> Bindings { get; set; } = new List<SharedParamBinding>();
    }

    public class SharedParamBinding
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("isShared")]
        public bool IsShared { get; set; }

        [JsonPropertyName("guid")]
        public string? Guid { get; set; }

        [JsonPropertyName("bindingType")]
        public string? BindingType { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>();
    }

    public class WarningsSection
    {
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("groups")]
        public List<WarningGroup> Groups { get; set; } = new List<WarningGroup>();
    }

    public class WarningGroup
    {
        [JsonPropertyName("definitionId")]
        public string? DefinitionId { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("topElementIds")]
        public List<long> TopElementIds { get; set; } = new List<long>();
    }

    public class RoomsSection
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("placed")]
        public int Placed { get; set; }

        [JsonPropertyName("zeroOrNegativeArea")]
        public int ZeroOrNegativeArea { get; set; }

        [JsonPropertyName("unplaced")]
        public int Unplaced { get; set; }
    }

    public class CategorySchemaInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("builtInCategory")]
        public string BuiltInCategory { get; set; } = string.Empty;

        [JsonPropertyName("elementCount")]
        public int ElementCount { get; set; }

        [JsonPropertyName("families")]
        public List<FamilySchemaInfo> Families { get; set; } = new List<FamilySchemaInfo>();
    }

    public class FamilySchemaInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("symbols")]
        public List<SymbolSchemaInfo> Symbols { get; set; } = new List<SymbolSchemaInfo>();
    }

    public class SymbolSchemaInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ParameterSchemaInfo>? Parameters { get; set; }
    }

    public class ParameterSchemaInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("builtInParameter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BuiltInParameter { get; set; }

        [JsonPropertyName("storageType")]
        public string StorageType { get; set; } = string.Empty;

        [JsonPropertyName("isReadOnly")]
        public bool IsReadOnly { get; set; }

        [JsonPropertyName("unitType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UnitType { get; set; }
    }
}
