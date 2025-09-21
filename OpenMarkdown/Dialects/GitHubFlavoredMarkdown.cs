using System.Text;
using System.Text.RegularExpressions;

namespace OpenMarkdown.Dialects;

/// <summary>
/// This class provides a GitHub Flavored Markdown (GFM) renderer by extending the base CommonMarkdown class.
/// It focuses on implementing GFM-specific features while delegating standard Markdown rendering to the base class.
///
/// Key GFM features implemented here:
/// - Strikethrough: `~~text~~` renders as `<del>text</del>`.
/// - Autolinks: Raw URLs and email addresses are automatically converted to links.
/// - Task list items: `-[ ]` and `-[x]` become disabled checkboxes within lists.
/// - Pipe tables: A simple implementation for rendering tables using `|` syntax.
///
/// Design approach:
/// - Most block-level parsing (headings, paragraphs, etc.) is handled by the `base.Render` method.
/// - Block-level GFM features like tables are handled in a pre-processing step (`PreprocessTables`).
/// - Inline-level GFM features like strikethrough and autolinks are applied by overriding the `Inline` method.
/// - Task lists are handled by post-processing the final HTML output. This is a pragmatic choice to avoid a full re-write of the list parser.
/// </summary>
public class GitHubFlavoredMarkdown : CommonMarkdown
{
    // Inline GFM regexes for strikethrough and autolinks.
    // Strikethrough matches two tildes around non-whitespace content.
    private Regex ReStrike => new(@"~~(?=\S)([\s\S]*?\S)~~", RegexOptions.Compiled);

