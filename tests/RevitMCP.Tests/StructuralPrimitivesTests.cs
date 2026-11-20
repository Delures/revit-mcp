using System.Collections.Generic;
using System.Text.Json;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Scripting;
using RevitMCP.Shared.Units;
using Xunit;

namespace RevitMCP.Tests
{
    public class StructuralPrimitivesTests
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [Fact]
        public void CreateSlab_Serialization_Roundtrip()
        {
            var req = new CreateElementsRequest
            {
                Action = "slab",
                Slab = new CreateSlabSpec
                {
                    LevelId = 1001,
                    FloorTypeId = 2002,
                    HeightOffset = 50,
                    IsStructural = true,
                    BoundaryPoints = new List<TransformPoint3D>
                    {
                        new TransformPoint3D { X = 0, Y = 0, Z = 0 },
                        new TransformPoint3D { X = 6000, Y = 0, Z = 0 },
                        new TransformPoint3D { X = 6000, Y = 6000, Z = 0 },
                        new TransformPoint3D { X = 0, Y = 6000, Z = 0 }
                    },
                    Holes = new List<List<TransformPoint3D>>
                    {
                        new List<TransformPoint3D>
                        {
                            new TransformPoint3D { X = 1000, Y = 1000, Z = 0 },
                            new TransformPoint3D { X = 2000, Y = 1000, Z = 0 },
                            new TransformPoint3D { X = 2000, Y = 2000, Z = 0 },
                            new TransformPoint3D { X = 1000, Y = 2000, Z = 0 }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            Assert.Contains("\"action\":\"slab\"", json);
            Assert.Contains("\"boundaryPoints\"", json);
            Assert.Contains("\"holes\"", json);

            var deserialized = JsonSerializer.Deserialize<CreateElementsRequest>(json, _jsonOptions);
            Assert.NotNull(deserialized);
            Assert.Equal("slab", deserialized!.Action);
            Assert.NotNull(deserialized.Slab);
            Assert.Equal(1001, deserialized.Slab!.LevelId);
            Assert.Equal(4, deserialized.Slab.BoundaryPoints.Count);
            Assert.NotNull(deserialized.Slab.Holes);
            Assert.Single(deserialized.Slab.Holes!);
            Assert.Equal(4, deserialized.Slab.Holes![0].Count);
        }

        [Fact]
        public void CreateBeam_Serialization_Roundtrip()
        {
            var req = new CreateElementsRequest
            {
                Action = "beam",
                Beam = new CreateBeamSpec
                {
                    LevelId = 1001,
                    FamilySymbolId = 3003,
                    FamilyName = "Балка железобетонная",
                    TypeName = "400х600",
                    StartPoint = new TransformPoint3D { X = 0, Y = 0, Z = 3000 },
                    EndPoint = new TransformPoint3D { X = 6000, Y = 0, Z = 3000 }
                }
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            Assert.Contains("\"action\":\"beam\"", json);

            var deserialized = JsonSerializer.Deserialize<CreateElementsRequest>(json, _jsonOptions);
            Assert.NotNull(deserialized?.Beam);
            Assert.Equal("Балка железобетонная", deserialized!.Beam!.FamilyName);
            Assert.Equal("400х600", deserialized.Beam.TypeName);
            Assert.Equal(6000, deserialized.Beam.EndPoint.X);
        }

        [Fact]
        public void CreateColumn_Serialization_Roundtrip()
        {
            var req = new CreateElementsRequest
            {
                Action = "column",
                Column = new CreateColumnSpec
                {
                    BaseLevelId = 1001,
                    TopLevelId = 1002,
                    BaseOffset = 0,
                    TopOffset = -200,
                    RotationDegrees = 45.0,
                    FamilyName = "Колонна ЖБ",
                    TypeName = "400х400",
                    Location = new TransformPoint3D { X = 3000, Y = 3000, Z = 0 }
                }
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<CreateElementsRequest>(json, _jsonOptions);

            Assert.NotNull(deserialized?.Column);
            Assert.Equal(1001, deserialized!.Column!.BaseLevelId);
            Assert.Equal(1002, deserialized.Column.TopLevelId);
            Assert.Equal(-200, deserialized.Column.TopOffset);
            Assert.Equal(45.0, deserialized.Column.RotationDegrees);
        }

        [Fact]
        public void CreateAreaReinforcement_Serialization_Roundtrip()
        {
            var req = new CreateElementsRequest
            {
                Action = "area_reinforcement",
                AreaReinforcement = new CreateAreaReinforcementSpec
                {
                    HostElementId = 5005,
                    TopMajorBarTypeId = 1205,
                    TopMinorBarTypeId = 1205,
                    BottomMajorBarTypeId = 1206,
                    BottomMinorBarTypeId = 1206,
                    TopMajorSpacing = 200,
                    TopMinorSpacing = 200,
                    BottomMajorSpacing = 150,
                    BottomMinorSpacing = 150,
                    MajorDirection = new TransformPoint3D { X = 1, Y = 0, Z = 0 }
                }
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<CreateElementsRequest>(json, _jsonOptions);

            Assert.NotNull(deserialized?.AreaReinforcement);
            Assert.Equal(5005, deserialized!.AreaReinforcement!.HostElementId);
            Assert.Equal(150, deserialized.AreaReinforcement.BottomMajorSpacing);
            Assert.Equal(1.0, deserialized.AreaReinforcement.MajorDirection.X);
        }

        [Fact]
        public void CreateRebar_Serialization_Roundtrip()
        {
            var req = new CreateElementsRequest
            {
                Action = "rebar",
                Rebar = new CreateRebarSpec
                {
                    HostElementId = 5005,
                    BarTypeId = 1205,
                    LayoutRule = "MaximumSpacing",
                    Spacing = 200,
                    Quantity = 10,
                    ArrayLength = 2000,
                    Normal = new TransformPoint3D { X = 0, Y = 0, Z = 1 },
                    CurveSegments = new List<List<TransformPoint3D>>
                    {
                        new List<TransformPoint3D>
                        {
                            new TransformPoint3D { X = 0, Y = 0, Z = 100 },
                            new TransformPoint3D { X = 2500, Y = 0, Z = 100 }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<CreateElementsRequest>(json, _jsonOptions);

            Assert.NotNull(deserialized?.Rebar);
            Assert.Equal(5005, deserialized!.Rebar!.HostElementId);
            Assert.Equal("MaximumSpacing", deserialized.Rebar.LayoutRule);
            Assert.Equal(200, deserialized.Rebar.Spacing);
            Assert.Single(deserialized.Rebar.CurveSegments);
            Assert.Equal(2, deserialized.Rebar.CurveSegments[0].Count);
        }

        [Fact]
        public void StructuralUnitMath_FeetAndMm_AreAccurate()
        {
            // 200 mm to feet
            double mm = 200;
            double feet = RevitUnitConverter.MmToFeet(mm);
            double backToMm = RevitUnitConverter.FeetToMm(feet);

            Assert.True(System.Math.Abs(mm - backToMm) < 1e-6);

            // 90 degrees to radians
            double deg = 90.0;
            double rad = RevitUnitConverter.DegreesToRadians(deg);
            double backToDeg = RevitUnitConverter.RadiansToDegrees(rad);

            Assert.True(System.Math.Abs(deg - backToDeg) < 1e-6);
        }

        [Fact]
        public void CreateFamilyInstance_UniversalOverloads_Serialization_Roundtrip()
        {
            var req = new CreateElementsRequest
            {
                Action = "family_instance",
                FamilyInstance = new CreateFamilyInstanceSpec
                {
                    FamilyName = "Окно_Двустворчатое",
                    TypeName = "1200х1500",
                    Location = new TransformPoint3D { X = 2500, Y = 0, Z = 900 },
                    StartPoint = new TransformPoint3D { X = 1000, Y = 0, Z = 0 },
                    EndPoint = new TransformPoint3D { X = 3000, Y = 0, Z = 0 },
                    ReferenceDirection = new TransformPoint3D { X = 0, Y = 1, Z = 0 },
                    LevelId = 101,
                    HostElementId = 202,
                    ViewId = 303,
                    StructuralType = "Footing",
                    RotationDegrees = 30.0
                }
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            var deserialized = JsonSerializer.Deserialize<CreateElementsRequest>(json, _jsonOptions);

            Assert.NotNull(deserialized?.FamilyInstance);
            Assert.Equal("Окно_Двустворчатое", deserialized!.FamilyInstance!.FamilyName);
            Assert.Equal("1200х1500", deserialized.FamilyInstance.TypeName);
            Assert.Equal(2500, deserialized.FamilyInstance.Location.X);
            Assert.NotNull(deserialized.FamilyInstance.StartPoint);
            Assert.Equal(1000, deserialized.FamilyInstance.StartPoint!.X);
            Assert.NotNull(deserialized.FamilyInstance.EndPoint);
            Assert.Equal(3000, deserialized.FamilyInstance.EndPoint!.X);
            Assert.NotNull(deserialized.FamilyInstance.ReferenceDirection);
            Assert.Equal(1, deserialized.FamilyInstance.ReferenceDirection!.Y);
            Assert.Equal(101, deserialized.FamilyInstance.LevelId);
            Assert.Equal(202, deserialized.FamilyInstance.HostElementId);
            Assert.Equal(303, deserialized.FamilyInstance.ViewId);
            Assert.Equal("Footing", deserialized.FamilyInstance.StructuralType);
            Assert.Equal(30.0, deserialized.FamilyInstance.RotationDegrees);
        }
    }
}
