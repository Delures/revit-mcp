using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCP.Plugin.Utils;
using RevitMCP.Shared.Commands;

namespace RevitMCP.Plugin.Commands
{
    public class ExportScheduleHandler
    {
        public ExportScheduleResult Execute(UIApplication app, ExportScheduleRequest request)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                return new ExportScheduleResult
                {
                    Success = false,
                    ErrorMessage = "No active document found in Revit."
                };
            }

            string action = request.Action?.Trim().ToLowerInvariant() ?? "get_data";

            if (action == "list")
            {
                var schedules = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => !s.IsTemplate)
                    .OrderBy(s => s.Name)
                    .Select(s =>
                    {
                        string? catName = null;
                        try
                        {
                            if (s.Definition?.CategoryId != null && s.Definition.CategoryId != ElementId.InvalidElementId)
                            {
                                catName = Category.GetCategory(doc, s.Definition.CategoryId)?.Name;
                            }
                        }
                        catch { }

                        return new ScheduleSummary
                        {
                            Id = (int)s.Id.ToLongId(),
                            Name = s.Name,
                            CategoryName = catName,
                            IsKeySchedule = s.Definition?.IsKeySchedule == true
                        };
                    })
                    .ToList();

                return new ExportScheduleResult
                {
                    Success = true,
                    Action = "list",
                    AvailableSchedules = schedules,
                    RowCount = schedules.Count
                };
            }
            else
            {
                ViewSchedule? targetSchedule = null;

                if (request.ScheduleId.HasValue && request.ScheduleId.Value > 0)
                {
                    var elem = doc.GetElement(ElementIdExtensions.FromLongId(request.ScheduleId.Value));
                    targetSchedule = elem as ViewSchedule;
                }
                else if (!string.IsNullOrWhiteSpace(request.ScheduleName))
                {
                    string targetName = request.ScheduleName!.Trim();
                    targetSchedule = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSchedule))
                        .Cast<ViewSchedule>()
                        .FirstOrDefault(s => !s.IsTemplate && string.Equals(s.Name, targetName, StringComparison.OrdinalIgnoreCase))
                        ?? new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSchedule))
                        .Cast<ViewSchedule>()
                        .FirstOrDefault(s => !s.IsTemplate && s.Name.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else
                {
                    targetSchedule = doc.ActiveView as ViewSchedule;
                }

                if (targetSchedule == null)
                {
                    return new ExportScheduleResult
                    {
                        Success = false,
                        ErrorMessage = "Target schedule not found. Specify a valid 'scheduleName', 'scheduleId', or open a schedule in the active view."
                    };
                }

                var tableData = targetSchedule.GetTableData();
                var bodySection = tableData?.GetSectionData(SectionType.Body);
                if (bodySection == null)
                {
                    return new ExportScheduleResult
                    {
                        Success = false,
                        ScheduleId = (int)targetSchedule.Id.ToLongId(),
                        ScheduleName = targetSchedule.Name,
                        ErrorMessage = "Could not retrieve Body section data for schedule."
                    };
                }

                int numCols = bodySection.NumberOfColumns;
                int numRows = bodySection.NumberOfRows;

                // Extract column headers
                var headers = new List<string>();
                var headerSection = tableData?.GetSectionData(SectionType.Header);

                for (int c = 0; c < numCols; c++)
                {
                    string headerText = string.Empty;
                    if (headerSection != null && headerSection.NumberOfRows > 0)
                    {
                        try
                        {
                            headerText = targetSchedule.GetCellText(SectionType.Header, headerSection.NumberOfRows - 1, c);
                        }
                        catch { }
                    }

                    if (string.IsNullOrWhiteSpace(headerText))
                    {
                        try
                        {
                            headerText = targetSchedule.GetCellText(SectionType.Body, 0, c);
                        }
                        catch { }
                    }

                    if (string.IsNullOrWhiteSpace(headerText))
                    {
                        headerText = $"Column {c + 1}";
                    }

                    headers.Add(headerText.Trim());
                }

                // Extract body rows
                int maxRows = Math.Max(1, Math.Min(5000, request.MaxRows));
                var rows = new List<List<string>>();

                for (int r = 0; r < numRows && rows.Count < maxRows; r++)
                {
                    var rowCells = new List<string>();
                    bool hasData = false;

                    for (int c = 0; c < numCols; c++)
                    {
                        string cellText = string.Empty;
                        try
                        {
                            cellText = targetSchedule.GetCellText(SectionType.Body, r, c);
                        }
                        catch { }

                        if (!string.IsNullOrWhiteSpace(cellText))
                        {
                            hasData = true;
                        }
                        rowCells.Add(cellText.Trim());
                    }

                    // Avoid duplicate row if row 0 matched headers exactly
                    if (r == 0 && rowCells.SequenceEqual(headers))
                    {
                        continue;
                    }

                    if (hasData)
                    {
                        rows.Add(rowCells);
                    }
                }

                return new ExportScheduleResult
                {
                    Success = true,
                    Action = "get_data",
                    ScheduleId = (int)targetSchedule.Id.ToLongId(),
                    ScheduleName = targetSchedule.Name,
                    Headers = headers,
                    Rows = rows,
                    RowCount = rows.Count,
                    ColumnCount = numCols
                };
            }
        }
    }
}
