using System.Text.RegularExpressions;

namespace NexusShell.Services
{
    /// <summary>
    /// Converts Gemini markdown output to Spectre.Console markup strings.
    /// Processes in order: escape brackets, code blocks, headers, bold, italic, inline code, bullets.
    /// </summary>
    public static class MarkdownRenderer
    {
        /// <summary>
        /// Converts a markdown string from Gemini into a Spectre.Console-compatible markup string.
        /// </summary>
        public static string ToSpectreMarkup(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            // 1. Escape raw square brackets BEFORE injecting any markup
            string s = raw.Replace("[", "[[").Replace("]", "]]");

            // 2. Fenced code blocks (``` ... ```)
            s = Regex.Replace(s, @"```[\w]*\n([\s\S]*?)```",
                m => $"\n[grey on grey11]{m.Groups[1].Value.Trim()}[/]\n",
                RegexOptions.Multiline);

            // 3. Headers (before bold so # doesn't interfere)
            s = Regex.Replace(s, @"^## (.+)$", "[bold cyan]$1[/]", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^# (.+)$",  "[bold underline cyan]$1[/]", RegexOptions.Multiline);

            // 4. Bold **text**
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "[bold]$1[/]");

            // 5. Italic *text*
            s = Regex.Replace(s, @"\*(.+?)\*", "[italic]$1[/]");

            // 6. Inline code `text`
            s = Regex.Replace(s, @"`([^`]+)`", "[cyan on grey15]$1[/]");

            // 7. Bullet points
            s = Regex.Replace(s, @"^- (.+)$", "• $1", RegexOptions.Multiline);

            return s;
        }
    }
}
