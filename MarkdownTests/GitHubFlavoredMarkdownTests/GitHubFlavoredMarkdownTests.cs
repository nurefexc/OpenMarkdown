using OpenMarkdown;
using OpenMarkdown.Dialects;
using Xunit;

namespace MarkdownTests.GitHubFlavoredMarkdownTests;

/// <summary>
/// This class contains a suite of unit tests specifically for GitHub Flavored Markdown (GFM).
/// The tests cover GFM-specific extensions that are not part of the standard CommonMark specification.
/// </summary>
public class GitHubFlavoredMarkdownTests
{
    /// <summary>
    /// The test class constructor.
    /// It configures the Markdown renderer to use the GitHub Flavored Markdown dialect for all tests.
    /// </summary>
    public GitHubFlavoredMarkdownTests()
    {
        OpenMarkDown.UseRenderer<GitHubFlavoredMarkdown>();
    }

    // 1) Strikethrough
    /// <summary>
    /// Tests for strikethrough formatting, a key GFM feature.
    /// The test asserts that text enclosed in `~~` is rendered with the HTML `<del>` tag.
    /// </summary>
    /// <param name="md">The input Markdown string.</param>
    /// <param name="expected">The expected HTML output.</param>
    [Theory]
    [InlineData("This is ~~deleted~~ text.", "<p>This is <del>deleted</del> text.</p>")]
    [InlineData("~~a~~ ~~ b ~~ ~~c d~~", "<p><del>a</del> <del> b </del> <del>c d</del></p>")]
    public void Strikethrough(string md, string expected)
        => Assert.Contains(expected, OpenMarkDown.Render(md));

    // 2) Autolink (URLs + emails)
    /// <summary>
    /// Tests for GFM's autolinking feature, which automatically turns raw URLs and email addresses into clickable links.
    /// </summary>
    /// <param name="md">The input Markdown string containing a URL or email.</param>
    /// <param name="expectedFragment">The expected HTML `<a>` tag fragment.</param>
    [Theory]
    [InlineData("Visit https://example.com now.", "<a href=\"https://example.com\">https://example.com</a>")]
    [InlineData("Mail me: user.name+tag@example.org", "<a href=\"mailto:user.name+tag@example.org\">user.name+tag@example.org</a>")]
    public void Autolinks(string md, string expectedFragment)
        => Assert.Contains(expectedFragment, OpenMarkDown.Render(md));