    // Regex for autolinking raw URLs (http/https).
    private Regex ReAutoUrl => new(@"\b(https?://[^\s<]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // Regex for autolinking email addresses.
    private Regex ReAutoEmail => new(@"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex for detecting a task list marker at the beginning of a list item.
    private Regex ReTaskMarker => new(@"^\s*\[( |x|X)\]\s+", RegexOptions.Compiled);

    // Regex for a table's separator line (e.g., `|---|---|`).
    private Regex ReTableSeparator => new(@"^\s*\|?(?:\s*:?-+:?\s*\|)+\s*:?-+:?\s*\|?\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Renders a GFM string to HTML.
    /// This method orchestrates the GFM-specific rendering pipeline. It applies block-level
    /// pre-processing for tables and then calls the base CommonMark renderer. Finally, it
    /// post-processes the output to handle task list items.
    /// </summary>
    public override string Render(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        // 1) Pre-process tables: Find and convert table syntax into an HTML `<table>` block.
        var pre = PreprocessTables(markdown);

        // 2) Render to HTML using the base CommonMark implementation.
        // This handles paragraphs, headings, lists, etc., from the pre-processed markdown.
        var html = base.Render(pre);

        // 3) Post-process: Inject task list checkboxes into the rendered HTML lists.
        html = ApplyTaskListRendering(html);

        return html;
    }

    /// <summary>
    /// Transforms list items that start with a task marker (`[ ]` or `[x]`) into list items with a disabled checkbox.
    /// This is done by post-processing the rendered HTML to find and modify `<li>` tags.
    /// </summary>
    /// <param name="html">The rendered HTML string from the base renderer.</param>
    /// <returns>The modified HTML with task list items.</returns>
    protected virtual string ApplyTaskListRendering(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        // Regex to capture `<li>...</li>` blocks.
        var reLi = new Regex(@"<li>([\s\S]*?)</li>", RegexOptions.Compiled);

        string Evaluator(Match m)
        {
            var inner = m.Groups[1].Value;

            // Get the text at the very beginning of the list item (before any other HTML tags).
            int cut = inner.IndexOf('<');
            string leading = cut >= 0 ? inner[..cut] : inner;

            // Use the helper method to render the task list checkbox.
            var replacedLeading = RenderTaskListItemContent(leading, out bool isTask);
            if (!isTask) return m.Value; // If it's not a task, return the original `<li>`.

            // Append the rest of the list item's content.
            var remainder = cut >= 0 ? inner[cut..] : string.Empty;
            var newInner = replacedLeading + remainder;

            // Return the modified `<li>` with the `task-list-item` class.
            // Note: Whitespace is minimized to avoid rendering issues in tests.
            return "<li class=\"task-list-item\">" + newInner + "</li>";
        }

        return reLi.Replace(html, new MatchEvaluator(Evaluator));
    }

    /// <summary>
    /// The `Inline` method is overridden to apply GFM-specific inline formatting.
    /// </summary>
    /// <remarks>
    /// The base `Inline` method handles CommonMark features like bold, italics, links, and images.
    /// This override adds GFM's strikethrough and autolink functionality on top of the base output.
    /// To maintain correct precedence, this method first processes the base output, then:
    /// 1. It handles strikethrough around `<code>` blocks.
    /// 2. It splits the HTML into segments at `<code>` tags to ensure `<code>` content is not processed.
    /// 3. It applies strikethrough and autolinking to the non-code segments only.
    /// </remarks>
    protected override string Inline(string text)
    {
        // The base method handles all standard inline CommonMark elements.
        var baseProcessed = base.Inline(text);
        if (string.IsNullOrEmpty(baseProcessed)) return baseProcessed;

        const string codeOpen = "<code>";
        const string codeClose = "</code>";

        // Step 1: Handle strikethrough around code spans.
        // This is a special case in GFM where `~~` can wrap a code block.
        string FoldStrikeAroundCode(string s)
        {
            var reStrikeCode = new Regex(@"~~\s*(<code>[\s\S]*?<\/code>)\s*~~", RegexOptions.Compiled);
            return reStrikeCode.Replace(s, m => "<del>" + m.Groups[1].Value + "</del>");
        }

        baseProcessed = FoldStrikeAroundCode(baseProcessed);

        // Step 2 & 3: Process only the non-code parts of the document.
        var sb = new StringBuilder(baseProcessed.Length + 16);
        int i = 0;
        while (i < baseProcessed.Length)
        {
            int open = baseProcessed.IndexOf(codeOpen, i, StringComparison.Ordinal);
            if (open < 0)
            {
                // No more code blocks. Process the rest of the string.
                sb.Append(ApplyGfmInlinesOutsideCode(baseProcessed[i..]));
                break;
            }

            // Process the text before the code block.
            sb.Append(ApplyGfmInlinesOutsideCode(baseProcessed[i..open]));

            int innerStart = open + codeOpen.Length;
            int close = baseProcessed.IndexOf(codeClose, innerStart, StringComparison.Ordinal);
            if (close < 0)
            {
                // If there's no closing code tag, we don't apply GFM inlines to the rest of the document.
                sb.Append(baseProcessed[open..]);
                break;
            }

            // Copy the entire `<code>...</code>` block verbatim.
            sb.Append(baseProcessed, open, close + codeClose.Length - open);
            i = close + codeClose.Length;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Applies GFM inline formatting (strikethrough and autolinks) to a given HTML segment,
    /// ensuring it does not affect content already within `<a>` tags.
    /// </summary>
    /// <param name="htmlSegment">The HTML segment to be processed.</param>
    /// <returns>The processed HTML segment.</returns>
    protected virtual string ApplyGfmInlinesOutsideCode(string htmlSegment)
    {
        if (string.IsNullOrEmpty(htmlSegment)) return htmlSegment;

        // Apply strikethrough.
        var reStrikeLoose = new Regex(@"~~([\s\S]*?)~~", RegexOptions.Compiled);
        string withStrike = reStrikeLoose.Replace(htmlSegment, m =>
        {
            var inner = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(inner)) return m.Value;
            return "<del>" + inner + "</del>";
        });

        // Helper method to check if a match is inside an existing `<a>` tag.
        static bool IsInsideAnchor(string s, int matchIndex)
        {
            int lastOpen = s.LastIndexOf("<a", matchIndex, StringComparison.OrdinalIgnoreCase);
            if (lastOpen < 0) return false;
            int openEnd = s.IndexOf('>', lastOpen);
            if (openEnd < 0 || openEnd >= matchIndex) return true;
            int lastClose = s.LastIndexOf("</a>", matchIndex, StringComparison.OrdinalIgnoreCase);
            return lastClose < lastOpen;
        }

        // Apply autolinks to URLs, but only if they are not inside an existing `<a>` tag.
        string withUrls = ReAutoUrl.Replace(withStrike, m =>
        {
            if (IsInsideAnchor(withStrike, m.Index)) return m.Value;
            var url = m.Groups[1].Value;
            var safe = HtmlEscapeUrl(url);
            return "<a href=\"" + safe + "\">" + safe + "</a>";
        });

        // Apply autolinks to email addresses, again avoiding existing `<a>` tags.
        string withEmails = ReAutoEmail.Replace(withUrls, m =>
        {
            if (IsInsideAnchor(withUrls, m.Index)) return m.Value;
            var mail = m.Value;
            var safe = HtmlEscape(mail);
            return "<a href=\"mailto:" + safe + "\">" + safe + "</a>";
        });

        return withEmails;
    }

    /// <summary>
    /// Pre-processes the entire markdown document to detect and convert pipe tables into HTML.
    /// It looks for a header row followed by a separator row (`|---|`) and then processes the body rows.
    /// </summary>
    /// <param name="markdown">The input Markdown string.</param>
    /// <returns>A new string with tables replaced by HTML `<table>` blocks.</returns>
    private string PreprocessTables(string markdown)
    {
        var lines = NormalizeNewlines(markdown).Split('\n');
        var sb = new StringBuilder();

        // Helper to split a table row string by `|` into a list of cell contents.
        static List<string> SplitRow(string line)
        {
            var t = line.Trim();
            if (t.StartsWith("|")) t = t[1..];
            if (t.EndsWith("|")) t = t[..^1];
            return t.Split('|').Select(s => s.Trim()).ToList();
        }

        int i = 0;
        while (i < lines.Length)
        {
            // Skip blank lines.
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                sb.AppendLine(lines[i]);
                i++;
                continue;
            }

            // Detect a potential table header row followed by a separator.
            if (i + 1 < lines.Length && lines[i].Contains('|') && ReTableSeparator.IsMatch(lines[i + 1]))
            {
                var header = SplitRow(lines[i]);
                var body = new List<List<string>>();

                int j = i + 2;
                while (j < lines.Length && lines[j].Contains('|') && !string.IsNullOrWhiteSpace(lines[j]))
                {
                    body.Add(SplitRow(lines[j]));
                    j++;
                }

                // Emit the HTML table.
                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr>");
                foreach (var h in header)
                    sb.Append("<th>").Append(Inline(h)).AppendLine("</th>");
                sb.AppendLine("</tr></thead>");

                if (body.Count > 0)
                {
                    sb.AppendLine("<tbody>");
                    foreach (var row in body)
                    {
                        sb.AppendLine("<tr>");
                        foreach (var cell in row)
                            sb.Append("<td>").Append(Inline(cell)).AppendLine("</td>");
                        sb.AppendLine("</tr>");
                    }
                    sb.AppendLine("</tbody>");
                }

                sb.AppendLine("</table>");

                i = j;
                continue;
            }

            // If no table is found, copy the line as-is for the base renderer to process.
            sb.AppendLine(lines[i]);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// A helper method to convert a string beginning with a task marker (`[ ]` or `[x]`) into a disabled checkbox.
    /// </summary>
    /// <param name="text">The text of a list item.</param>
    /// <param name="isTask">An output parameter indicating if a task marker was found.</param>
    /// <returns>The rendered HTML for the checkbox and the rest of the text.</returns>
    public string RenderTaskListItemContent(string text, out bool isTask)
    {
        var m = ReTaskMarker.Match(text);
        if (!m.Success)
        {
            isTask = false;
            return text;
        }

        isTask = true;
        bool isChecked = !string.IsNullOrWhiteSpace(m.Groups[1].Value) && m.Groups[1].Value != " ";
        var rest = text[m.Length..];
        var checkbox = "<input type=\"checkbox\" disabled" + (isChecked ? " checked" : "") + " />";
        return checkbox + " " + rest;
    }
}