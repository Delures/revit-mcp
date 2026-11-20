using System;
using System.Collections.Generic;
using System.Text.Json;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Scripting;
using Xunit;

namespace RevitMCP.Tests
{
    public class Stage2Tests
    {
        [Fact]
        public void ExecuteScriptResult_WithWarningsAndDialogs_SerializesProperly()
        {
            var result = new ExecuteScriptResult
            {
                Success = true,
                ExecutionTimeMs = 120,
                Warnings = new List<WarningRecord>
                {
                    new WarningRecord
                    {
                        Severity = "Warning",
                        DefinitionId = "00000000-0000-0000-0000-000000000001",
                        Message = "Элементы имеют повторяющееся значение Марка.",
                        ElementIds = new List<long> { 101, 102 },
                        ActionTaken = "AutoSuppressed"
                    }
                },
                CapturedDialogs = new List<CapturedDialogRecord>
                {
                    new CapturedDialogRecord
                    {
                        DialogId = "TaskDialog_FamilyAlreadyExists",
                        Message = "Семейство уже существует. Перезаписать?",
                        ActionTaken = "OverrideResult_CommandLink1_Overwrite",
                        Timestamp = DateTime.UtcNow
                    }
                }
            };

            string json = JsonSerializer.Serialize(result);
            Assert.Contains("AutoSuppressed", json);

            var deserialized = JsonSerializer.Deserialize<ExecuteScriptResult>(json);
            Assert.NotNull(deserialized);
            Assert.Single(deserialized.Warnings!);
            Assert.Equal("AutoSuppressed", deserialized.Warnings![0].ActionTaken);
            Assert.Equal("Элементы имеют повторяющееся значение Марка.", deserialized.Warnings![0].Message);
            Assert.Equal(2, deserialized.Warnings![0].ElementIds.Count);
            Assert.Single(deserialized.CapturedDialogs!);
            Assert.Equal("TaskDialog_FamilyAlreadyExists", deserialized.CapturedDialogs![0].DialogId);
            Assert.Equal("Семейство уже существует. Перезаписать?", deserialized.CapturedDialogs![0].Message);
        }

        [Fact]
        public void RoslynScriptEngine_PropagatesWarningsFromContext()
        {
            var engine = new RoslynScriptEngine();
            var context = new MockScriptContext();

            string code = @"
context.AddWarning(new RevitMCP.Shared.Scripting.WarningRecord
{
    Severity = ""Warning"",
    Message = ""Custom warning test"",
    ElementIds = new System.Collections.Generic.List<long> { 999 },
    ActionTaken = ""AutoSuppressed""
});
return 42;
";

            var result = engine.Execute(code, context);

            Assert.True(result.Success);
            Assert.Equal(42, Convert.ToInt32(result.ReturnValue));
            Assert.NotNull(result.Warnings);
            Assert.Single(result.Warnings!);
            Assert.Equal("Custom warning test", result.Warnings![0].Message);
            Assert.Equal(999, result.Warnings![0].ElementIds[0]);
        }

