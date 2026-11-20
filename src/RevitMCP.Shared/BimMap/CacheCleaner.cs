using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitMCP.Shared.BimMap
{
    public static class CacheCleaner
    {
        public static string GetDefaultCacheDirectory()
        {
            return McpStorageContext.ModelsDirectory;
        }

        public static ManageCacheResult GetStatus(string? cacheDirectory = null)
        {
            string dir = cacheDirectory ?? GetDefaultCacheDirectory();
            var res = new ManageCacheResult { Action = "status" };

            if (!Directory.Exists(dir))
            {
                res.Message = "Cache directory does not exist yet.";
                return res;
            }

            var di = new DirectoryInfo(dir);
            var dbFiles = di.GetFiles("*.db", SearchOption.TopDirectoryOnly);
            var allFiles = di.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".db-wal", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".db-shm", StringComparison.OrdinalIgnoreCase))
                .ToList();

            long totalBytes = allFiles.Sum(f => f.Length);
            res.TotalSizeMb = Math.Round(totalBytes / (1024.0 * 1024.0), 2);
            res.TotalFiles = allFiles.Count;

            foreach (var db in dbFiles)
            {
                long projectBytes = db.Length;
                string baseName = Path.GetFileNameWithoutExtension(db.FullName);

                // Add size of wal and shm
                var wal = new FileInfo(Path.Combine(dir, $"{baseName}.db-wal"));
                if (wal.Exists) projectBytes += wal.Length;
                var shm = new FileInfo(Path.Combine(dir, $"{baseName}.db-shm"));
                if (shm.Exists) projectBytes += shm.Length;

                res.Projects.Add(new CacheProjectInfo
                {
                    ProjectGuid = baseName,
                    FilePath = db.FullName,
                    SizeBytes = projectBytes,
                    LastModifiedUtc = db.LastWriteTimeUtc
                });
            }

            res.Message = $"Cache holds {res.Projects.Count} projects ({res.TotalSizeMb} MB across {res.TotalFiles} files).";
            return res;
        }

        public static ManageCacheResult Clean(int maxSizeMb = 2048, int retentionDays = 30, string? cacheDirectory = null)
        {
            string dir = cacheDirectory ?? GetDefaultCacheDirectory();
            var res = new ManageCacheResult { Action = "clean" };

            if (!Directory.Exists(dir))
            {
                res.Message = "Cache directory does not exist.";
                return res;
            }

            var di = new DirectoryInfo(dir);
            var dbFiles = di.GetFiles("*.db", SearchOption.TopDirectoryOnly).ToList();
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            long freedBytes = 0;
            var cleanedList = new List<string>();

            // Phase 1: Retention pruning (older than retentionDays)
            foreach (var db in dbFiles.ToList())
            {
                if (db.LastWriteTimeUtc < cutoff)
                {
                    long bytes = DeleteProjectFiles(dir, db.Name);
                    freedBytes += bytes;
                    cleanedList.Add($"{db.Name} (expired: {db.LastWriteTimeUtc:yyyy-MM-dd})");
                    dbFiles.Remove(db);
                }
            }

            // Phase 2: LRU Budget pruning (if total exceeds maxSizeMb)
            long maxBytes = (long)maxSizeMb * 1024 * 1024;
            var remainingFiles = di.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".db-wal", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".db-shm", StringComparison.OrdinalIgnoreCase))
                .ToList();

            long currentBytes = remainingFiles.Sum(f => f.Length);

            if (currentBytes > maxBytes)
            {
                // Sort remaining db files by LastWriteTimeUtc ascending (oldest first)
                var sorted = dbFiles.OrderBy(f => f.LastWriteTimeUtc).ToList();
                foreach (var db in sorted)
                {
                    if (currentBytes <= maxBytes) break;

                    long bytes = DeleteProjectFiles(dir, db.Name);
                    freedBytes += bytes;
                    currentBytes -= bytes;
                    cleanedList.Add($"{db.Name} (LRU quota exceeded)");
                }
            }

            res.FreedMb = Math.Round(freedBytes / (1024.0 * 1024.0), 2);
            res.CleanedFiles = cleanedList;

            // Re-fetch remaining status
            var afterStatus = GetStatus(dir);
            res.TotalSizeMb = afterStatus.TotalSizeMb;
            res.TotalFiles = afterStatus.TotalFiles;
            res.Projects = afterStatus.Projects;
            res.Message = $"Cleaned {cleanedList.Count} projects, freed {res.FreedMb} MB. Current cache size: {res.TotalSizeMb} MB.";

            return res;
        }

        private static long DeleteProjectFiles(string dir, string dbFileName)
        {
            long freed = 0;
            string baseName = Path.GetFileNameWithoutExtension(dbFileName);
            string[] patterns = { $"{baseName}.db", $"{baseName}.db-wal", $"{baseName}.db-shm" };

            foreach (var p in patterns)
            {
                string path = Path.Combine(dir, p);
                try
                {
                    if (File.Exists(path))
                    {
                        var fi = new FileInfo(path);
                        freed += fi.Length;
                        File.Delete(path);
                    }
                }
                catch { }
            }

            return freed;
        }
    }
}
