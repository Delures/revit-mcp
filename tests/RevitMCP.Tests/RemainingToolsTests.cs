using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RevitMCP.Cli.Mcp;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;
using Xunit;

namespace RevitMCP.Tests
{
    public class RemainingToolsTests
    {
        [Fact]
        public void ManageSelection_Serialization_Roundtrip()
        {
            var request = new ManageSelectionRequest
            {
                Action = "set",
                ElementIds = new List<int> { 101, 102, 103 }
            };

            string json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<ManageSelectionRequest>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("set", deserialized.Action);
            Assert.Equal(3, deserialized.ElementIds?.Count);
            Assert.Contains(102, deserialized.ElementIds!);

            var result = new ManageSelectionResult
            {
                Success = true,
                Action = "get",
                SelectedElementIds = new List<int> { 101 },
                Elements = new List<SelectedElementInfo>
                {
                    new SelectedElementInfo
                    {
                        Id = 101,
                        Category = "Стены",
                        FamilyName = "Базовая стена",
                        TypeName = "Железобетон 200мм",
                        Name = "Ж/б 200"
                    }
                }
            };

            string resJson = JsonSerializer.Serialize(result);
            var resDeserialized = JsonSerializer.Deserialize<ManageSelectionResult>(resJson);

            Assert.NotNull(resDeserialized);
            Assert.True(resDeserialized.Success);
            Assert.Single(resDeserialized.SelectedElementIds);
            Assert.Equal("Стены", resDeserialized.Elements?[0].Category);
        }

        [Fact]
        public void InspectElementGeometry_Serialization_And_Math()
        {
            var req = new InspectElementGeometryRequest
            {
                ElementId = 554433,
                IncludeFaces = true
            };

            string json = JsonSerializer.Serialize(req);
            var dReq = JsonSerializer.Deserialize<InspectElementGeometryRequest>(json);
            Assert.NotNull(dReq);
            Assert.Equal(554433, dReq.ElementId);
            Assert.True(dReq.IncludeFaces);

            var result = new InspectElementGeometryResult
            {
                Success = true,
                ElementId = 554433,
                CategoryName = "Перекрытия",
                ElementName = "Плита монолитная 200мм",
                GeometryType = "slab",
                ThicknessMm = 200.0,
                VolumeM3 = 15.6,
                BoundingBoxMm = new BoundingBoxMm
                {
                    Min = new Point3DMm(0, 0, 3000),
                    Max = new Point3DMm(6000, 6000, 3200),
                    WidthMm = 6000,
                    LengthMm = 6000,
                    HeightMm = 200
                },
                BoundaryPointsMm = new List<Point3DMm>
                {
                    new Point3DMm(0, 0, 3200),
                    new Point3DMm(6000, 0, 3200),
                    new Point3DMm(6000, 6000, 3200),
                    new Point3DMm(0, 6000, 3200)
                },
                HolesMm = new List<List<Point3DMm>>
                {
                    new List<Point3DMm>
                    {
                        new Point3DMm(2000, 2000, 3200),
                        new Point3DMm(3000, 2000, 3200),
                        new Point3DMm(3000, 3000, 3200),
                        new Point3DMm(2000, 3000, 3200)
                    }
                }
            };

            string resJson = JsonSerializer.Serialize(result);
            var dRes = JsonSerializer.Deserialize<InspectElementGeometryResult>(resJson);

            Assert.NotNull(dRes);
            Assert.True(dRes.Success);
            Assert.Equal("slab", dRes.GeometryType);
            Assert.Equal(200.0, dRes.ThicknessMm);
            Assert.Equal(4, dRes.BoundaryPointsMm?.Count);
            Assert.Single(dRes.HolesMm!);
            Assert.Equal(4, dRes.HolesMm?[0].Count);

            // Verify Unit conversion helper
            double cuFeet = 35.3147;
            double m3 = RevitUnitConverter.CuFeetToCuMeters(cuFeet);
            Assert.InRange(m3, 0.99, 1.01);
        }