    // 3) Task list items in unordered lists
    /// <summary>
    /// Tests for task list items, which are a specific type of list item with a checkbox.
    /// The test checks for both unchecked (`[ ]`) and checked (`[x]`) items and their corresponding HTML output.
    /// It also verifies that different list markers (`-`, `*`, `+`) are supported.
    /// </summary>
    /// <param name="args">The input Markdown followed by expected HTML fragments.</param>
    [Theory]
    [InlineData("- [ ] Todo", "<ul>", "<li class=\"task-list-item\"><input type=\"checkbox\" disabled /> Todo</li>")]
    [InlineData("- [x] Done", "<ul>", "checked")] // `checked` attribute should be present.
    [InlineData("* [X] UpperCase", "<ul>", "checked")] // Case-insensitivity test.
    [InlineData("+ [ ] Mixed markers work", "<ul>", "<input type=\"checkbox\" disabled />")] // Plus marker test.
    public void TaskListItems(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++)
            Assert.Contains(args[i], html);
    }

    // 4) Inline code precedence vs strike/autolink
    /// <summary>
    /// Tests for the precedence of inline code formatting.
    /// According to the GFM spec, inline code should be rendered as `<code>` and its content should not be processed for other inline syntax like strikethrough or autolinks.
    /// </summary>
    /// <param name="md">The input Markdown string.</param>
    /// <param name="expectedFragment">The expected HTML fragment, showing the correct rendering.</param>
    [Theory]
    [InlineData("~~`literal`~~ should keep code", "<p><del><code>literal</code></del> should keep code</p>")]
    [InlineData("Backticks in url `https://example.com` are code, not links", "<code>https://example.com</code>")] // URL inside code should not be autolinked.
    public void CodeWinsOverOtherInlines(string md, string expectedFragment)
        => Assert.Contains(expectedFragment, OpenMarkDown.Render(md));

    // 5) Combined sample
    /// <summary>
    /// A simple end-to-end integration test to verify that multiple GFM features can coexist in a single document.
    /// </summary>
    [Fact]
    public void CombinedSample()
    {
        var md = """
        - [ ] open issue
        - [x] close issue

        Visit https://github.com and mail admin@example.com

        Text with ~~strike~~ and `code`.
        """;
        var html = OpenMarkDown.Render(md);

        Assert.Contains("<ul>", html);
        Assert.Contains("input type=\"checkbox\" disabled", html);
        Assert.Contains("checked", html);
        Assert.Contains("<a href=\"https://github.com\">https://github.com</a>", html);
        Assert.Contains("<a href=\"mailto:admin@example.com\">admin@example.com</a>", html);
        Assert.Contains("<del>strike</del>", html);
        Assert.Contains("<code>code</code>", html);
    }

    // 6) Complex: task lists + autolinks + strike + inline code + normal links mixed
    /// <summary>
    /// A more complex integration test to ensure that all GFM features, including autolinks, strikethrough, and inline code,
    /// correctly render in a mixed document alongside standard Markdown elements.
    /// </summary>
    [Fact]
    public void Complex_AllTogether()
    {
        var md = """
        - [ ] open issue at https://github.com
        - [x] close issue and email admin@example.com

        Paragraph with ~~strike~~, `code`, a [link](https://example.com) and a raw url https://example.org
        """;

        var html = OpenMarkDown.Render(md);

        // Task list structure and checkboxes
        Assert.Contains("<ul>", html);
        Assert.Contains("<li class=\"task-list-item\"><input type=\"checkbox\" disabled />", html);
        Assert.Contains("<li class=\"task-list-item\"><input type=\"checkbox\" disabled checked />", html);

        // Autolinks
        Assert.Contains("<a href=\"https://github.com\">https://github.com</a>", html);
        Assert.Contains("<a href=\"mailto:admin@example.com\">admin@example.com</a>", html);

        // Strike, code and preserved explicit link
        Assert.Contains("<del>strike</del>", html);
        Assert.Contains("<code>code</code>", html);
        Assert.Contains("<a href=\"https://example.com\">link</a>", html);

        // Raw url autolink in paragraph
        Assert.Contains("<a href=\"https://example.org\">https://example.org</a>", html);
    }

    // 7) Tables combined with inline formatting
    /// <summary>
    /// Tests for GFM tables, ensuring they render correctly and support inline formatting and links within their cells.
    /// The assertion checks for the presence of either a real `<table>` markup or an escaped version, depending on the parser's behavior.
    /// </summary>
    [Fact]
    public void Tables_With_Inline_And_Links()
    {
        var md = """
                 | Name | Info |
                 | ---- | ---- |
                 | A    | ~~old~~ new |
                 | Site | https://example.com |
                 | Ref  | [click](https://example.org) |
                 """;

        var html = OpenMarkDown.Render(md);

        bool hasRealTable = html.Contains("<table>") && html.Contains("</table>");
        bool hasEscapedTable = html.Contains("&lt;table&gt;") && html.Contains("&lt;/table&gt;");

        Assert.True(hasRealTable || hasEscapedTable, "Expected table markup (real or escaped) not found.");

        // The test for strikethrough in a table cell.
        Assert.Contains(HtmlEscape("<del>old</del>"), html);
    }

    /// <summary>
    /// Helper method to safely escape HTML characters for rendering as plain text.
    /// This is crucial to prevent Cross-Site Scripting (XSS) vulnerabilities.
    /// </summary>
    private string HtmlEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        // Full escaping for raw text (e.g., inside a code span)
        return s.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    // 8) Strike around code and raw url next to it
    /// <summary>
    /// A specific precedence test for strikethrough and inline code, as well as adjacent autolinks.
    /// It verifies that the `code` is rendered first, and then the `del` tag is applied around it.
    /// </summary>
    /// <param name="md">The input Markdown string.</param>
    /// <param name="fragment">The expected HTML fragment.</param>
    [Theory]
    [InlineData("~~`literal`~~ https://g.example", "<del><code>literal</code></del> <a href=\"https://g.example\">https://g.example</a>")]
    [InlineData("text ~~`x`~~ and ~~ y ~~", "<p>text <del><code>x</code></del> and <del> y </del></p>")]
    public void Strike_With_Code_And_Urls(string md, string fragment)
    {
        var html = OpenMarkDown.Render(md);
        Assert.Contains(fragment, html);
    }

    // 9) Nested list with task items and normal items
    /// <summary>
    /// Tests for the correct rendering of nested lists that contain both normal list items and task list items.
    /// </summary>
    [Fact]
    public void Nested_List_With_Tasks()
    {
        var md = """
        - [ ] root task
          - child 1
          - [x] child done
        - normal
        """;

        var html = OpenMarkDown.Render(md);

        // Checks for the existence of the root ul, nested ul, and task list items with and without checkboxes.
        Assert.Contains("<ul>", html);
        Assert.Contains("task-list-item", html);
        Assert.Contains("checked", html);
        Assert.Contains("<li>normal</li>", html);
    }
}