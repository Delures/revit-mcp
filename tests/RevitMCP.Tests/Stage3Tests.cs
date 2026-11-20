using System;
using System.Collections.Generic;
using System.Text.Json;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Scripting;
using RevitMCP.Shared.Units;
using Xunit;

namespace RevitMCP.Tests
{
    public class Stage3Tests
    {
        private const double Epsilon = 1e-6;

        [Fact]
        public void RevitUnitConverter_ConvertsLengthUnitsAccurately()
        {
            // 304.8 mm = 1.0 ft
            double feetFromMm = RevitUnitConverter.ConvertToInternal(304.8, "mm");
            Assert.True(Math.Abs(feetFromMm - 1.0) < Epsilon);

            double mmFromFeet = RevitUnitConverter.ConvertFromInternal(1.0, "mm");
            Assert.True(Math.Abs(mmFromFeet - 304.8) < Epsilon);

            // 1.0 meter = 1000 / 304.8 ft = 3.280839895 ft
            double feetFromM = RevitUnitConverter.ConvertToInternal(1.0, "m");
            Assert.True(Math.Abs(feetFromM - (1.0 / 0.3048)) < Epsilon);

            // Russian alias: "мм"
            double feetFromMmRu = RevitUnitConverter.ConvertToInternal(609.6, "мм");
            Assert.True(Math.Abs(feetFromMmRu - 2.0) < Epsilon);

            // Centimeters: 30.48 cm = 1.0 ft
            double feetFromCm = RevitUnitConverter.ConvertToInternal(30.48, "cm");
            Assert.True(Math.Abs(feetFromCm - 1.0) < Epsilon);

            // Inches: 12 in = 1.0 ft
            double feetFromIn = RevitUnitConverter.ConvertToInternal(12.0, "in");
            Assert.True(Math.Abs(feetFromIn - 1.0) < Epsilon);
        }

        [Fact]
        public void RevitUnitConverter_ConvertsAreaAndVolumeUnitsAccurately()
        {
            // Area: 1 m2
            double internalSqFt = RevitUnitConverter.ConvertToInternal(1.0, "m2");
            double backToM2 = RevitUnitConverter.ConvertFromInternal(internalSqFt, "m2");
            Assert.True(Math.Abs(backToM2 - 1.0) < Epsilon);

            // Russian alias: "м2"
            double internalSqFtRu = RevitUnitConverter.ConvertToInternal(10.0, "м2");
            double backToM2Ru = RevitUnitConverter.ConvertFromInternal(internalSqFtRu, "м2");
            Assert.True(Math.Abs(backToM2Ru - 10.0) < Epsilon);

            // Volume: 1 m3
            double internalCuFt = RevitUnitConverter.ConvertToInternal(1.0, "m3");
            double backToM3 = RevitUnitConverter.ConvertFromInternal(internalCuFt, "m3");
            Assert.True(Math.Abs(backToM3 - 1.0) < Epsilon);

            // Russian alias: "м3"
            double internalCuFtRu = RevitUnitConverter.ConvertToInternal(5.5, "м3");
            double backToM3Ru = RevitUnitConverter.ConvertFromInternal(internalCuFtRu, "м3");
            Assert.True(Math.Abs(backToM3Ru - 5.5) < Epsilon);
        }

        [Fact]
        public void RevitUnitConverter_ConvertsAngleUnitsAccurately()
        {
            // 180 degrees = PI radians
            double rad = RevitUnitConverter.ConvertToInternal(180.0, "degrees");
            Assert.True(Math.Abs(rad - Math.PI) < Epsilon);

            double deg = RevitUnitConverter.ConvertFromInternal(Math.PI, "degrees");
            Assert.True(Math.Abs(deg - 180.0) < Epsilon);

            // Russian alias: "градус"
            double radRu = RevitUnitConverter.ConvertToInternal(90.0, "градус");
            Assert.True(Math.Abs(radRu - (Math.PI / 2.0)) < Epsilon);
        }