        [Fact]
        public void VisualOverride_Serialization_Roundtrip()
        {
            var req = new VisualOverrideRequest
            {
                Action = "override_graphics",
                ElementIds = new List<int> { 1234, 5678 },
                Transparency = 50,
                Color = new ColorRgb(255, 0, 0),
                Halftone = true,
                SolidIn3D = true,
                UnobscuredInView = true
            };

            string json = JsonSerializer.Serialize(req);
            var dReq = JsonSerializer.Deserialize<VisualOverrideRequest>(json);

            Assert.NotNull(dReq);
            Assert.Equal("override_graphics", dReq.Action);
            Assert.Equal(2, dReq.ElementIds.Count);
            Assert.Equal(50, dReq.Transparency);
            Assert.Equal((byte)255, dReq.Color?.R);
            Assert.True(dReq.SolidIn3D);

            var res = new VisualOverrideResult
            {
                Success = true,
                Action = "override_graphics",
                AffectedElementIds = new List<int> { 1234, 5678 },
                ViewId = 999,
                ViewName = "{3D - AI Verification}"
            };

            string resJson = JsonSerializer.Serialize(res);
            var dRes = JsonSerializer.Deserialize<VisualOverrideResult>(resJson);

            Assert.NotNull(dRes);
            Assert.True(dRes.Success);
            Assert.Equal(2, dRes.AffectedElementIds.Count);
            Assert.Equal(999, dRes.ViewId);
        }

        [Fact]
        public void ExportSchedule_Serialization_Roundtrip()
        {
            var listReq = new ExportScheduleRequest
            {
                Action = "list"
            };
            string listJson = JsonSerializer.Serialize(listReq);
            var dListReq = JsonSerializer.Deserialize<ExportScheduleRequest>(listJson);
            Assert.NotNull(dListReq);
            Assert.Equal("list", dListReq.Action);

            var dataRes = new ExportScheduleResult
            {
                Success = true,
                Action = "get_data",
                ScheduleId = 778899,
                ScheduleName = "Ведомость расхода стали",
                Headers = new List<string> { "Марка", "Диаметр, мм", "Класс арматуры", "Длина общая, м", "Масса, кг" },
                Rows = new List<List<string>>
                {
                    new List<string> { "СТ-1", "12", "А500С", "154.2", "136.9" },
                    new List<string> { "СТ-2", "16", "А500С", "88.0", "138.9" }
                },
                RowCount = 2,
                ColumnCount = 5
            };

            string resJson = JsonSerializer.Serialize(dataRes);
            var dDataRes = JsonSerializer.Deserialize<ExportScheduleResult>(resJson);

            Assert.NotNull(dDataRes);
            Assert.True(dDataRes.Success);
            Assert.Equal("Ведомость расхода стали", dDataRes.ScheduleName);
            Assert.Equal(5, dDataRes.Headers?.Count);
            Assert.Equal(2, dDataRes.Rows?.Count);
            Assert.Equal("136.9", dDataRes.Rows?[0][4]);
        }

        [Fact]
        public void McpToolRegistry_Registers_All_Native_Tools()
        {
            var registry = McpToolRegistry.CreateDefault();
            var allTools = registry.GetAllTools();

            // Total 22 tools in complete roster
            Assert.Equal(22, allTools.Count);

            var expectedToolNames = new[]
            {
                // Core (4)
                "ping", "list_revit_instances", "select_revit_instance", "execute_csharp",
                // Knowledge (2)
                "record_lesson", "query_knowledge",
                // Document, Parameter & Schedule (4)
                "manage_documents", "inspect_schema", "manage_parameters", "export_schedule",
                // Query & BIM-Map (6)
                "query_elements", "get_bim_map", "search_bim_map", "rebuild_bim_map", "manage_cache",
                // Modeling (2)
                "transform_elements", "create_elements",
                // Analysis, UI & Vision (4)
                "export_view_image", "check_intersections", "manage_selection", "inspect_element_geometry", "visual_override"
            };

            foreach (var name in expectedToolNames)
            {
                Assert.True(registry.TryGetTool(name, out var tool), $"Tool '{name}' should be registered in default registry.");
                Assert.NotNull(tool);
                Assert.False(string.IsNullOrWhiteSpace(tool!.Description));
                Assert.NotNull(tool.InputSchema);
            }

            var schemas = registry.GetToolsListSchema();
            Assert.Equal(22, schemas.Length);
        }
    }
}
