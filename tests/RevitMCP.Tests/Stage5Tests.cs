using System;
using System.Collections.Generic;
using System.Text.Json;
using RevitMCP.Shared.Commands;
using RevitMCP.Shared.Units;
using Xunit;

namespace RevitMCP.Tests
{
    public class Stage5Tests
    {
        [Fact]
        public void UnitConverter_MmAndFeet_RoundtripAccuracy()
        {
            double mm = 3000.0;
            double feet = RevitUnitConverter.MmToFeet(mm);
            double backMm = RevitUnitConverter.FeetToMm(feet);

            Assert.True(Math.Abs(mm - backMm) < 1e-6);
            Assert.True(Math.Abs(feet - 9.842519685) < 1e-4);
        }

        [Fact]
        public void UnitConverter_DegreesAndRadians_RoundtripAccuracy()
        {
            double degrees = 90.0;
            double radians = RevitUnitConverter.DegreesToRadians(degrees);
            double backDegrees = RevitUnitConverter.RadiansToDegrees(radians);

            Assert.True(Math.Abs(radians - (Math.PI / 2.0)) < 1e-6);
            Assert.True(Math.Abs(degrees - backDegrees) < 1e-6);
        }

        [Fact]
        public void ExportViewImageRequest_Serialization_PreservesAllFields()
        {
            var req = new ExportViewImageRequest
            {
                ViewId = 123456,
                ViewName = "Level 1 - Floor Plan",
                Format = "png",
                PixelSize = 1920,
                ReturnBase64 = true,
                OutputFilePath = @"C:\Temp\view.png"
            };

            string json = JsonSerializer.Serialize(req);
            var deserialized = JsonSerializer.Deserialize<ExportViewImageRequest>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(123456, deserialized!.ViewId);
            Assert.Equal("Level 1 - Floor Plan", deserialized.ViewName);
            Assert.Equal("png", deserialized.Format);
            Assert.Equal(1920, deserialized.PixelSize);
            Assert.True(deserialized.ReturnBase64);
            Assert.Equal(@"C:\Temp\view.png", deserialized.OutputFilePath);
        }

        [Fact]
        public void ExportViewImageResult_Serialization_Roundtrip()
        {
            var res = new ExportViewImageResult
            {
                Success = true,
                ViewId = 1001,
                ViewName = "{3D}",
                Format = "png",
                Width = 1920,
                Height = 1080,
                FileSizeBytes = 512000,
                Base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
                MimeType = "image/png"
            };

            string json = JsonSerializer.Serialize(res);
            var deserialized = JsonSerializer.Deserialize<ExportViewImageResult>(json);

            Assert.NotNull(deserialized);
            Assert.True(deserialized!.Success);
            Assert.Equal(1001, deserialized.ViewId);
            Assert.Equal("image/png", deserialized.MimeType);
            Assert.StartsWith("iVBORw", deserialized.Base64Data);
        }

        [Fact]
        public void TransformElementsRequest_MoveAndRotate_Serialization()
        {
            var moveReq = new TransformElementsRequest
            {
                Action = "move",
                ElementIds = new List<long> { 101, 102 },
                Dx = 500,
                Dy = -200,
                Dz = 0
            };

            string moveJson = JsonSerializer.Serialize(moveReq);
            var desMove = JsonSerializer.Deserialize<TransformElementsRequest>(moveJson);

            Assert.NotNull(desMove);
            Assert.Equal("move", desMove!.Action);
            Assert.Equal(2, desMove.ElementIds.Count);
            Assert.Equal(500, desMove.Dx);

            var rotateReq = new TransformElementsRequest
            {
                Action = "rotate",
                ElementIds = new List<long> { 101 },
                AngleDegrees = 45.0,
                AxisOrigin = new TransformPoint3D(1000, 2000, 0),
                AxisDirection = new TransformPoint3D(0, 0, 1)
            };

            string rotJson = JsonSerializer.Serialize(rotateReq);
            var desRot = JsonSerializer.Deserialize<TransformElementsRequest>(rotJson);

            Assert.NotNull(desRot);
            Assert.Equal("rotate", desRot!.Action);
            Assert.Equal(45.0, desRot.AngleDegrees);
            Assert.NotNull(desRot.AxisOrigin);
            Assert.Equal(1000, desRot.AxisOrigin!.X);
        }

        [Fact]
        public void CreateElementsRequest_WallAndGrid_Serialization()
        {
            var wallReq = new CreateElementsRequest
            {
                Action = "wall",
                Wall = new CreateWallSpec
                {
                    StartPoint = new TransformPoint3D(0, 0, 0),
                    EndPoint = new TransformPoint3D(6000, 0, 0),
                    Height = 3000,
                    Offset = 0,
                    IsStructural = true,
                    LevelId = 500
                }
            };

            string wallJson = JsonSerializer.Serialize(wallReq);
            var desWall = JsonSerializer.Deserialize<CreateElementsRequest>(wallJson);

            Assert.NotNull(desWall);
            Assert.Equal("wall", desWall!.Action);
            Assert.NotNull(desWall.Wall);
            Assert.Equal(6000, desWall.Wall!.EndPoint.X);
            Assert.True(desWall.Wall.IsStructural);

            var gridReq = new CreateElementsRequest
            {
                Action = "grid",
                Grid = new CreateGridSpec
                {
                    Name = "1",
                    StartPoint = new TransformPoint3D(0, -10000, 0),
                    EndPoint = new TransformPoint3D(0, 10000, 0)
                }
            };

            string gridJson = JsonSerializer.Serialize(gridReq);
            var desGrid = JsonSerializer.Deserialize<CreateElementsRequest>(gridJson);

            Assert.NotNull(desGrid);
            Assert.Equal("grid", desGrid!.Action);
            Assert.NotNull(desGrid.Grid);
            Assert.Equal("1", desGrid.Grid!.Name);
        }

        [Fact]
        public void CreateElementsRequest_FamilyInstanceAndRoom_Serialization()
        {
            var fiReq = new CreateElementsRequest
            {
                Action = "family_instance",
                FamilyInstance = new CreateFamilyInstanceSpec
                {
                    FamilyName = "Одиночная дверь",
                    TypeName = "900 x 2100 мм",
                    Location = new TransformPoint3D(3000, 0, 0),
                    HostElementId = 2001
                }
            };

            string fiJson = JsonSerializer.Serialize(fiReq);
            var desFi = JsonSerializer.Deserialize<CreateElementsRequest>(fiJson);

            Assert.NotNull(desFi);
            Assert.Equal("family_instance", desFi!.Action);
            Assert.Equal("Одиночная дверь", desFi.FamilyInstance!.FamilyName);
            Assert.Equal(2001, desFi.FamilyInstance.HostElementId);

            var roomReq = new CreateElementsRequest
            {
                Action = "room",
                Room = new CreateRoomSpec
                {
                    Name = "Кабинет",
                    Number = "101",
                    LevelId = 500,
                    Point = new TransformPoint3D(1500, 1500, 0)
                }
            };

            string roomJson = JsonSerializer.Serialize(roomReq);
            var desRoom = JsonSerializer.Deserialize<CreateElementsRequest>(roomJson);

            Assert.NotNull(desRoom);
            Assert.Equal("room", desRoom!.Action);
            Assert.Equal("Кабинет", desRoom.Room!.Name);
            Assert.Equal("101", desRoom.Room.Number);
        }
    }
}
