using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RevitMCP.Shared.BimMap;
using Xunit;

namespace RevitMCP.Tests
{
    public class Stage4Tests : IDisposable
    {
        private readonly string _testDir;

        public Stage4Tests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "RevitMCP_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void BimMapDatabase_InitializesSchema_AndStoresMetadata()
        {
            string projectGuid = "test-project-101";
            using (var db = new BimMapDatabase(projectGuid, _testDir))
            {
                db.InitializeSchema();
                db.SaveMetadata(new Dictionary<string, string>
                {
                    ["project_guid"] = projectGuid,
                    ["version_guid"] = "v-1.0.0",
                    ["document_title"] = "Test Tower.rvt"
                });

                string? title = db.GetMetadata("document_title");
                string? version = db.GetMetadata("version_guid");

                Assert.Equal("Test Tower.rvt", title);
                Assert.Equal("v-1.0.0", version);
            }
        }

        [Fact]
        public void BimMapDatabase_StoresAndRetrieves_Topology()
        {
            string projectGuid = "test-topo-project";
            using (var db = new BimMapDatabase(projectGuid, _testDir))
            {
                db.InitializeSchema();

                var topo = new BimMapTopology
                {
                    Levels = new List<BimMapLevel>
                    {
                        new BimMapLevel { Id = 10, Name = "Level 1", ElevationMm = 0.0 },
                        new BimMapLevel { Id = 20, Name = "Level 2", ElevationMm = 3300.0 }
                    },
                    Grids = new List<BimMapGrid>
                    {
                        new BimMapGrid { Id = 100, Name = "1", IsCurved = false, StartXMm = 0, StartYMm = 0, EndXMm = 0, EndYMm = 10000 },
                        new BimMapGrid { Id = 101, Name = "A", IsCurved = false, StartXMm = 0, StartYMm = 0, EndXMm = 10000, EndYMm = 0 }
                    },
                    CategoryCounts = new Dictionary<string, int>
                    {
                        ["Стены"] = 150,
                        ["Двери"] = 45,
                        ["Колонны"] = 28
                    }
                };

                db.SaveTopology(topo);

                var loadedTopo = db.GetTopology();

                Assert.Equal(2, loadedTopo.Levels.Count);
                Assert.Equal("Level 1", loadedTopo.Levels[0].Name);
                Assert.Equal(3300.0, loadedTopo.Levels[1].ElevationMm);

                Assert.Equal(2, loadedTopo.Grids.Count);
                Assert.Equal("1", loadedTopo.Grids[0].Name);

                Assert.Equal(3, loadedTopo.CategoryCounts.Count);
                Assert.Equal(150, loadedTopo.CategoryCounts["Стены"]);
            }
        }

        [Fact]
        public void BimMapDatabase_StoresAndRetrieves_SpatialRooms()
        {
            string projectGuid = "test-spatial-project";
            using (var db = new BimMapDatabase(projectGuid, _testDir))
            {
                db.InitializeSchema();

                var spatial = new BimMapSpatial
                {
                    Rooms = new List<BimMapRoom>
                    {
                        new BimMapRoom
                        {
                            Id = 501,
                            Number = "101",
                            Name = "Кухня",
                            LevelId = 10,
                            LevelName = "Level 1",
                            AreaM2 = 24.5,
                            VolumeM3 = 73.5,
                            ElementCount = 8,
                            BoundingBox = new BimMapBoundingBox
                            {
                                Min = new[] { 0.0, 0.0, 0.0 },
                                Max = new[] { 5000.0, 5000.0, 3000.0 }
                            }
                        },
                        new BimMapRoom
                        {
                            Id = 502,
                            Number = "201",
                            Name = "Конференц-зал",
                            LevelId = 20,
                            LevelName = "Level 2",
                            AreaM2 = 85.0,
                            VolumeM3 = 255.0,
                            ElementCount = 20
                        }
                    }
                };

                db.SaveSpatial(spatial);

                var allRooms = db.GetSpatial();
                Assert.Equal(2, allRooms.Rooms.Count);

                var level1Rooms = db.GetSpatial(levelId: 10);
                Assert.Single(level1Rooms.Rooms);
                Assert.Equal("Кухня", level1Rooms.Rooms[0].Name);
                Assert.NotNull(level1Rooms.Rooms[0].BoundingBox);
                Assert.Equal(5000.0, level1Rooms.Rooms[0].BoundingBox!.Max[0]);
            }
        }