        [Fact]
        public void ManageParameters_SerializationRoundtrip()
        {
            var req = new ManageParametersRequest
            {
                Action = "set",
                ElementIds = new List<long> { 123456, 789012 },
                Parameters = new List<ParameterSpec>
                {
                    new ParameterSpec { Name = "Марка", Value = "W-01" },
                    new ParameterSpec { BuiltInName = "WALL_USER_HEIGHT_PARAM", Value = 3000.0, Unit = "mm" },
                    new ParameterSpec { Guid = "00000000-0000-0000-0000-000000000001", Value = 42 }
                }
            };

            string json = JsonSerializer.Serialize(req);
            var deserialized = JsonSerializer.Deserialize<ManageParametersRequest>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("set", deserialized!.Action);
            Assert.Equal(2, deserialized.ElementIds.Count);
            Assert.Equal(3, deserialized.Parameters.Count);
            Assert.Equal("Марка", deserialized.Parameters[0].Name);
            Assert.Equal("mm", deserialized.Parameters[1].Unit);

            var res = new ManageParametersResult
            {
                Success = true,
                Action = "set",
                TotalUpdated = 2,
                Warnings = new List<WarningRecord>
                {
                    new WarningRecord { Severity = "Warning", Message = "Elements have duplicate Mark values." }
                },
                Elements = new List<ElementParametersResult>
                {
                    new ElementParametersResult
                    {
                        ElementId = 123456,
                        Success = true,
                        Parameters = new Dictionary<string, ParameterValueResult>
                        {
                            ["Марка"] = new ParameterValueResult
                            {
                                Name = "Марка",
                                Value = "W-01",
                                DisplayValue = "W-01",
                                StorageType = "String",
                                Updated = true
                            }
                        }
                    }
                }
            };

            string resJson = JsonSerializer.Serialize(res);
            var deserializedRes = JsonSerializer.Deserialize<ManageParametersResult>(resJson);

            Assert.NotNull(deserializedRes);
            Assert.True(deserializedRes!.Success);
            Assert.Equal(2, deserializedRes.TotalUpdated);
            Assert.NotNull(deserializedRes.Warnings);
            Assert.Single(deserializedRes.Warnings!);
            Assert.Equal("Elements have duplicate Mark values.", deserializedRes.Warnings![0].Message);
        }

        [Fact]
        public void ManageDocuments_SerializationRoundtrip()
        {
            var req = new ManageDocumentsRequest
            {
                Action = "sync_with_central",
                Comment = "Automated sync from AI Agent"
            };

            string json = JsonSerializer.Serialize(req);
            var deserialized = JsonSerializer.Deserialize<ManageDocumentsRequest>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("sync_with_central", deserialized!.Action);
            Assert.Equal("Automated sync from AI Agent", deserialized.Comment);

            var res = new ManageDocumentsResult
            {
                Success = true,
                Action = "list",
                ActiveDocument = new DocumentInfo
                {
                    Title = "Project1.rvt",
                    PathName = "C:\\Projects\\Project1.rvt",
                    IsActive = true,
                    IsWorkshared = true,
                    IsModified = false
                },
                Documents = new List<DocumentInfo>
                {
                    new DocumentInfo
                    {
                        Title = "Project1.rvt",
                        PathName = "C:\\Projects\\Project1.rvt",
                        IsActive = true,
                        IsWorkshared = true,
                        IsModified = false
                    }
                },
                Message = "Found 1 open document(s)."
            };

            string resJson = JsonSerializer.Serialize(res);
            var deserializedRes = JsonSerializer.Deserialize<ManageDocumentsResult>(resJson);

            Assert.NotNull(deserializedRes);
            Assert.True(deserializedRes!.Success);
            Assert.NotNull(deserializedRes.ActiveDocument);
            Assert.Equal("Project1.rvt", deserializedRes.ActiveDocument!.Title);
            Assert.True(deserializedRes.ActiveDocument.IsWorkshared);
        }
    }
}
