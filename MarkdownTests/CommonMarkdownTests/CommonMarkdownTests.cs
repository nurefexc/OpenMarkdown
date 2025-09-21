using OpenMarkdown;
using OpenMarkdown.Dialects;
using Xunit; // Added for clarity on the testing framework used.

namespace MarkdownTests.CommonMarkdownTests;

/// <summary>
/// This class contains a comprehensive suite of unit tests for the CommonMark specification.
/// The tests cover a wide range of syntax elements, from basic formatting to complex nested blocks.
/// They ensure that the OpenMarkdown renderer correctly interprets and converts CommonMark into valid HTML.
/// </summary>
public class CommonMarkdownTests
{
    /// <summary>
    /// The test class constructor.
    /// This sets up the Markdown renderer to use the CommonMarkdown dialect for all tests within this class.
    /// This ensures a consistent testing environment aligned with the CommonMark spec.
    /// </summary>
    public CommonMarkdownTests()
    {
        OpenMarkDown.UseRenderer<CommonMarkdown>();
    }

    /// <summary>
    /// Tests the rendering of ATX-style headings from H1 to H6.
    /// It also includes a test case for trailing hash characters, which should be ignored.
    /// </summary>
    /// <param name="markdown">The input Markdown string for the heading.</param>
    /// <param name="expected">The expected HTML heading tag.</param>
    [Theory]
    [InlineData("# H1", "<h1>H1</h1>")]
    [InlineData("## H2", "<h2>H2</h2>")]
    [InlineData("### H3", "<h3>H3</h3>")]
    [InlineData("#### H4", "<h4>H4</h4>")]
    [InlineData("##### H5", "<h5>H5</h5>")]
    [InlineData("###### H6", "<h6>H6</h6>")]
    [InlineData("# Title ####", "<h1>Title</h1>")] // Trailing hashes should be ignored.
    public void Headings_Render(string markdown, string expected)
    {
        var html = OpenMarkDown.Render(markdown).Trim();
        Assert.Contains(expected, html);
    }

    /// <summary>
    /// Tests basic paragraph rendering and line breaks.
    /// A single newline character should result in a soft break (`<br />`), while two newlines create a new paragraph (`<p>`).
    /// </summary>
    /// <param name="markdown">The input Markdown string.</param>
    /// <param name="expectedFragments">The expected HTML fragments to be found in the output.</param>
    [Theory]
    [InlineData("Hello", "<p>Hello</p>")] // Simple paragraph.
    [InlineData("Hello\nworld", "<p>Hello<br />world</p>")] // Soft break within a paragraph.
    [InlineData("Hello\n\nworld", "<p>Hello</p>", "<p>world</p>")] // Hard break to a new paragraph.
    public void Paragraphs_And_SoftBreaks(string markdown, params string[] expectedFragments)
    {
        var html = OpenMarkDown.Render(markdown).Trim();
        foreach (var frag in expectedFragments) Assert.Contains(frag, html);
    }

    /// <summary>
    /// Tests the rendering of horizontal rules using `---`, `***`, and `___`.
    /// </summary>
    /// <param name="markdown">The input Markdown string for the horizontal rule.</param>
    /// <param name="expected">The expected HTML `<hr />` tag.</param>
    [Theory]
    [InlineData("---", "<hr />")]
    [InlineData("***", "<hr />")]
    [InlineData("___", "<hr />")]
    public void HorizontalRules_Render(string markdown, string expected)
    {
        var html = OpenMarkDown.Render(markdown).Trim();
        Assert.Contains(expected, html);
    }

    /// <summary>
    /// Tests the rendering of blockquotes.
    /// Includes cases with single lines, multi-line soft breaks, and new paragraphs within the quote.
    /// </summary>
    /// <param name="args">The input Markdown string, followed by the expected HTML fragments.</param>
    [Theory]
    [InlineData("> Quote", "<blockquote>", "</blockquote>", "<p>Quote</p>")] // Simple blockquote.
    [InlineData("> A\n> B", "<blockquote>", "<p>A<br />B</p>", "</blockquote>")] // Blockquote with a soft break.
    [InlineData("> A\n\nB", "<blockquote>", "<p>A</p>", "</blockquote>", "<p>B</p>")] // Blockquote with an empty line.
    public void BlockQuotes_Render(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }

    /// <summary>
    /// Tests the rendering of both unordered and ordered lists.
    /// Unordered lists can use `-`, `+`, or `*` as markers. Ordered lists use a number followed by a period.
    /// </summary>
    /// <param name="args">The input Markdown string, followed by the expected HTML fragments.</param>
    [Theory]
    [InlineData("- a\n- b", "<ul>", "<li>a</li>", "<li>b</li>", "</ul>")] // Unordered list with hyphens.
    [InlineData("+ a\n+ b", "<ul>", "<li>a</li>", "<li>b</li>", "</ul>")] // Unordered list with pluses.
    [InlineData("* a\n* b", "<ul>", "<li>a</li>", "<li>b</li>", "</ul>")] // Unordered list with asterisks.
    [InlineData("1. first\n2. second", "<ol>", "<li>first</li>", "<li>second</li>", "</ol>")] // Ordered list.
    public void Lists_Render(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }

