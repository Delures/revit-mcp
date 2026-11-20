using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RevitMCP.Shared.BimMap
{
    public class BimMapElementRecord
    {
        public long Id { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public long? LevelId { get; set; }
        public string? LevelName { get; set; }
        public long? RoomId { get; set; }
        public string? RoomName { get; set; }
        public string? Mark { get; set; }
        public string? Comments { get; set; }
        public double[]? BoundingBoxMin { get; set; }
        public double[]? BoundingBoxMax { get; set; }
        public string? ParametersJson { get; set; }
    }

    public class BimMapDatabase : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private SqliteConnection? _connection;

        public string DatabasePath => _dbPath;

        public BimMapDatabase(string projectGuid, string? baseDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(projectGuid))
            {
                throw new ArgumentException("ProjectGuid cannot be empty.", nameof(projectGuid));
            }

            string sanitizedGuid = Path.GetFileNameWithoutExtension(projectGuid);
            string dir = baseDirectory ?? GetDefaultCacheDirectory();
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _dbPath = Path.Combine(dir, $"{sanitizedGuid}.db");
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();
        }

        public static string GetDefaultCacheDirectory()
        {
            return McpStorageContext.ModelsDirectory;
        }

        public static bool Exists(string projectGuid, string? baseDirectory = null)
        {
            string sanitizedGuid = Path.GetFileNameWithoutExtension(projectGuid);
            string dir = baseDirectory ?? GetDefaultCacheDirectory();
            return File.Exists(Path.Combine(dir, $"{sanitizedGuid}.db"));
        }

        private SqliteConnection GetOpenConnection()
        {
            if (_connection == null)
            {
                _connection = new SqliteConnection(_connectionString);
                _connection.Open();
                ConfigurePragmas(_connection);
            }
            else if (_connection.State != System.Data.ConnectionState.Open)
            {
                _connection.Open();
                ConfigurePragmas(_connection);
            }

            return _connection;
        }

        private static void ConfigurePragmas(SqliteConnection conn)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA busy_timeout = 5000;
                    PRAGMA cache_size = -8192;
                    PRAGMA auto_vacuum = INCREMENTAL;";
                cmd.ExecuteNonQuery();
            }
        }

        public void InitializeSchema()
        {
            var conn = GetOpenConnection();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS project_meta (
                        key TEXT PRIMARY KEY,
                        value TEXT
                    );

                    CREATE TABLE IF NOT EXISTS levels (
                        id INTEGER PRIMARY KEY,
                        name TEXT NOT NULL,
                        elevation_mm REAL NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS grids (
                        id INTEGER PRIMARY KEY,
                        name TEXT NOT NULL,
                        is_curved INTEGER NOT NULL DEFAULT 0,
                        start_x_mm REAL,
                        start_y_mm REAL,
                        end_x_mm REAL,
                        end_y_mm REAL
                    );

                    CREATE TABLE IF NOT EXISTS category_summary (
                        category_name TEXT PRIMARY KEY,
                        built_in_name TEXT,
                        count INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS rooms (
                        id INTEGER PRIMARY KEY,
                        number TEXT,
                        name TEXT,
                        level_id INTEGER,
                        level_name TEXT,
                        area_m2 REAL,
                        volume_m3 REAL,
                        min_x_mm REAL,
                        min_y_mm REAL,
                        min_z_mm REAL,
                        max_x_mm REAL,
                        max_y_mm REAL,
                        max_z_mm REAL,
                        element_count INTEGER DEFAULT 0
                    );

                    CREATE TABLE IF NOT EXISTS elements (
                        id INTEGER PRIMARY KEY,
                        category_name TEXT NOT NULL,
                        family_name TEXT,
                        type_name TEXT,
                        level_id INTEGER,
                        level_name TEXT,
                        room_id INTEGER,
                        mark TEXT,
                        comments TEXT,
                        min_x_mm REAL,
                        min_y_mm REAL,
                        min_z_mm REAL,
                        max_x_mm REAL,
                        max_y_mm REAL,
                        max_z_mm REAL,
                        parameters_json TEXT
                    );

                    CREATE INDEX IF NOT EXISTS idx_elements_category ON elements(category_name);
                    CREATE INDEX IF NOT EXISTS idx_elements_level ON elements(level_id);
                    CREATE INDEX IF NOT EXISTS idx_elements_room ON elements(room_id);

                    CREATE VIRTUAL TABLE IF NOT EXISTS elements_fts USING fts5(
                        element_id UNINDEXED,
                        category,
                        family,
                        type_name,
                        room_name,
                        mark,
                        comments,
                        parameters
                    );";
                cmd.ExecuteNonQuery();
            }
        }

        public void SaveMetadata(Dictionary<string, string> metadata)
        {
            var conn = GetOpenConnection();
            using (var trans = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    cmd.CommandText = "INSERT OR REPLACE INTO project_meta (key, value) VALUES (@k, @v);";
                    var pKey = cmd.Parameters.Add("@k", SqliteType.Text);
                    var pVal = cmd.Parameters.Add("@v", SqliteType.Text);

                    foreach (var kvp in metadata)
                    {
                        pKey.Value = kvp.Key;
                        pVal.Value = kvp.Value ?? string.Empty;
                        cmd.ExecuteNonQuery();
                    }
                }
                trans.Commit();
            }
        }

        public string? GetMetadata(string key)
        {
            var conn = GetOpenConnection();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT value FROM project_meta WHERE key = @k LIMIT 1;";
                cmd.Parameters.AddWithValue("@k", key);
                var val = cmd.ExecuteScalar();
                return val != null && val != DBNull.Value ? val.ToString() : null;
            }
        }

        public Dictionary<string, string> GetAllMetadata()
        {
            var res = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var conn = GetOpenConnection();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT key, value FROM project_meta;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string k = reader.GetString(0);
                        string v = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        res[k] = v;
                    }
                }
            }
            return res;
        }

        public void SaveTopology(BimMapTopology topology)
        {
            var conn = GetOpenConnection();
            using (var trans = conn.BeginTransaction())
            {
                // Clear existing
                using (var clearCmd = conn.CreateCommand())
                {
                    clearCmd.Transaction = trans;
                    clearCmd.CommandText = "DELETE FROM levels; DELETE FROM grids; DELETE FROM category_summary;";
                    clearCmd.ExecuteNonQuery();
                }

                // Levels
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    cmd.CommandText = "INSERT OR REPLACE INTO levels (id, name, elevation_mm) VALUES (@id, @name, @elev);";
                    var pId = cmd.Parameters.Add("@id", SqliteType.Integer);
                    var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                    var pElev = cmd.Parameters.Add("@elev", SqliteType.Real);

                    foreach (var lvl in topology.Levels)
                    {
                        pId.Value = lvl.Id;
                        pName.Value = lvl.Name;
                        pElev.Value = lvl.ElevationMm;
                        cmd.ExecuteNonQuery();
                    }
                }

                // Grids
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    cmd.CommandText = @"INSERT OR REPLACE INTO grids (id, name, is_curved, start_x_mm, start_y_mm, end_x_mm, end_y_mm) 
                        VALUES (@id, @name, @curved, @sx, @sy, @ex, @ey);";
                    var pId = cmd.Parameters.Add("@id", SqliteType.Integer);
                    var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                    var pCurved = cmd.Parameters.Add("@curved", SqliteType.Integer);
                    var pSx = cmd.Parameters.Add("@sx", SqliteType.Real);
                    var pSy = cmd.Parameters.Add("@sy", SqliteType.Real);
                    var pEx = cmd.Parameters.Add("@ex", SqliteType.Real);
                    var pEy = cmd.Parameters.Add("@ey", SqliteType.Real);

                    foreach (var g in topology.Grids)
                    {
                        pId.Value = g.Id;
                        pName.Value = g.Name;
                        pCurved.Value = g.IsCurved ? 1 : 0;
                        pSx.Value = (object?)g.StartXMm ?? DBNull.Value;
                        pSy.Value = (object?)g.StartYMm ?? DBNull.Value;
                        pEx.Value = (object?)g.EndXMm ?? DBNull.Value;
                        pEy.Value = (object?)g.EndYMm ?? DBNull.Value;
                        cmd.ExecuteNonQuery();
                    }
                }

                // Category Summary
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    cmd.CommandText = "INSERT OR REPLACE INTO category_summary (category_name, count) VALUES (@cat, @cnt);";
                    var pCat = cmd.Parameters.Add("@cat", SqliteType.Text);
                    var pCnt = cmd.Parameters.Add("@cnt", SqliteType.Integer);

                    foreach (var kvp in topology.CategoryCounts)
                    {
                        pCat.Value = kvp.Key;
                        pCnt.Value = kvp.Value;
                        cmd.ExecuteNonQuery();
                    }
                }

                trans.Commit();
            }
        }

        public void SaveSpatial(BimMapSpatial spatial)
        {
            var conn = GetOpenConnection();
            using (var trans = conn.BeginTransaction())
            {
                using (var clearCmd = conn.CreateCommand())
                {
                    clearCmd.Transaction = trans;
                    clearCmd.CommandText = "DELETE FROM rooms;";
                    clearCmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    cmd.CommandText = @"INSERT OR REPLACE INTO rooms (id, number, name, level_id, level_name, area_m2, volume_m3, min_x_mm, min_y_mm, min_z_mm, max_x_mm, max_y_mm, max_z_mm, element_count)
                        VALUES (@id, @num, @name, @lid, @lname, @area, @vol, @minx, @miny, @minz, @maxx, @maxy, @maxz, @elem_cnt);";

                    var pId = cmd.Parameters.Add("@id", SqliteType.Integer);
                    var pNum = cmd.Parameters.Add("@num", SqliteType.Text);
                    var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                    var pLid = cmd.Parameters.Add("@lid", SqliteType.Integer);
                    var pLname = cmd.Parameters.Add("@lname", SqliteType.Text);
                    var pArea = cmd.Parameters.Add("@area", SqliteType.Real);
                    var pVol = cmd.Parameters.Add("@vol", SqliteType.Real);
                    var pMinX = cmd.Parameters.Add("@minx", SqliteType.Real);
                    var pMinY = cmd.Parameters.Add("@miny", SqliteType.Real);
                    var pMinZ = cmd.Parameters.Add("@minz", SqliteType.Real);
                    var pMaxX = cmd.Parameters.Add("@maxx", SqliteType.Real);
                    var pMaxY = cmd.Parameters.Add("@maxy", SqliteType.Real);
                    var pMaxZ = cmd.Parameters.Add("@maxz", SqliteType.Real);
                    var pElemCnt = cmd.Parameters.Add("@elem_cnt", SqliteType.Integer);

                    foreach (var rm in spatial.Rooms)
                    {
                        pId.Value = rm.Id;
                        pNum.Value = rm.Number ?? string.Empty;
                        pName.Value = rm.Name ?? string.Empty;
                        pLid.Value = (object?)rm.LevelId ?? DBNull.Value;
                        pLname.Value = (object?)rm.LevelName ?? DBNull.Value;
                        pArea.Value = rm.AreaM2;
                        pVol.Value = rm.VolumeM3;
                        pElemCnt.Value = rm.ElementCount;

                        if (rm.BoundingBox != null)
                        {
                            pMinX.Value = rm.BoundingBox.Min[0];
                            pMinY.Value = rm.BoundingBox.Min[1];
                            pMinZ.Value = rm.BoundingBox.Min[2];
                            pMaxX.Value = rm.BoundingBox.Max[0];
                            pMaxY.Value = rm.BoundingBox.Max[1];
                            pMaxZ.Value = rm.BoundingBox.Max[2];
                        }
                        else
                        {
                            pMinX.Value = DBNull.Value;
                            pMinY.Value = DBNull.Value;
                            pMinZ.Value = DBNull.Value;
                            pMaxX.Value = DBNull.Value;
                            pMaxY.Value = DBNull.Value;
                            pMaxZ.Value = DBNull.Value;
                        }

                        cmd.ExecuteNonQuery();
                    }
                }

                trans.Commit();
            }
        }

        public void SaveElementsBatch(IEnumerable<BimMapElementRecord> elements)
        {
            var conn = GetOpenConnection();
            using (var trans = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                using (var ftsCmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    ftsCmd.Transaction = trans;

                    cmd.CommandText = @"INSERT OR REPLACE INTO elements 
                        (id, category_name, family_name, type_name, level_id, level_name, room_id, mark, comments, min_x_mm, min_y_mm, min_z_mm, max_x_mm, max_y_mm, max_z_mm, parameters_json)
                        VALUES (@id, @cat, @fam, @type, @lid, @lname, @rid, @mark, @comm, @minx, @miny, @minz, @maxx, @maxy, @maxz, @params);";

                    ftsCmd.CommandText = @"INSERT INTO elements_fts (element_id, category, family, type_name, room_name, mark, comments, parameters)
                        VALUES (@eid, @f_cat, @f_fam, @f_type, @f_room, @f_mark, @f_comm, @f_params);";

                    var pId = cmd.Parameters.Add("@id", SqliteType.Integer);
                    var pCat = cmd.Parameters.Add("@cat", SqliteType.Text);
                    var pFam = cmd.Parameters.Add("@fam", SqliteType.Text);
                    var pType = cmd.Parameters.Add("@type", SqliteType.Text);
                    var pLid = cmd.Parameters.Add("@lid", SqliteType.Integer);
                    var pLname = cmd.Parameters.Add("@lname", SqliteType.Text);
                    var pRid = cmd.Parameters.Add("@rid", SqliteType.Integer);
                    var pMark = cmd.Parameters.Add("@mark", SqliteType.Text);
                    var pComm = cmd.Parameters.Add("@comm", SqliteType.Text);
                    var pMinX = cmd.Parameters.Add("@minx", SqliteType.Real);
                    var pMinY = cmd.Parameters.Add("@miny", SqliteType.Real);
                    var pMinZ = cmd.Parameters.Add("@minz", SqliteType.Real);
                    var pMaxX = cmd.Parameters.Add("@maxx", SqliteType.Real);
                    var pMaxY = cmd.Parameters.Add("@maxy", SqliteType.Real);
                    var pMaxZ = cmd.Parameters.Add("@maxz", SqliteType.Real);
                    var pParams = cmd.Parameters.Add("@params", SqliteType.Text);

                    var pfEid = ftsCmd.Parameters.Add("@eid", SqliteType.Integer);
                    var pfCat = ftsCmd.Parameters.Add("@f_cat", SqliteType.Text);
                    var pfFam = ftsCmd.Parameters.Add("@f_fam", SqliteType.Text);
                    var pfType = ftsCmd.Parameters.Add("@f_type", SqliteType.Text);
                    var pfRoom = ftsCmd.Parameters.Add("@f_room", SqliteType.Text);
                    var pfMark = ftsCmd.Parameters.Add("@f_mark", SqliteType.Text);
                    var pfComm = ftsCmd.Parameters.Add("@f_comm", SqliteType.Text);
                    var pfParams = ftsCmd.Parameters.Add("@f_params", SqliteType.Text);

                    foreach (var elem in elements)
                    {
                        pId.Value = elem.Id;
                        pCat.Value = elem.CategoryName;
                        pFam.Value = (object?)elem.FamilyName ?? DBNull.Value;
                        pType.Value = (object?)elem.TypeName ?? DBNull.Value;
                        pLid.Value = (object?)elem.LevelId ?? DBNull.Value;
                        pLname.Value = (object?)elem.LevelName ?? DBNull.Value;
                        pRid.Value = (object?)elem.RoomId ?? DBNull.Value;
                        pMark.Value = (object?)elem.Mark ?? DBNull.Value;
                        pComm.Value = (object?)elem.Comments ?? DBNull.Value;
                        pParams.Value = (object?)elem.ParametersJson ?? DBNull.Value;

                        if (elem.BoundingBoxMin != null && elem.BoundingBoxMax != null)
                        {
                            pMinX.Value = elem.BoundingBoxMin[0];
                            pMinY.Value = elem.BoundingBoxMin[1];
                            pMinZ.Value = elem.BoundingBoxMin[2];
                            pMaxX.Value = elem.BoundingBoxMax[0];
                            pMaxY.Value = elem.BoundingBoxMax[1];
                            pMaxZ.Value = elem.BoundingBoxMax[2];
                        }
                        else
                        {
                            pMinX.Value = DBNull.Value;
                            pMinY.Value = DBNull.Value;
                            pMinZ.Value = DBNull.Value;
                            pMaxX.Value = DBNull.Value;
                            pMaxY.Value = DBNull.Value;
                            pMaxZ.Value = DBNull.Value;
                        }

                        cmd.ExecuteNonQuery();

                        // FTS
                        pfEid.Value = elem.Id;
                        pfCat.Value = elem.CategoryName;
                        pfFam.Value = elem.FamilyName ?? string.Empty;
                        pfType.Value = elem.TypeName ?? string.Empty;
                        pfRoom.Value = elem.RoomName ?? string.Empty;
                        pfMark.Value = elem.Mark ?? string.Empty;
                        pfComm.Value = elem.Comments ?? string.Empty;
                        pfParams.Value = elem.ParametersJson ?? string.Empty;
                        ftsCmd.ExecuteNonQuery();
                    }
                }

                trans.Commit();
            }
        }

        public BimMapTopology GetTopology()
        {
            var topo = new BimMapTopology();
            var conn = GetOpenConnection();

            // Levels
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, name, elevation_mm FROM levels ORDER BY elevation_mm ASC;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        topo.Levels.Add(new BimMapLevel
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1),
                            ElevationMm = reader.GetDouble(2)
                        });
                    }
                }
            }

            // Grids
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, name, is_curved, start_x_mm, start_y_mm, end_x_mm, end_y_mm FROM grids ORDER BY name ASC;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        topo.Grids.Add(new BimMapGrid
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1),
                            IsCurved = reader.GetInt32(2) == 1,
                            StartXMm = reader.IsDBNull(3) ? null : (double?)reader.GetDouble(3),
                            StartYMm = reader.IsDBNull(4) ? null : (double?)reader.GetDouble(4),
                            EndXMm = reader.IsDBNull(5) ? null : (double?)reader.GetDouble(5),
                            EndYMm = reader.IsDBNull(6) ? null : (double?)reader.GetDouble(6)
                        });
                    }
                }
            }

            // Categories
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT category_name, count FROM category_summary ORDER BY count DESC;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        topo.CategoryCounts[reader.GetString(0)] = reader.GetInt32(1);
                    }
                }
            }

            return topo;
        }

        public BimMapSpatial GetSpatial(long? levelId = null)
        {
            var spatial = new BimMapSpatial();
            var conn = GetOpenConnection();

            using (var cmd = conn.CreateCommand())
            {
                string sql = "SELECT id, number, name, level_id, level_name, area_m2, volume_m3, min_x_mm, min_y_mm, min_z_mm, max_x_mm, max_y_mm, max_z_mm, element_count FROM rooms";
                if (levelId.HasValue)
                {
                    sql += " WHERE level_id = @lid";
                    cmd.Parameters.AddWithValue("@lid", levelId.Value);
                }
                sql += " ORDER BY number ASC;";
                cmd.CommandText = sql;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var rm = new BimMapRoom
                        {
                            Id = reader.GetInt64(0),
                            Number = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            Name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            LevelId = reader.IsDBNull(3) ? null : (long?)reader.GetInt64(3),
                            LevelName = reader.IsDBNull(4) ? null : reader.GetString(4),
                            AreaM2 = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                            VolumeM3 = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                            ElementCount = reader.IsDBNull(13) ? 0 : reader.GetInt32(13)
                        };

                        if (!reader.IsDBNull(7))
                        {
                            rm.BoundingBox = new BimMapBoundingBox
                            {
                                Min = new[] { reader.GetDouble(7), reader.GetDouble(8), reader.GetDouble(9) },
                                Max = new[] { reader.GetDouble(10), reader.GetDouble(11), reader.GetDouble(12) }
                            };
                        }

                        spatial.Rooms.Add(rm);
                    }
                }
            }

            return spatial;
        }

        public List<BimMapSearchResultItem> Search(string query, string? category = null, long? levelId = null, int limit = 50)
        {
            var results = new List<BimMapSearchResultItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            var conn = GetOpenConnection();

            // Prepare FTS5 query with wildcard
            string sanitizedQuery = query.Trim().Replace("'", "''").Replace("\"", "");
            string ftsExpr = $"\"{sanitizedQuery}\"*";

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT f.element_id, e.category_name, e.family_name, e.type_name, e.level_name, e.mark, e.comments, snippet(elements_fts, 7, '<b>', '</b>', '...', 15) as snippet
                    FROM elements_fts f
                    JOIN elements e ON e.id = f.element_id
                    WHERE elements_fts MATCH @q
                    " + (!string.IsNullOrWhiteSpace(category) ? " AND e.category_name = @cat" : "") + @"
                    " + (levelId.HasValue ? " AND e.level_id = @lid" : "") + @"
                    LIMIT @lim;";

                cmd.Parameters.AddWithValue("@q", ftsExpr);
                cmd.Parameters.AddWithValue("@lim", Math.Min(limit, 200));
                if (!string.IsNullOrWhiteSpace(category))
                {
                    cmd.Parameters.AddWithValue("@cat", category);
                }
                if (levelId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@lid", levelId.Value);
                }

                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new BimMapSearchResultItem
                            {
                                ElementId = reader.GetInt64(0),
                                Category = reader.GetString(1),
                                Family = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                TypeName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                LevelName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Mark = reader.IsDBNull(5) ? null : reader.GetString(5),
                                Comments = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Snippet = reader.IsDBNull(7) ? null : reader.GetString(7)
                            });
                        }
                    }
                }
                catch
                {
                    // Fallback to LIKE if FTS query syntax error
                    cmd.CommandText = @"
                        SELECT id, category_name, family_name, type_name, level_name, mark, comments
                        FROM elements
                        WHERE (mark LIKE @like OR comments LIKE @like OR type_name LIKE @like OR family_name LIKE @like)
                        " + (!string.IsNullOrWhiteSpace(category) ? " AND category_name = @cat" : "") + @"
                        " + (levelId.HasValue ? " AND level_id = @lid" : "") + @"
                        LIMIT @lim;";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@like", $"%{sanitizedQuery}%");
                    cmd.Parameters.AddWithValue("@lim", Math.Min(limit, 200));
                    if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("@cat", category);
                    if (levelId.HasValue) cmd.Parameters.AddWithValue("@lid", levelId.Value);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new BimMapSearchResultItem
                            {
                                ElementId = reader.GetInt64(0),
                                Category = reader.GetString(1),
                                Family = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                TypeName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                LevelName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Mark = reader.IsDBNull(5) ? null : reader.GetString(5),
                                Comments = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }
                }
            }

            return results;
        }

        public void ClearAll()
        {
            var conn = GetOpenConnection();
            using (var trans = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trans;
                    cmd.CommandText = @"
                        DELETE FROM project_meta;
                        DELETE FROM levels;
                        DELETE FROM grids;
                        DELETE FROM category_summary;
                        DELETE FROM rooms;
                        DELETE FROM elements;
                        DELETE FROM elements_fts;";
                    cmd.ExecuteNonQuery();
                }
                trans.Commit();
            }
        }

        public void OptimizeAndTruncateWal()
        {
            var conn = GetOpenConnection();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA wal_checkpoint(TRUNCATE);
                    PRAGMA incremental_vacuum(500);";
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                try
                {
                    OptimizeAndTruncateWal();
                    _connection.Close();
                    _connection.Dispose();
                }
                catch { }
                _connection = null;
            }
        }
    }
}
