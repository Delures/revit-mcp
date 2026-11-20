using System.Collections.Generic;

namespace RevitMCP.Shared.Commands
{
    public class ScheduleSummary
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public bool IsKeySchedule { get; set; }
    }

    public class ExportScheduleRequest : IAgentAction
    {
        public string Action { get; set; } = "get_data"; // "list", "get_data"
        public string? ScheduleName { get; set; }
        public int? ScheduleId { get; set; }
        public int MaxRows { get; set; } = 500;
        public string? ActionSummary { get; set; }
        public string? AgentName { get; set; }
    }

    public class ExportScheduleResult
    {
        public bool Success { get; set; }
        public string Action { get; set; } = "get_data";
        public List<ScheduleSummary>? AvailableSchedules { get; set; }
        public int? ScheduleId { get; set; }
        public string? ScheduleName { get; set; }
        public List<string>? Headers { get; set; }
        public List<List<string>>? Rows { get; set; }
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
