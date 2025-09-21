using System.Text;
using System.Text.RegularExpressions;

namespace OpenMarkdown.Dialects;

/// <summary>
/// A renderer class that converts Markdown to HTML, following the CommonMark specification.
/// This implementation uses a state-machine approach to process block-level elements
/// and a series of regular expressions to handle inline formatting.
/// </summary>
public class CommonMarkdown : BaseMarkdown
{
    // The following regular expressions define the syntax rules for various Markdown elements.
    // They are compiled for better performance during repeated matching.

    /// <summary>
    /// A precise regex for matching inline code spans (e.g., `code` or ``two backticks``).
    /// </summary>
    /// <remarks>
    /// This pattern is designed to correctly match varying lengths of backtick delimiters (` ` `)
    /// while ensuring the content inside does not contain a delimiter of the same length.
    /// - Group 1: The opening backtick run (e.g., ```).
    /// - Group 2: The content inside the code span, matched minimally.
    /// - `\1`: A backreference to Group 1, ensuring the closing delimiter is the same length as the opening one.
    /// - Negative lookarounds (`(?<!`)` and `(?!`)`): Prevents the pattern from over-consuming extra backticks.
    /// </remarks>
    protected virtual Regex ReCodeSpan => new(@"(?<!`)(`+)([\s\S]*?)(?<!`)\1(?!`)", RegexOptions.Compiled);

    /// <summary>Regex for matching **strong** emphasis.</summary>
    protected virtual Regex ReStrong   => new(@"(\*\*|__)(?=\S)([\s\S]*?\S)\1", RegexOptions.Compiled);
    /// <summary>Regex for matching *emphasis*.</summary>
    protected virtual Regex ReEm       => new(@"(\*|_)(?=\S)([\s\S]*?\S)\1", RegexOptions.Compiled);
    /// <summary>Regex for matching inline images: `![alt](url "title")`.</summary>
    protected virtual Regex ReImage    => new(@"!\[([^\]]*)\]\((\S+?)(?:\s+""(.*?)"")?\)", RegexOptions.Compiled);
    /// <summary>Regex for matching inline links: `[label](url "title")`.</summary>
    protected virtual Regex ReLink     => new(@"\[([^\]]+)\]\((\S+?)(?:\s+""(.*?)"")?\)", RegexOptions.Compiled);