    /// <summary>
    /// Tests for fenced code blocks, which are enclosed in backticks (```) or tildes (~~~).
    /// Includes a case for language-specific syntax highlighting.
    /// </summary>
    /// <param name="args">The input Markdown string, followed by the expected HTML fragments.</param>
    [Theory]
    [InlineData("```\ncode\n```", "<pre><code>", "code", "</code></pre>")] // Fenced block with backticks.
    [InlineData("~~~\ncode\n~~~", "<pre><code>", "code", "</code></pre>")] // Fenced block with tildes.
    [InlineData("```csharp\nConsole.WriteLine(1);\n```", "<pre><code class=\"language-csharp\">", "Console.WriteLine(1);", "</code></pre>")] // Fenced block with language specifier.
    public void FencedCodeBlocks_Render(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }

    /// <summary>
    /// Tests for indented code blocks, which are created by indenting each line with four spaces or one tab.
    /// </summary>
    /// <param name="args">The input Markdown string, followed by the expected HTML fragments.</param>
    [Theory]
    [InlineData("    line 1\n        line 2", "<pre><code>", "line 1", "    line 2", "</code></pre>")] // Indented with spaces.
    [InlineData("\tConsole.WriteLine(1);", "<pre><code>", "Console.WriteLine(1);", "</code></pre>")] // Indented with a tab.
    public void IndentedCodeBlocks_Render(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }

    /// <summary>
    /// Tests for various inline formatting: emphasis, strong emphasis, and code spans.
    /// It validates that the correct HTML tags (`<em>`, `<strong>`, `<code>`) are generated.
    /// </summary>
    /// <param name="markdown">The input Markdown string with inline formatting.</param>
    /// <param name="expected">The full expected HTML output.</param>
    [Theory]
    [InlineData("This is *em* text.", "<p>This is <em>em</em> text.</p>")]
    [InlineData("This is _em_ text.", "<p>This is <em>em</em> text.</p>")]
    [InlineData("This is **strong** text.", "<p>This is <strong>strong</strong> text.</p>")]
    [InlineData("This is __strong__ text.", "<p>This is <strong>strong</strong> text.</p>")]
    [InlineData("Text with `span`.", "<p>Text with <code>span</code>.</p>")]
    public void Inline_Emphasis_Strong_CodeSpan(string markdown, string expected)
    {
        var html = OpenMarkDown.Render(markdown).Trim();
        Assert.Contains(expected, html);
    }

    /// <summary>
    /// Tests for inline links and images, including the use of titles.
    /// </summary>
    /// <param name="markdown">The input Markdown string for the link or image.</param>
    /// <param name="expected">The expected HTML output (`<a>` or `<img>` tag).</param>
    [Theory]
    [InlineData("[link](https://example.com)", "<p><a href=\"https://example.com\">link</a></p>")]
    [InlineData("[x](https://example.com \"Title\")", "<p><a href=\"https://example.com\" title=\"Title\">x</a></p>")]
    [InlineData("![alt](https://img/test.png)", "<p><img src=\"https://img/test.png\" alt=\"alt\" /></p>")]
    [InlineData("![a](u \"t\")", "<p><img src=\"u\" alt=\"a\" title=\"t\" /></p>")]
    public void Links_And_Images(string markdown, string expected)
    {
        var html = OpenMarkDown.Render(markdown).Trim();
        Assert.Contains(expected, html);
    }

    /// <summary>
    /// A test case for basic HTML escaping.
    /// It verifies that HTML tags and special characters like `&` are properly escaped in the output.
    /// This is a critical security and rendering feature.
    /// </summary>
    [Fact]
    public void HtmlEscaping_Basic()
    {
        var html = OpenMarkDown.Render("<b>& test</b>");
        Assert.Contains("<p>", html);
        Assert.Contains("&lt;b&gt;&amp; test&lt;/b&gt;", html);
        Assert.Contains("</p>", html);
    }

    /// <summary>
    /// An end-to-end integration test that combines multiple syntax elements in a single document.
    /// This test ensures that different Markdown blocks and inline elements render correctly together without interfering with each other.
    /// </summary>
    /// <param name="args">The input Markdown string, followed by the expected HTML fragments for the entire document.</param>
    [Theory]
    [InlineData(
        "# Title\n\n> Quote line 1\n> Quote line 2\n\n- a\n- b\n\n```\ncode\n```\n\nText with `span` and **bold**.",
        "<h1>Title</h1>",
        "<blockquote>",
        "<p>Quote line 1<br />Quote line 2</p>",
        "</blockquote>",
        "<ul>",
        "<li>a</li>",
        "<li>b</li>",
        "</ul>",
        "<pre><code>",
        "code",
        "</code></pre>",
        "<p>Text with <code>span</code> and <strong>bold</strong>.</p>"
    )]
    public void Complex_EndToEnd(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }
}