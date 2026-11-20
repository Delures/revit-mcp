using System;
using System.Collections.Generic;
using System.Text.Json;
using RevitMCP.Shared.Commands;
using Xunit;

namespace RevitMCP.Tests
{
    public class CheckIntersectionsTests
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        [Fact]
        public void CheckIntersectionsRequest_Serialization_Roundtrip()
        {
            var req = new CheckIntersectionsRequest
            {
                TargetElementId = 324100,
                CandidateCategories = new List<string> { "Колонны", "OST_StructuralColumns", "Стены" },
                CandidateElementIds = new List<long> { 101, 102, 103 },
                ActiveViewOnly = true,
                CalculateClashVolume = true,
                Limit = 50
            };

            string json = JsonSerializer.Serialize(req, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<CheckIntersectionsRequest>(json, _jsonOptions);

            Assert.NotNull(deserialized);
            Assert.Equal(324100, deserialized!.TargetElementId);
            Assert.Equal(3, deserialized.CandidateCategories?.Count);
            Assert.Contains("OST_StructuralColumns", deserialized.CandidateCategories!);
            Assert.Equal(3, deserialized.CandidateElementIds?.Count);
            Assert.True(deserialized.ActiveViewOnly);
            Assert.True(deserialized.CalculateClashVolume);
            Assert.Equal(50, deserialized.Limit);
        }

        [Fact]
        public void CheckIntersectionsResult_WithClashRecords_Serialization_Roundtrip()
        {
            var res = new CheckIntersectionsResult
            {
                Success = true,
                TargetElementId = 324100,
                TargetName = "Перекрытие монолитное 200мм",
                TargetCategory = "Перекрытия",
                TotalIntersections = 2,
                ExecutionTimeMs = 18,
                Intersections = new List<ClashRecord>
                {
                    new ClashRecord
                    {
                        ElementId = 405101,
                        CategoryName = "Несущие колонны",
                        FamilyName = "Колонна ЖБ",
                        TypeName = "400х400",
                        ElementName = "Колонна ЖБ : 400х400",
                        ClashVolumeM3 = 0.032,
                        ClashBoundingBoxMm = new ClashBboxMm
                        {
                            Min = new[] { 1000.0, 2000.0, 3000.0 },
                            Max = new[] { 1400.0, 2400.0, 3200.0 }
                        }
                    },
                    new ClashRecord
                    {
                        ElementId = 506202,
                        CategoryName = "Воздуховоды",
                        FamilyName = "Воздуховод прямоугольный",
                        TypeName = "500х300",
                        ElementName = "Воздуховод : 500х300",
                        ClashVolumeM3 = 0.045
                    }
                }
            };

            string json = JsonSerializer.Serialize(res, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<CheckIntersectionsResult>(json, _jsonOptions);

            Assert.NotNull(deserialized);
            Assert.True(deserialized!.Success);
            Assert.Equal(324100, deserialized.TargetElementId);
            Assert.Equal("Перекрытие монолитное 200мм", deserialized.TargetName);
            Assert.Equal(2, deserialized.TotalIntersections);
            Assert.Equal(2, deserialized.Intersections.Count);

            var clash1 = deserialized.Intersections[0];
            Assert.Equal(405101, clash1.ElementId);
            Assert.Equal("Несущие колонны", clash1.CategoryName);
            Assert.Equal(0.032, clash1.ClashVolumeM3);
            Assert.NotNull(clash1.ClashBoundingBoxMm);
            Assert.Equal(1000.0, clash1.ClashBoundingBoxMm!.Min[0]);
            Assert.Equal(1400.0, clash1.ClashBoundingBoxMm!.Max[0]);

            var clash2 = deserialized.Intersections[1];
            Assert.Equal(506202, clash2.ElementId);
            Assert.Equal("Воздуховоды", clash2.CategoryName);
            Assert.Equal(0.045, clash2.ClashVolumeM3);
        }

        [Fact]
        public void CheckIntersectionsResult_ErrorState()
        {
            var res = new CheckIntersectionsResult
            {
                Success = false,
                TargetElementId = -1,
                ErrorCode = "TARGET_ELEMENT_NOT_FOUND",
                ErrorMessage = "Target element was not found."
            };

            string json = JsonSerializer.Serialize(res, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<CheckIntersectionsResult>(json, _jsonOptions);

            Assert.NotNull(deserialized);
            Assert.False(deserialized!.Success);
            Assert.Equal("TARGET_ELEMENT_NOT_FOUND", deserialized.ErrorCode);
        }
    }
}