    /// <summary>Regex for matching horizontal rules: `---`, `***`, or `___`.</summary>
    protected virtual Regex ReHr          => new(@"^\s*(\*{3,}|-{3,}|_{3,})\s*$", RegexOptions.Compiled);
    /// <summary>Regex for matching unordered list markers: `*`, `+`, or `-`.</summary>
    protected virtual Regex ReUlMarker    => new(@"^\s*([*+\-])\s+(.+)$", RegexOptions.Compiled);
    /// <summary>Regex for matching ordered list markers: `1.`, `2)`, etc.</summary>
    protected virtual Regex ReOlMarker    => new(@"^\s*(\d+)[.)]\s+(.+)$", RegexOptions.Compiled);
    /// <summary>Regex for matching the start of a fenced code block: ```` or `~~~`.</summary>
    protected virtual Regex ReFenceStart  => new(@"^\s*(```|~~~)\s*([A-Za-z0-9_\-+]*)\s*$", RegexOptions.Compiled);
    /// <summary>Regex for matching the end of a fenced code block.</summary>
    protected virtual Regex ReFenceEnd    => new(@"^\s*(```|~~~)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Renders a given Markdown string to HTML.
    /// </summary>
    /// <param name="markdown">The Markdown source string.</param>
    /// <returns>The rendered HTML string.</returns>
    public override string Render(string markdown)
    {
        if (markdown == null) return string.Empty;

        // Pre-process the input to normalize newlines for consistent parsing.
        var lines = NormalizeNewlines(markdown).Split('\n');
        var html = new StringBuilder();

        // State flags to track the current parsing context (e.g., inside a paragraph, list, etc.).
        bool inParagraph = false;
        bool inBlockQuote = false;
        bool inFence = false;
        string fenceLang = "";
        var fenceBuffer = new StringBuilder();
        bool inIndentedCode = false;
        var indentedCodeBuffer = new StringBuilder();
        bool inUl = false;
        bool inOl = false;

        // Helper methods to close open HTML blocks, ensuring proper tag nesting.
        void CloseParagraphIfOpen()
        {
            if (!inParagraph) return;
            html.AppendLine("</p>");
            inParagraph = false;
        }

        void CloseListIfOpen()
        {
            if (inUl) { html.AppendLine("</ul>"); inUl = false; }
            if (inOl) { html.AppendLine("</ol>"); inOl = false; }
        }

        void CloseBlockQuoteIfOpen()
        {
            if (!inBlockQuote) return;
            html.AppendLine("</blockquote>");
            inBlockQuote = false;
        }

        void CloseCodeBlocksIfOpen()
        {
            if (inFence)
            {
                html.Append("<pre><code");
                if (!string.IsNullOrWhiteSpace(fenceLang))
                    html.Append(" class=\"language-").Append(HtmlEscape(fenceLang)).Append('"');
                html.Append('>')
                    .Append(HtmlEscapePreserveNewlines(fenceBuffer.ToString()))
                    .AppendLine("</code></pre>");
                inFence = false;
                fenceLang = "";
                fenceBuffer.Clear();
            }

            if (inIndentedCode)
            {
                html.Append("<pre><code>")
                    .Append(HtmlEscapePreserveNewlines(TrimTrailingNewline(indentedCodeBuffer.ToString())))
                    .AppendLine("</code></pre>");
                inIndentedCode = false;
                indentedCodeBuffer.Clear();
            }
        }

        void CloseFlowingBlocks()
        {
            CloseParagraphIfOpen();
            CloseListIfOpen();
            CloseBlockQuoteIfOpen();
        }

        // Main parsing loop, processing the document line by line.
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // 1. Fenced code block: If we are already inside, append to the buffer or close it.
            if (inFence)
            {
                if (ReFenceEnd.IsMatch(line))
                {
                    CloseCodeBlocksIfOpen();
                }
                else
                {
                    fenceBuffer.AppendLine(line);
                }
                continue;
            }

            // 2. Indented code block: Handle continuation or end of the block.
            if (inIndentedCode)
            {
                if (IsIndentedCodeLine(line))
                {
                    indentedCodeBuffer.AppendLine(StripIndent(line));
                    continue;
                }
                else
                {
                    CloseCodeBlocksIfOpen();
                }
            }

            // 3. Blank line: Ends a paragraph or a code block.
            if (string.IsNullOrWhiteSpace(line))
            {
                CloseParagraphIfOpen();
                CloseCodeBlocksIfOpen();
                continue;
            }

            // 4. Setext headings: Check for an underline on the next line.
            // This must be checked before other block-level elements for correct parsing.
            if (!inParagraph && !inUl && !inOl && !inBlockQuote)
            {
                if (i + 1 < lines.Length)
                {
                    var next = lines[i + 1];
                    if (IsSetextUnderline(next, out int setextLevel))
                    {
                        CloseFlowingBlocks();
                        CloseCodeBlocksIfOpen();
                        var content = Inline(line.Trim());
                        int lvl = setextLevel;
                        html.Append("<h").Append(lvl).Append(">")
                            .Append(content)
                            .Append("</h").Append(lvl).AppendLine(">");
                        i++; // Consume the underline line.
                        continue;
                    }
                }
            }

            // 5. Block quote: Handle nesting by checking for leading `>` markers.
            int quoteDepth = CountLeadingBlockQuoteMarkers(line, out string afterQuote);
            if (quoteDepth > 0)
            {
                if (!inBlockQuote)
                {
                    CloseParagraphIfOpen();
                    CloseListIfOpen();
                    html.AppendLine("<blockquote>");
                    inBlockQuote = true;
                }
                line = afterQuote;
            }
            else if (inBlockQuote)
            {
                // If the block quote has ended, close the tag.
                CloseParagraphIfOpen();
                CloseBlockQuoteIfOpen();
            }

            // 6. Horizontal rule: A standalone rule on a line.
            if (ReHr.IsMatch(line))
            {
                CloseFlowingBlocks();
                CloseCodeBlocksIfOpen();
                html.AppendLine("<hr />");
                continue;
            }

            // 7. Fenced code block start: Open the block and set state.
            var fenceStartMatch = ReFenceStart.Match(line);
            if (fenceStartMatch.Success)
            {
                CloseParagraphIfOpen();
                CloseListIfOpen();
                inFence = true;
                fenceLang = fenceStartMatch.Groups[2].Value ?? "";
                fenceBuffer.Clear();
                continue;
            }

            // 8. Indented code block start: Open the block and set state.
            if (IsIndentedCodeLine(line))
            {
                CloseParagraphIfOpen();
                CloseListIfOpen();
                inIndentedCode = true;
                indentedCodeBuffer.Clear();
                indentedCodeBuffer.AppendLine(StripIndent(line));
                continue;
            }

            // 9. ATX Headings: The `#` style.
            int headingLevel = CountHeadingLevel(line, out string headingText);
            if (headingLevel > 0)
            {
                CloseFlowingBlocks();
                CloseCodeBlocksIfOpen();
                html.Append("<h").Append(headingLevel).Append(">")
                    .Append(Inline(headingText))
                    .Append("</h").Append(headingLevel).AppendLine(">");
                continue;
            }

            // 10. Lists: Process list items and manage `<ul>` or `<ol>` tags.
            var ulMatch = ReUlMarker.Match(line);
            var olMatch = ReOlMarker.Match(line);

            if (ulMatch.Success)
            {
                if (!inUl)
                {
                    CloseParagraphIfOpen();
                    html.AppendLine("<ul>");
                    inUl = true;
                }
                if (inOl)
                {
                    html.AppendLine("</ol>");
                    inOl = false;
                }
                var itemText = ulMatch.Groups[2].Value;
                html.Append("<li>").Append(Inline(itemText)).AppendLine("</li>");
                continue;
            }
            else if (olMatch.Success)
            {
                if (!inOl)
                {
                    CloseParagraphIfOpen();
                    html.AppendLine("<ol>");
                    inOl = true;
                }
                if (inUl)
                {
                    html.AppendLine("</ul>");
                    inUl = false;
                }
                var itemText = olMatch.Groups[2].Value;
                html.Append("<li>").Append(Inline(itemText)).AppendLine("</li>");
                continue;
            }
            else if (inUl || inOl)
            {
                // Ends a list if the current line is not a list item.
                CloseListIfOpen();
            }

            // 11. Paragraphs: If no other block-level element is matched, this is a paragraph.
            if (!inParagraph)
            {
                CloseCodeBlocksIfOpen();
                html.Append("<p>");
                inParagraph = true;
            }
            else
            {
                // A single newline within a paragraph becomes a `<br />`.
                html.Append("<br />");
            }

            html.Append(Inline(line));
        }

        // Final cleanup after the loop to close any open HTML tags.
        CloseParagraphIfOpen();
        CloseCodeBlocksIfOpen();
        CloseListIfOpen();
        CloseBlockQuoteIfOpen();

        return html.ToString();
    }

