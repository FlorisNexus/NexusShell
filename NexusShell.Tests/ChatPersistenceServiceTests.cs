using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NexusShell.Models;
using NexusShell.Services;
using Xunit;

namespace NexusShell.Tests
{
    public class ChatPersistenceServiceTests : IDisposable
    {
        private readonly string _tempPath;

        public ChatPersistenceServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempPath)) Directory.Delete(_tempPath, true);
        }

        [Fact]
        public void LoadHistory_ShouldReturnEmpty_WhenFileDoesNotExist()
        {
            var svc = new ChatPersistenceService();
            var result = svc.LoadHistory(_tempPath);
            result.Should().BeEmpty();
        }

        [Fact]
        public void SaveAndLoad_ShouldRoundTrip()
        {
            var svc = new ChatPersistenceService();
            var turns = new List<ConversationTurn>
            {
                new() { Role = "user", Content = "hello", Timestamp = new DateTime(2026, 3, 5, 10, 0, 0) },
                new() { Role = "ai",   Content = "world", Timestamp = new DateTime(2026, 3, 5, 10, 0, 1) }
            };

            svc.SaveHistory(_tempPath, turns);
            var loaded = svc.LoadHistory(_tempPath);

            loaded.Should().HaveCount(2);
            loaded[0].Role.Should().Be("user");
            loaded[0].Content.Should().Be("hello");
            loaded[1].Role.Should().Be("ai");
            loaded[1].Content.Should().Be("world");
        }

        [Fact]
        public void SaveHistory_ShouldCapAt50Turns()
        {
            var svc = new ChatPersistenceService();
            var turns = new List<ConversationTurn>();
            for (int i = 0; i < 60; i++)
                turns.Add(new ConversationTurn { Role = "user", Content = $"turn {i}" });

            svc.SaveHistory(_tempPath, turns);
            var loaded = svc.LoadHistory(_tempPath);

            loaded.Should().HaveCount(50);
            loaded[0].Content.Should().Be("turn 10"); // last 50 of 60
        }
    }
}