        [Fact]
        public void BimMapDatabase_Fts5FullTextSearch_WorksInstantly()
        {
            string projectGuid = "test-fts-project";
            using (var db = new BimMapDatabase(projectGuid, _testDir))
            {
                db.InitializeSchema();

                var elements = new List<BimMapElementRecord>
                {
                    new BimMapElementRecord
                    {
                        Id = 1001,
                        CategoryName = "Несущие колонны",
                        FamilyName = "Железобетонная колонна",
                        TypeName = "400x400 B25",
                        LevelId = 10,
                        LevelName = "Level 1",
                        RoomName = "Холл",
                        Mark = "К-1",
                        Comments = "Усиленное армирование у стыка",
                        ParametersJson = "{\"Бетон\":\"B25\",\"Высота\":3300}"
                    },
                    new BimMapElementRecord
                    {
                        Id = 1002,
                        CategoryName = "Двери",
                        FamilyName = "Металлическая дверь",
                        TypeName = "ДО 21-10",
                        LevelId = 10,
                        LevelName = "Level 1",
                        RoomName = "Кухня",
                        Mark = "Д-01",
                        Comments = "Противопожарная EI60",
                        ParametersJson = "{\"Огнестойкость\":\"EI60\"}"
                    },
                    new BimMapElementRecord
                    {
                        Id = 1003,
                        CategoryName = "Стены",
                        FamilyName = "Базовая стена",
                        TypeName = "Кирпич 250мм",
                        LevelId = 20,
                        LevelName = "Level 2",
                        Mark = "С-1",
                        Comments = "Несущая стена лестничной клетки"
                    }
                };

                db.SaveElementsBatch(elements);

                // Search by Russian keyword
                var foundColumns = db.Search("колонна");
                Assert.Single(foundColumns);
                Assert.Equal(1001, foundColumns[0].ElementId);
                Assert.Equal("К-1", foundColumns[0].Mark);

                // Search by Mark
                var foundDoor = db.Search("Д-01");
                Assert.Single(foundDoor);
                Assert.Equal("Двери", foundDoor[0].Category);

                // Search by Comment keyword
                var foundFireDoor = db.Search("Противопожарная");
                Assert.Single(foundFireDoor);
                Assert.Equal(1002, foundFireDoor[0].ElementId);

                // Search with category filter
                var wallSearch = db.Search("стена", category: "Стены");
                Assert.Single(wallSearch);
                Assert.Equal(1003, wallSearch[0].ElementId);
            }
        }

        [Fact]
        public void CacheCleaner_Status_AndPruning_EnforcesBudget()
        {
            string cacheDir = Path.Combine(_testDir, "CacheStorage");
            Directory.CreateDirectory(cacheDir);

            // Create fake database files
            string oldDb = Path.Combine(cacheDir, "old-project.db");
            File.WriteAllBytes(oldDb, new byte[1024 * 1024 * 2]); // 2 MB
            File.SetLastWriteTimeUtc(oldDb, DateTime.UtcNow.AddDays(-40)); // 40 days old

            string newDb = Path.Combine(cacheDir, "new-project.db");
            File.WriteAllBytes(newDb, new byte[1024 * 1024 * 5]); // 5 MB
            File.SetLastWriteTimeUtc(newDb, DateTime.UtcNow);

            // Check status
            var status = CacheCleaner.GetStatus(cacheDir);
            Assert.Equal(2, status.Projects.Count);
            Assert.True(status.TotalSizeMb >= 7.0);

            // Clean with 30 days retention
            var cleanResult = CacheCleaner.Clean(maxSizeMb: 100, retentionDays: 30, cacheDirectory: cacheDir);
            Assert.Single(cleanResult.CleanedFiles);
            Assert.Contains("old-project.db", cleanResult.CleanedFiles[0]);
            Assert.False(File.Exists(oldDb));
            Assert.True(File.Exists(newDb));

            // Clean with aggressive size limit (e.g. 2 MB limit when 5 MB file exists)
            var cleanLru = CacheCleaner.Clean(maxSizeMb: 2, retentionDays: 365, cacheDirectory: cacheDir);
            Assert.Single(cleanLru.CleanedFiles);
            Assert.Contains("new-project.db", cleanLru.CleanedFiles[0]);
            Assert.False(File.Exists(newDb));
        }
    }
}