        [Fact]
        public void InspectSchemaModels_SerializationRoundtrip()
        {
            var req = new InspectSchemaRequest
            {
                Category = "Walls",
                Family = "Basic Wall",
                IncludeParameters = true
            };

            string reqJson = JsonSerializer.Serialize(req);
            var parsedReq = JsonSerializer.Deserialize<InspectSchemaRequest>(reqJson);
            Assert.NotNull(parsedReq);
            Assert.Equal("Walls", parsedReq.Category);
            Assert.True(parsedReq.IncludeParameters);

            var res = new InspectSchemaResult
            {
                Categories = new List<CategorySchemaInfo>
                {
                    new CategorySchemaInfo
                    {
                        Id = -2000011,
                        Name = "Стены",
                        BuiltInCategory = "OST_Walls",
                        ElementCount = 42,
                        Families = new List<FamilySchemaInfo>
                        {
                            new FamilySchemaInfo
                            {
                                Id = 1001,
                                Name = "Базовая стена",
                                Symbols = new List<SymbolSchemaInfo>
                                {
                                    new SymbolSchemaInfo
                                    {
                                        Id = 1002,
                                        Name = "Типовой - 200мм",
                                        Parameters = new List<ParameterSchemaInfo>
                                        {
                                            new ParameterSchemaInfo
                                            {
                                                Name = "Ширина",
                                                BuiltInParameter = "WALL_ATTR_WIDTH_PARAM",
                                                StorageType = "Double",
                                                IsReadOnly = true,
                                                UnitType = "autodesk.unit.unit:millimeters-1.0.0"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string resJson = JsonSerializer.Serialize(res);
            var parsedRes = JsonSerializer.Deserialize<InspectSchemaResult>(resJson);
            Assert.NotNull(parsedRes);
            Assert.Single(parsedRes.Categories);
            Assert.Equal("Стены", parsedRes.Categories[0].Name);
            Assert.Single(parsedRes.Categories[0].Families);
            Assert.Single(parsedRes.Categories[0].Families[0].Symbols);
            Assert.Single(parsedRes.Categories[0].Families[0].Symbols[0].Parameters!);
            Assert.Equal("WALL_ATTR_WIDTH_PARAM", parsedRes.Categories[0].Families[0].Symbols[0].Parameters![0].BuiltInParameter);
        }

        [Fact]
        public void QueryElementsModels_SerializationRoundtrip()
        {
            var req = new QueryElementsRequest
            {
                Category = "OST_Walls",
                Level = "Level 1",
                Limit = 25,
                Offset = 5,
                BoundingBox = new BoundingBoxQueryDto
                {
                    Min = new double[] { 0, 0, 0 },
                    Max = new double[] { 5000, 3000, 3000 }
                },
                Parameters = new List<ParameterFilterRule>
                {
                    new ParameterFilterRule
                    {
                        Name = "Comments",
                        Operator = "Contains",
                        Value = "Фасад"
                    }
                },
                Fields = new List<string> { "id", "category", "parameters" }
            };

            string reqJson = JsonSerializer.Serialize(req);
            var parsedReq = JsonSerializer.Deserialize<QueryElementsRequest>(reqJson);
            Assert.NotNull(parsedReq);
            Assert.Equal("OST_Walls", parsedReq.Category);
            Assert.Equal("Level 1", parsedReq.Level);
            Assert.Equal(25, parsedReq.Limit);
            Assert.Equal(5, parsedReq.Offset);
            Assert.Equal(5000, parsedReq.BoundingBox!.Max[0]);
            Assert.Single(parsedReq.Parameters!);
            Assert.Equal("Фасад", parsedReq.Parameters![0].Value?.ToString());

            var res = new QueryElementsResult
            {
                TotalCount = 100,
                ReturnedCount = 1,
                Offset = 5,
                Elements = new List<ElementDto>
                {
                    new ElementDto
                    {
                        Id = 554321,
                        Category = "Walls",
                        FamilyName = "Basic Wall",
                        TypeName = "Exterior - Brick",
                        Level = "Level 1",
                        BoundingBox = new BoundingBoxQueryDto
                        {
                            Min = new double[] { 100, 200, 0 },
                            Max = new double[] { 5100, 3200, 3000 }
                        },
                        Parameters = new Dictionary<string, object?>
                        {
                            { "Comments", "Фасад северный" },
                            { "Length", 5000.0 }
                        }
                    }
                }
            };

            string resJson = JsonSerializer.Serialize(res);
            var parsedRes = JsonSerializer.Deserialize<QueryElementsResult>(resJson);
            Assert.NotNull(parsedRes);
            Assert.Equal(100, parsedRes.TotalCount);
            Assert.Single(parsedRes.Elements);
            Assert.Equal(554321, parsedRes.Elements[0].Id);
            Assert.Equal("Exterior - Brick", parsedRes.Elements[0].TypeName);
            Assert.NotNull(parsedRes.Elements[0].BoundingBox);
            Assert.Equal(5100, parsedRes.Elements[0].BoundingBox!.Max[0]);
        }
    }
}
