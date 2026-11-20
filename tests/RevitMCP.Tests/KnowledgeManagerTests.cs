using System;
using System.IO;
using System.Linq;
using RevitMCP.Shared.Knowledge;
using Xunit;

namespace RevitMCP.Tests
{
    public class KnowledgeManagerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly AgentKnowledgeManager _manager;

        public KnowledgeManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "RevitMcpKnowledgeTests_" + Guid.NewGuid().ToString("N"));
            _manager = new AgentKnowledgeManager(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        [Fact]
        public void RecordLesson_CreatesMarkdownFileWithFrontmatter()
        {
            var request = new RecordLessonRequest
            {
                Topic = "Wall Creation Coordinates",
                Category = "Geometry",
                RevitVersions = new System.Collections.Generic.List<string> { "2024", "2025" },
                Content = "### Проблема\nКоординаты в Revit API задаются в футах, а не миллиметрах.\n### Решение\nВсегда делить на 304.8."
            };

            var lesson = _manager.RecordLesson(request);

            Assert.NotNull(lesson);
            Assert.Equal("Wall Creation Coordinates", lesson.Topic);
            Assert.Equal("Geometry", lesson.Category);
            Assert.True(File.Exists(lesson.FilePath));

            string fileContent = File.ReadAllText(lesson.FilePath);
            Assert.Contains("---", fileContent);
            Assert.Contains("topic: \"Wall Creation Coordinates\"", fileContent);
            Assert.Contains("category: \"Geometry\"", fileContent);
            Assert.Contains("Координаты в Revit API задаются в футах", fileContent);
        }

        [Fact]
        public void Query_FindsLessonsByKeyword()
        {
            _manager.RecordLesson(new RecordLessonRequest
            {
                Topic = "Duplicate Mark Warning",
                Category = "Transactions",
                Content = "Предупреждение DuplicateMark подавляется через DeleteWarning."
            });

            _manager.RecordLesson(new RecordLessonRequest
            {
                Topic = "Column Insertion",
                Category = "Geometry",
                Content = "Колонны привязываются к уровням."
            });

            var searchMark = _manager.Query("DuplicateMark");
            Assert.Single(searchMark);
            Assert.Equal("Duplicate Mark Warning", searchMark[0].Topic);

            var searchGeo = _manager.Query("", category: "Geometry");
            Assert.Single(searchGeo);
            Assert.Equal("Column Insertion", searchGeo[0].Topic);
        }

        [Fact]
        public void GetAggregatedRules_FormatsMarkdown()
        {
            _manager.RecordLesson(new RecordLessonRequest
            {
                Topic = "Rule 1",
                Category = "API",
                Content = "Do not call TaskDialog.Show."
            });

            string markdown = _manager.GetAggregatedRules();

            Assert.Contains("# Revit MCP Knowledge Base", markdown);
            Assert.Contains("Do not call TaskDialog.Show", markdown);
        }
    }
}
