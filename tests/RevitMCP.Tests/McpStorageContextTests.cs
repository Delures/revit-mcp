using System;
using System.IO;
using RevitMCP.Shared;
using RevitMCP.Shared.BimMap;
using RevitMCP.Shared.Ipc;
using RevitMCP.Shared.Knowledge;
using RevitMCP.Shared.Session;
using Xunit;

namespace RevitMCP.Tests
{
    [Collection("Sequential")]
    public class McpStorageContextTests : IDisposable
    {
        public McpStorageContextTests()
        {
            McpStorageContext.Reset();
        }

        public void Dispose()
        {
            McpStorageContext.Reset();
        }

        [Fact]
        public void Defaults_AreRevitMcp()
        {
            Assert.Equal("RevitMCP", McpStorageContext.McpName);
            Assert.Equal("RevitMCP-", McpStorageContext.PipePrefix);

            string expectedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RevitMCP");

            Assert.Equal(expectedRoot, McpStorageContext.RootDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "models"), McpStorageContext.ModelsDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "sessions"), McpStorageContext.SessionsDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "knowledge"), McpStorageContext.KnowledgeDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "logs"), McpStorageContext.LogsDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "tools"), McpStorageContext.ToolsDirectory);
        }

        [Fact]
        public void ChangingMcpName_UpdatesAllPathsAndPipes()
        {
            McpStorageContext.McpName = "BimCore";

            Assert.Equal("BimCore", McpStorageContext.McpName);
            Assert.Equal("BimCore-", McpStorageContext.PipePrefix);

            string expectedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BimCore");

            Assert.Equal(expectedRoot, McpStorageContext.RootDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "models"), McpStorageContext.ModelsDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "sessions"), McpStorageContext.SessionsDirectory);

            // NamedPipeUtils should automatically reflect new prefix
            string pipeName = NamedPipeUtils.GetPipeName("2022", 12345);
            Assert.Equal("BimCore-2022-12345", pipeName);

            // Subsystem directory helpers should reflect the change
            Assert.Equal(McpStorageContext.ModelsDirectory, BimMapDatabase.GetDefaultCacheDirectory());
            Assert.Equal(McpStorageContext.ModelsDirectory, CacheCleaner.GetDefaultCacheDirectory());
            Assert.Equal(McpStorageContext.SessionsDirectory, SessionRegistry.GetSessionsDirectory());

            var km = new AgentKnowledgeManager();
            Assert.Equal(McpStorageContext.KnowledgeDirectory, km.StorageDirectory);
        }

        [Fact]
        public void Initialize_WithCustomName_SetsMcpName()
        {
            McpStorageContext.Initialize("CustomMCP");
            Assert.Equal("CustomMCP", McpStorageContext.McpName);
            Assert.Equal("CustomMCP-", McpStorageContext.PipePrefix);
        }
    }
}
