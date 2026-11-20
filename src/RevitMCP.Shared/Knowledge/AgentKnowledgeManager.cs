using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RevitMCP.Shared.Knowledge
{
    public class AgentKnowledgeManager
    {
        private readonly string _storageDir;
        private static readonly object _fileLock = new object();

        public AgentKnowledgeManager(string? storageDir = null)
        {
            _storageDir = string.IsNullOrWhiteSpace(storageDir)
                ? McpStorageContext.KnowledgeDirectory
                : storageDir!;

            if (!Directory.Exists(_storageDir))
            {
                Directory.CreateDirectory(_storageDir);
            }
        }

        public string StorageDirectory => _storageDir;

        public KnowledgeLesson RecordLesson(RecordLessonRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Topic))
            {
                throw new ArgumentException("Topic cannot be empty", nameof(request));
            }

            string safeFileName = SanitizeFileName(request.Topic) + ".md";
            string filePath = Path.Combine(_storageDir, safeFileName);

            var lesson = new KnowledgeLesson
            {
                Id = Path.GetFileNameWithoutExtension(safeFileName),
                Topic = request.Topic.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
                RevitVersions = request.RevitVersions ?? new List<string>(),
                Created = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Content = request.Content?.Trim() ?? string.Empty,
                FilePath = filePath
            };

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine($"topic: \"{EscapeYaml(lesson.Topic)}\"");
            sb.AppendLine($"category: \"{EscapeYaml(lesson.Category)}\"");
            sb.AppendLine($"created: \"{lesson.Created}\"");
            if (lesson.RevitVersions.Count > 0)
            {
                sb.AppendLine("revitVersions: [" + string.Join(", ", lesson.RevitVersions.Select(v => $"\"{v}\"")) + "]");
            }
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(lesson.Content);

            lock (_fileLock)
            {
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }

            return lesson;
        }

        public List<KnowledgeLesson> GetAllLessons()
        {
            var list = new List<KnowledgeLesson>();

            if (!Directory.Exists(_storageDir))
            {
                return list;
            }

            string[] files;
            lock (_fileLock)
            {
                files = Directory.GetFiles(_storageDir, "*.md");
            }

            foreach (var file in files)
            {
                try
                {
                    var lesson = ParseLessonFile(file);
                    if (lesson != null)
                    {
                        list.Add(lesson);
                    }
                }
                catch
                {
                    // Ignore malformed files
                }
            }

            return list.OrderByDescending(l => l.Created).ToList();
        }

        public List<KnowledgeLesson> Query(string query, string? category = null)
        {
            var all = GetAllLessons();
            string lowerQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            string? lowerCategory = category?.Trim().ToLowerInvariant();

            return all.Where(lesson =>
            {
                if (!string.IsNullOrWhiteSpace(lowerCategory) &&
                    !lesson.Category.ToLowerInvariant().Contains(lowerCategory))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(lowerQuery))
                {
                    return true;
                }

                return lesson.Topic.ToLowerInvariant().Contains(lowerQuery) ||
                       lesson.Category.ToLowerInvariant().Contains(lowerQuery) ||
                       lesson.Content.ToLowerInvariant().Contains(lowerQuery);
            }).ToList();
        }

        public string GetAggregatedRules(string? targetRevitVersion = null)
        {
            var lessons = GetAllLessons();
            if (lessons.Count == 0)
            {
                return "# Revit MCP Knowledge Base & Rules\n\nNo rules recorded yet. Use `record_lesson` to record insights and best practices.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Revit MCP Knowledge Base & API Best Practices");
            sb.AppendLine($"*Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*\n");

            var grouped = lessons.GroupBy(l => l.Category).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                sb.AppendLine($"## Category: {group.Key}\n");
                foreach (var lesson in group)
                {
                    if (!string.IsNullOrWhiteSpace(targetRevitVersion) &&
                        lesson.RevitVersions.Count > 0 &&
                        !lesson.RevitVersions.Contains(targetRevitVersion!))
                    {
                        continue; // Skip rules not applicable to current version
                    }

                    sb.AppendLine($"### {lesson.Topic}");
                    sb.AppendLine($"*Created: {lesson.Created}* | *Versions: {(lesson.RevitVersions.Count > 0 ? string.Join(", ", lesson.RevitVersions) : "All")}*\n");
                    sb.AppendLine(lesson.Content);
                    sb.AppendLine("\n---");
                }
            }

            return sb.ToString();
        }

        private static KnowledgeLesson? ParseLessonFile(string filePath)
        {
            string content;
            lock (_fileLock)
            {
                content = File.ReadAllText(filePath, Encoding.UTF8);
            }

            var lesson = new KnowledgeLesson
            {
                Id = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath
            };

            var match = Regex.Match(content, @"^---\r?\n(.*?)\r?\n---\r?\n(.*)$", RegexOptions.Singleline);
            if (!match.Success)
            {
                lesson.Topic = lesson.Id;
                lesson.Content = content;
                return lesson;
            }

            string frontmatter = match.Groups[1].Value;
            lesson.Content = match.Groups[2].Value.Trim();

            var lines = frontmatter.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx <= 0) continue;

                string key = line.Substring(0, colonIdx).Trim().ToLowerInvariant();
                string val = line.Substring(colonIdx + 1).Trim().Trim('"', '\'');

                switch (key)
                {
                    case "topic":
                        lesson.Topic = val;
                        break;
                    case "category":
                        lesson.Category = val;
                        break;
                    case "created":
                        lesson.Created = val;
                        break;
                    case "revitversions":
                        var vMatches = Regex.Matches(val, @"[""']([^""']+)[""']");
                        foreach (Match m in vMatches)
                        {
                            lesson.RevitVersions.Add(m.Groups[1].Value);
                        }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(lesson.Topic))
            {
                lesson.Topic = lesson.Id;
            }

            return lesson;
        }

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var clean = new string(name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
            clean = Regex.Replace(clean, @"\s+", "_");
            return clean.Trim('_');
        }

        private static string EscapeYaml(string str)
        {
            return str.Replace("\"", "\\\"");
        }
    }
}