    // Inline processing
    /// <summary>
    /// Processes and renders inline Markdown elements within a given string.
    /// This method uses a token-free, iterative approach to handle Markdown precedence.
    /// It first tries to match high-precedence elements like code spans, images, and links.
    /// Any remaining text is then processed for lower-precedence elements like emphasis and strong emphasis.
    /// </summary>
    /// <param name="text">The input string to be processed inline.</param>
    /// <returns>The rendered HTML fragment.</returns>
    protected virtual string Inline(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length + 16);
        int i = 0;

        // Helper to check for a match at a specific starting position.
        bool TryMatch(Regex re, string s, int start, out Match m, bool anchored = true)
        {
            m = re.Match(s, start);
            return m.Success && (!anchored || m.Index == start);
        }

        while (i < text.Length)
        {
            // Try to match highest-precedence elements first.
            if (TryMatch(ReCodeSpan, text, i, out var mCode))
            {
                var raw = mCode.Groups[2].Value;
                if (raw.Length >= 2 && raw.StartsWith(" ") && raw.EndsWith(" "))
                    raw = raw.Substring(1, raw.Length - 2);
                sb.Append("<code>").Append(HtmlEscape(raw)).Append("</code>");
                i += mCode.Length;
                continue;
            }

            if (TryMatch(ReImage, text, i, out var mImg))
            {
                var alt = HtmlEscape(mImg.Groups[1].Value);
                var src = HtmlEscapeUrl(mImg.Groups[2].Value);
                var title = mImg.Groups[3].Success ? HtmlEscape(mImg.Groups[3].Value) : null;

                sb.Append("<img src=\"").Append(src).Append("\" alt=\"").Append(alt).Append('"');
                if (!string.IsNullOrEmpty(title)) sb.Append(" title=\"").Append(title).Append('"');
                sb.Append(" />");

                i += mImg.Length;
                continue;
            }

            if (TryMatch(ReLink, text, i, out var mLink))
            {
                var label = mLink.Groups[1].Value;
                var labelHtml = InlineLabel(label); // Process the link label separately.

                var url = HtmlEscapeUrl(mLink.Groups[2].Value);
                var title = mLink.Groups[3].Success ? HtmlEscape(mLink.Groups[3].Value) : null;

                sb.Append("<a href=\"").Append(url).Append("\"");
                if (!string.IsNullOrEmpty(title)) sb.Append(" title=\"").Append(title).Append('"');
                sb.Append(">").Append(labelHtml).Append("</a>");

                i += mLink.Length;
                continue;
            }

            // If no special inline syntax is found, process a segment of plain text.
            int start = i;
            while (i < text.Length
                   && !ReCodeSpan.IsMatch(text, i)
                   && !ReImage.IsMatch(text, i)
                   && !ReLink.IsMatch(text, i))
            {
                i++;
            }

            if (i == start)
                i++;

            string segment = text.Substring(start, i - start);
            // Escape and process emphasis/strong emphasis on the plain text segment.
            string escaped = HtmlEscape(segment);
            escaped = ReStrong.Replace(escaped, m => "<strong>" + m.Groups[2].Value + "</strong>");
            escaped = ReEm.Replace(escaped, m => "<em>" + m.Groups[2].Value + "</em>");
            sb.Append(escaped);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Processes a string specifically as a link label.
    /// This method allows for emphasis and code spans but prevents images and nested links,
    /// adhering to CommonMark's rules for link labels.
    /// </summary>
    /// <param name="text">The link label text.</param>
    /// <returns>The rendered HTML fragment.</returns>
    protected virtual string InlineLabel(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length + 16);
        int i = 0;

        while (i < text.Length)
        {
            // First, check for code spans as they have the highest precedence.
            var mCode = ReCodeSpan.Match(text, i);
            if (!mCode.Success)
            {
                // If no code span is found, process the rest of the string for emphasis.
                string escaped = HtmlEscape(text[i..]);
                escaped = ReStrong.Replace(escaped, m => "<strong>" + m.Groups[2].Value + "</strong>");
                escaped = ReEm.Replace(escaped, m => "<em>" + m.Groups[2].Value + "</em>");
                sb.Append(escaped);
                break;
            }

            if (mCode.Index > i)
            {
                // Process the text before the code span for emphasis.
                string mid = HtmlEscape(text[i..mCode.Index]);
                mid = ReStrong.Replace(mid, m => "<strong>" + m.Groups[2].Value + "</strong>");
                mid = ReEm.Replace(mid, m => "<em>" + m.Groups[2].Value + "</em>");
                sb.Append(mid);
            }

            // Append the processed code span.
            var raw = mCode.Groups[2].Value;
            if (raw.Length >= 2 && raw.StartsWith(" ") && raw.EndsWith(" "))
                raw = raw.Substring(1, raw.Length - 2);
            sb.Append("<code>").Append(HtmlEscape(raw)).Append("</code>");

            i = mCode.Index + mCode.Length;
        }

        return sb.ToString();
    }

