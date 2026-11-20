using System;
using System.Collections.Generic;
using System.Text.Json;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Scripting;
using RevitMCP.Shared.Units;
using Xunit;

namespace RevitMCP.Tests
{
    /// <summary>
    /// Tests for the audit-driven extensions (MDTs Sovetsk hospital audit):
    /// new request/result DTO fields, section payloads, fill-rate audit, matrix/duplicates,
    /// and the new RoslynScriptEngine usings — all testable without live Revit.
    /// </summary>
    public class AuditExtensionsTests
    {
        // ---------- CheckIntersections: matrix / duplicates ----------

        [Fact]
        public void CheckIntersections_MatrixMode_Serialization_Roundtrip()
        {
            var req = new CheckIntersectionsRequest
            {
                Mode = "matrix",
                SourceCategories = new List<string> { "Walls", "Floors" },
                TargetCategories = new List<string> { "Pipes", "Ducts" },
                MinClashVolumeM3 = 0.0001,
                MaxDurationMs = 25000
            };

            string json = JsonSerializer.Serialize(req);
            var d = JsonSerializer.Deserialize<CheckIntersectionsRequest>(json);

            Assert.NotNull(d);
            Assert.Equal("matrix", d.Mode);
            Assert.Null(d.TargetElementId); // optional in matrix mode
            Assert.Equal(2, d.SourceCategories?.Count);
            Assert.Equal(0.0001, d.MinClashVolumeM3);
            Assert.Equal(25000, d.MaxDurationMs);
            // camelCase on the wire
            Assert.Contains("\"targetCategories\"", json);
            Assert.Contains("\"minClashVolumeM3\"", json);
        }

        [Fact]
        public void CheckIntersections_Result_MatrixAndDuplicates_Payloads_Roundtrip()
        {
            var result = new CheckIntersectionsResult
            {
                Mode = "matrix",
                Truncated = true,
                CheckedPairs = 321,
                TotalCandidatePairs = 1000,
                Matrix = new List<MatrixCell>
                {
                    new MatrixCell { SourceCategory = "Walls", TargetCategory = "Pipes", ClashCount = 12, TotalVolumeM3 = 0.5 }
                },
                DuplicateGroups = new List<DuplicateGroup>
                {
                    new DuplicateGroup
                    {
                        CategoryId = 1, TypeId = 2, CategoryName = "Walls", TypeName = "Basic Wall",
                        Count = 2, ElementIds = new List<long> { 100, 101 }
                    }
                }
            };

            string json = JsonSerializer.Serialize(result);
            var d = JsonSerializer.Deserialize<CheckIntersectionsResult>(json);

            Assert.NotNull(d);
            Assert.True(d.Truncated);
            Assert.Equal(321, d.CheckedPairs);
            Assert.Single(d.Matrix!);
            Assert.Equal("Walls", d.Matrix![0].SourceCategory);
            Assert.Single(d.DuplicateGroups!);
            Assert.Equal(2, d.DuplicateGroups![0].Count);
        }

        // ---------- QueryElements: summary / groupBy / parameterAudit ----------

        [Fact]
        public void QueryElements_SummaryMode_Serialization_Roundtrip()
        {
            var req = new QueryElementsRequest
            {
                Mode = "summary",
                GroupBy = new List<string> { "category", "level" },
                IncludeParameters = false,
                ParameterAudit = new List<string> { "ADSK_Marka", "ADSK_Position" },
                ResolveLevelFallback = true,
                CheckLevelQuality = true
            };

            string json = JsonSerializer.Serialize(req);
            var d = JsonSerializer.Deserialize<QueryElementsRequest>(json);

            Assert.NotNull(d);
            Assert.Equal("summary", d.Mode);
            Assert.Equal(2, d.GroupBy?.Count);
            Assert.False(d.IncludeParameters);
            Assert.Equal(2, d.ParameterAudit?.Count);
            Assert.True(d.ResolveLevelFallback);
            Assert.True(d.CheckLevelQuality);
            Assert.Contains("\"parameterAudit\"", json);
        }

        [Fact]
        public void QueryElements_Result_GroupsAndAudit_Roundtrip()
        {
            var result = new QueryElementsResult
            {
                TotalCount = 100,
                ReturnedCount = 0,
                Groups = new List<GroupCount>
                {
                    new GroupCount { Key = "category:Walls", Count = 60 }
                },
                ParameterAudit = new List<ParameterAuditResult>
                {
                    new ParameterAuditResult
                    {
                        Name = "ADSK_Marka", Total = 100, FilledInstance = 80, FilledType = 10,
                        FilledAny = 90, EmptyCount = 10, FillRatePercent = 90.0,
                        EmptyExamples = new List<long> { 1, 2, 3 }
                    }
                },
                LevelQuality = new LevelQualityInfo { MissingLevelCount = 5, MultiLevelElements = new List<long> { 7 } }
            };

            string json = JsonSerializer.Serialize(result);
            var d = JsonSerializer.Deserialize<QueryElementsResult>(json);

            Assert.NotNull(d);
            Assert.Single(d.Groups!);
            Assert.Equal(60, d.Groups![0].Count);
            Assert.Single(d.ParameterAudit!);
            Assert.Equal(90.0, d.ParameterAudit![0].FillRatePercent);
            Assert.Equal(3, d.ParameterAudit![0].EmptyExamples.Count);
            Assert.Equal(5, d.LevelQuality!.MissingLevelCount);
        }

