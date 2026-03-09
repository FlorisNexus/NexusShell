using System;
using System.IO;
using Moq;
using NexusShell;
using NexusShell.Interfaces;
using NexusShell.Services;
using Xunit;

namespace NexusShell.Tests
{
    public class PlanServiceTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly NexusSettings _settings;
        private readonly Mock<IHistoryService> _history;
        private readonly PlanService _sut;

        public PlanServiceTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempRoot);
            // NEXT.md must exist before we append to it
            File.WriteAllText(Path.Combine(_tempRoot, "NEXT.md"), "# Pending Work\n");

            _settings = new NexusSettings(
                ReposRoot: _tempRoot,
                ConductorRoot: _tempRoot,
                Version: "vTest",
                Role: "Admin",
                CloudSyncPath: _tempRoot);

            _history = new Mock<IHistoryService>();
            _sut = new PlanService(_settings, _history.Object);
        }

        [Fact]
        public void CreatePlanPair_CreatesExpectedFiles()
        {
            var (planPath, promptPath) = _sut.CreatePlanPair("continuum", "rate-limiting");

            Assert.True(File.Exists(planPath));
            Assert.True(File.Exists(promptPath));
        }

        [Fact]
        public void CreatePlanPair_PlanContainsProjectAndSlug()
        {
            var (planPath, _) = _sut.CreatePlanPair("finova", "iban-validation");

            string content = File.ReadAllText(planPath);
            Assert.Contains("finova", content);
            Assert.Contains("iban-validation", content);
        }

        [Fact]
        public void CreatePlanPair_PromptContainsDoneInstruction()
        {
            var (_, promptPath) = _sut.CreatePlanPair("continuum", "rate-limiting");

            string content = File.ReadAllText(promptPath);
            Assert.Contains("[DONE]", content);
            Assert.Contains("NEXT.md", content);
        }

        [Fact]
        public void CreatePlanPair_AppendsToPendingWorkFile()
        {
            _sut.CreatePlanPair("patrimio", "tenant-billing");

            string nextMd = File.ReadAllText(Path.Combine(_tempRoot, "NEXT.md"));
            Assert.Contains("patrimio-gemini-prompt-tenant-billing-", nextMd);
            Assert.Contains("[ ]", nextMd);
        }

        [Fact]
        public void CreatePlanPair_LogsHistoryEvent()
        {
            _sut.CreatePlanPair("finova", "vat-check");

            _history.Verify(h => h.AddEvent(It.Is<string>(s => s.Contains("finova-plan-vat-check"))), Times.Once);
        }

        [Fact]
        public void CreatePlanPair_CreatesProjectSubdirectory()
        {
            _sut.CreatePlanPair("nexusshell", "some-feature");

            string dir = Path.Combine(_tempRoot, "plans", "nexusshell");
            Assert.True(Directory.Exists(dir));
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch {}
        }
    }
}