    /// <summary>
    /// This method provides an alternative approach for processing inline elements
    /// without links or images, by tokenizing code spans first.
    /// </summary>
    /// <remarks>
    /// It's a different strategy than the `Inline` method. It first converts all code spans to `<code>...</code>` tags,
    /// then replaces these tags with unique tokens to "protect" them from further processing.
    /// It then escapes the remaining text and applies `<strong>` and `<em>` formatting, before restoring the original code tags.
    /// This pattern is useful when a more robust precedence rule is required.
    /// </remarks>
    protected virtual string InlineWithoutLinksOrImages(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Convert code spans to <code>...</code> first
        string withCode = ReCodeSpan.Replace(text, m =>
        {
            var raw = m.Groups[2].Value;
            if (raw.Length >= 2 && raw.StartsWith(" ") && raw.EndsWith(" "))
                raw = raw.Substring(1, raw.Length - 2);
            return "<code>" + HtmlEscape(raw) + "</code>";
        });

        // Protect code spans and escape the rest (same approach as Inline)
        const string codeOpen = "<code>";
        const string codeClose = "</code>";
        var tokens = new List<string>();
        string Tokenize(string s)
        {
            tokens.Add(s);
            return $"@@CODESPAN_TOK_INLBL_{tokens.Count - 1}@@";
        }

        var sb = new StringBuilder(withCode.Length);
        int pos = 0;
        while (pos < withCode.Length)
        {
            int open = withCode.IndexOf(codeOpen, pos, StringComparison.Ordinal);
            if (open < 0)
            {
                sb.Append(HtmlEscape(withCode[pos..]));
                break;
            }

            sb.Append(HtmlEscape(withCode[pos..open]));

            int innerStart = open + codeOpen.Length;
            int close = withCode.IndexOf(codeClose, innerStart, StringComparison.Ordinal);
            if (close < 0)
            {
                sb.Append(HtmlEscape(withCode[open..]));
                pos = withCode.Length;
                break;
            }

            string codeSpan = withCode.Substring(open, close + codeClose.Length - open);
            sb.Append(Tokenize(codeSpan));
            pos = close + codeClose.Length;
        }

        string escapedNonCode = sb.ToString();

        // Emphasis/strong only
        string afterStrong = ReStrong.Replace(escapedNonCode, m => "<strong>" + m.Groups[2].Value + "</strong>");
        string afterEm = ReEm.Replace(afterStrong, m => "<em>" + m.Groups[2].Value + "</em>");

        // Restore tokens
        string restored = afterEm;
        for (int i = 0; i < tokens.Count; i++)
        {
            restored = restored.Replace($"@@CODESPAN_TOK_INLBL_{i}@@", tokens[i]);
        }

        return restored;
    }