        // ---------- InspectSchema: sections ----------

        [Fact]
        public void InspectSchema_Sections_Serialization_Roundtrip()
        {
            var req = new InspectSchemaRequest
            {
                Sections = new List<string> { "categories", "project_info", "worksets" }
            };

            string json = JsonSerializer.Serialize(req);
            var d = JsonSerializer.Deserialize<InspectSchemaRequest>(json);

            Assert.NotNull(d);
            Assert.Equal(3, d.Sections?.Count);
            Assert.Contains("\"sections\"", json);
        }

        [Fact]
        public void InspectSchema_Result_Sections_IgnoredWhenNull()
        {
            var result = new InspectSchemaResult
            {
                Worksets = new WorksetsSection
                {
                    Count = 2,
                    Worksets = new List<WorksetInfo>
                    {
                        new WorksetInfo { Id = 1, Name = "Workset1", IsDefault = true }
                    }
                }
            };

            string json = JsonSerializer.Serialize(result);

            // present section serialized with camelCase name
            Assert.Contains("\"worksets\"", json);
            // absent sections omitted entirely (WhenWritingNull)
            Assert.DoesNotContain("\"projectInfo\"", json);
            Assert.DoesNotContain("\"coordinates\"", json);
        }

        // ---------- ManageDocuments: detach / worksets / audit ----------

        [Fact]
        public void ManageDocuments_Open_DetachAndSuppress_Serialization_Roundtrip()
        {
            var req = new ManageDocumentsRequest
            {
                Action = "open",
                FilePath = @"C:\Models\231-P-1-TX.rvt",
                DetachMode = "preserve_worksets",
                Worksets = "all",
                Audit = true,
                SuppressDialogs = true
            };

            string json = JsonSerializer.Serialize(req);
            var d = JsonSerializer.Deserialize<ManageDocumentsRequest>(json);

            Assert.NotNull(d);
            Assert.Equal("preserve_worksets", d.DetachMode);
            Assert.Equal("all", d.Worksets?.ToString());
            Assert.True(d.Audit);
            Assert.True(d.SuppressDialogs);
        }

        [Fact]
        public void ManageDocuments_Result_SuppressedDialogs_Roundtrip()
        {
            var result = new ManageDocumentsResult
            {
                Success = true,
                Action = "open",
                SuppressedDialogs = new List<CapturedDialogRecord>
                {
                    new CapturedDialogRecord { DialogId = "Dialog_Revit_DocWarnDialog", ActionTaken = "OverrideResult(6)" }
                },
                SuppressedWarnings = new List<WarningRecord>
                {
                    new WarningRecord { Message = "Room is not in a properly enclosed region", ActionTaken = "DeleteWarning" }
                }
            };

            string json = JsonSerializer.Serialize(result);
            var d = JsonSerializer.Deserialize<ManageDocumentsResult>(json);

            Assert.NotNull(d);
            Assert.True(d.Success);
            Assert.Single(d.SuppressedDialogs!);
            Assert.Equal("Dialog_Revit_DocWarnDialog", d.SuppressedDialogs![0].DialogId);
            Assert.Single(d.SuppressedWarnings!);
        }

        // ---------- RevitUnitConverter sanity ----------

        [Fact]
        public void RevitUnitConverter_Conversions_AreCorrect()
        {
            Assert.Equal(304.8, RevitUnitConverter.FeetToMm(1.0), 6);
            Assert.Equal(1.0, RevitUnitConverter.MmToFeet(304.8), 6);
            Assert.Equal(0.028316846592, RevitUnitConverter.CuFeetToCuMeters(1.0), 9);
            Assert.Equal(180.0, RevitUnitConverter.RadiansToDegrees(Math.PI), 6);
        }

        // ---------- RoslynScriptEngine new usings ----------

        [Fact]
        public void RoslynScriptEngine_SystemTextJson_Using_IsAvailable()
        {
            var engine = new RoslynScriptEngine();
            var context = new MockScriptContext();

            // System.Text.Json is now in the default usings — JsonSerializer must resolve
            // without an explicit using in the script body.
            string code = @"
                var obj = new { name = ""audit"" };
                string json = JsonSerializer.Serialize(obj);
                return json.Contains(""audit"") ? ""ok"" : ""fail"";
            ";

            var result = engine.Execute(code, context, null);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("ok", result.ReturnValue?.ToString());
        }

        [Fact]
        public void RoslynScriptEngine_SystemDiagnostics_Using_IsAvailable()
        {
            var engine = new RoslynScriptEngine();
            var context = new MockScriptContext();

            string code = @"
                var sw = Stopwatch.StartNew();
                return sw != null ? ""ok"" : ""fail"";
            ";

            var result = engine.Execute(code, context, null);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("ok", result.ReturnValue?.ToString());
        }
    }
}
