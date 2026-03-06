using FluentAssertions;
using NexusShell.Services;
using Xunit;

namespace NexusShell.Tests
{
    public class MarkdownRendererTests
    {
        [Fact]
        public void Bold_ShouldConvertToSpectreMarkup()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("**hello**");
            result.Should().Be("[bold]hello[/]");
        }

        [Fact]
        public void H1_ShouldConvertToUnderlineCyan()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("# Title");
            result.Should().Be("[bold underline cyan]Title[/]");
        }

        [Fact]
        public void H2_ShouldConvertToBoldCyan()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("## Subtitle");
            result.Should().Be("[bold cyan]Subtitle[/]");
        }

        [Fact]
        public void InlineCode_ShouldConvertToMonospace()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("Use `dotnet run`");
            result.Should().Be("Use [cyan on grey15]dotnet run[/]");
        }

        [Fact]
        public void Bullet_ShouldConvertToBulletChar()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("- item one");
            result.Should().Be("• item one");
        }

        [Fact]
        public void SquareBrackets_ShouldBeEscaped()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("see [docs]");
            result.Should().Be("see [[docs]]");
        }

        [Fact]
        public void PlainText_ShouldPassThrough()
        {
            var result = MarkdownRenderer.ToSpectreMarkup("just plain text");
            result.Should().Be("just plain text");
        }
    }
}