    /// <summary>
    /// Normalizes newline characters (`\r\n`, `\r`) to a single `\n`.
    /// </summary>
    protected virtual string NormalizeNewlines(string input)
        => input.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>
    /// Checks if a line is a valid indented code block line (starts with 4 spaces or a tab).
    /// </summary>
    protected virtual bool IsIndentedCodeLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        int spaces = 0;
        foreach (var ch in line)
        {
            if (ch == ' ') spaces++;
            else if (ch == '\t') return true;
            else break;
        }
        return spaces >= 4;
    }

    /// <summary>
    /// Removes the leading 4 spaces or 1 tab from an indented code block line.
    /// </summary>
    protected virtual string StripIndent(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        int removed = 0;
        var sb = new StringBuilder();
        foreach (var ch in line)
        {
            if (removed < 4 && ch == ' ') { removed++; continue; }
            if (removed < 4 && ch == '\t') { removed = 4; continue; }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Counts the leading `#` characters to determine the heading level.
    /// Also removes any trailing `#` characters from the heading text.
    /// </summary>
    /// <param name="line">The input line.</param>
    /// <param name="text">The extracted heading text.</param>
    /// <returns>The heading level (1-6) or 0 if not a valid ATX heading.</returns>
    protected virtual int CountHeadingLevel(string line, out string text)
    {
        int level = 0;
        int i = 0;
        while (i < line.Length && line[i] == '#')
        {
            level++;
            i++;
            if (level > 6) break;
        }
        if (level == 0) { text = line; return 0; }
        if (i < line.Length && line[i] == ' ')
        {
            text = line[(i + 0)..].Trim();
            text = Regex.Replace(text, @"\s#+\s*$", "").TrimEnd();
            return Math.Min(level, 6);
        }
        text = line;
        return 0;
    }

    /// <summary>
    /// Counts the number of leading `>` markers for blockquotes.
    /// </summary>
    /// <param name="line">The input line.</param>
    /// <param name="after">The remaining part of the line after the blockquote markers.</param>
    /// <returns>The nesting depth of the blockquote.</returns>
    protected virtual int CountLeadingBlockQuoteMarkers(string line, out string after)
    {
        int i = 0;
        int count = 0;
        while (i < line.Length)
        {
            int j = i;
            while (j < line.Length && line[j] == ' ') j++;
            if (j < line.Length && line[j] == '>')
            {
                count++;
                i = j + 1;
                if (i < line.Length && line[i] == ' ') i++;
            }
            else break;
        }
        after = i <= line.Length ? line[i..] : string.Empty;
        return count;
    }

    /// <summary>
    /// Escapes HTML special characters to prevent rendering issues and XSS attacks.
    /// </summary>
    protected virtual string HtmlEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("&", "&amp;").Replace("<", "&lt;")
                .Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    /// <summary>
    /// Escapes HTML special characters but preserves newlines, typically used for code blocks.
    /// </summary>
    protected virtual string HtmlEscapePreserveNewlines(string s)
        => HtmlEscape(s).Replace("\r", "");

    /// <summary>
    /// Escapes a string to be safely used as a URL attribute value.
    /// </summary>
    protected virtual string HtmlEscapeUrl(string s)
        => string.IsNullOrEmpty(s) ? string.Empty : HtmlEscape(s);

    /// <summary>
    /// Removes a single trailing newline character from a string.
    /// </summary>
    protected virtual string TrimTrailingNewline(string s)
        => s.EndsWith("\n") ? s[..^1] : s;

    /// <summary>
    /// Checks if a line is a valid Setext heading underline (`===` or `---`).
    /// </summary>
    /// <param name="line">The input line.</param>
    /// <param name="level">The heading level (1 for `=`, 2 for `-`).</param>
    /// <returns>True if the line is a Setext underline, otherwise false.</returns>
    protected virtual bool IsSetextUnderline(string line, out int level)
    {
        // check if line is all = or -
        if (string.IsNullOrEmpty(line)) { level = 0; return false; }
        char c = line[0];
        if (c != '=' && c != '-') { level = 0; return false; }
        for (int i = 1; i < line.Length; i++)
            if (line[i] != c) { level = 0; return false; }
        level = (c == '=') ? 1 : 2;
        return true;
    }
}