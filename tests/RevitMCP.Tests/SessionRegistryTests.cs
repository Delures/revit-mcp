using System;
using System.IO;
using System.Linq;
using RevitMCP.Shared.Session;
using Xunit;

namespace RevitMCP.Tests
{
    [Collection("Sequential")]
    public class SessionRegistryTests
    {
        [Fact]
        public void SessionRegistry_CanRegisterAndUnregister()
        {
            int testPid = 999999;
            string testVersion = "2025";
            var session = new RevitSessionInfo
            {
                ProcessId = testPid,
                RevitVersion = testVersion,
                PipeName = $"RevitMCP-{testVersion}-{testPid}",
                MainWindowTitle = "Test Revit Project - 3D View",
                ActiveDocumentTitle = "TestProject.rvt",
                IsDocumentOpen = true,
                UiState = RevitUiStates.Ready
            };

            SessionRegistry.RegisterSession(session);
            string expectedPath = SessionRegistry.GetSessionFilePath(testVersion, testPid);
            Assert.True(File.Exists(expectedPath));

            // Without pruning dead sessions (since 999999 isn't a real process)
            var active = SessionRegistry.GetActiveSessions(pruneDeadSessions: false);
            Assert.Contains(active, s => s.ProcessId == testPid);

            SessionRegistry.UnregisterSession(testVersion, testPid);
            Assert.False(File.Exists(expectedPath));
        }
    }
}
