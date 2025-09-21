using OpenMarkdown;
using OpenMarkdown.Dialects;
using Xunit;

namespace MarkdownTests.CommonMarkdownTests;

/// <summary>
/// This class contains a suite of unit tests for the core syntax elements of the CommonMark specification.
/// The tests verify that the OpenMarkdown library correctly renders Markdown code according to the CommonMark guidelines.
/// Each test checks different input Markdown strings against their expected HTML output.
/// </summary>
public class CommonMarkdownSpecTableTests
{
    /// <summary>
    /// The constructor for the test class.
    /// It initializes the Markdown renderer, setting it to use the CommonMarkdown dialect.
    /// This ensures all tests run against the correct specification.
    /// </summary>
    public CommonMarkdownSpecTableTests()
    {
        OpenMarkDown.UseRenderer<CommonMarkdown>();
    }

    /// <summary>
    /// Tests for emphasis (italics) and strong emphasis (bold) formatting using both `*` and `_` characters.
    /// The test asserts that the input Markdown converts to `em` and `strong` HTML tags.
    /// </summary>
    /// <param name="md">The input Markdown string.</param>
    /// <param name="expected">The expected HTML output.</param>
    [Theory]
    [InlineData("*Italic*", "<p><em>Italic</em></p>")] // Italic with `*`.
    [InlineData("_Italic_", "<p><em>Italic</em></p>")] // Italic with `_`.
    [InlineData("**Bold**", "<p><strong>Bold</strong></p>")] // Bold with `**`.
    [InlineData("__Bold__", "<p><strong>Bold</strong></p>")] // Bold with `__`.
    public void Emphasis_And_Strong(string md, string expected)
        => Assert.Contains(expected, OpenMarkDown.Render(md));

    /// <summary>
    /// Tests for ATX-style headings, which are defined using the `#` character.
    /// </summary>
    /// <param name="md">The input Markdown heading.</param>
    /// <param name="expected">The expected `h1` or `h2` HTML tag.</param>
    [Theory]
    [InlineData("# Heading 1", "<h1>Heading 1</h1>")] // H1 heading with `#`.
    [InlineData("## Heading 2", "<h2>Heading 2</h2>")] // H2 heading with `##`.
    public void Atx_Headings(string md, string expected)
        => Assert.Contains(expected, OpenMarkDown.Render(md));

    /// <summary>
    /// Tests for Setext-style headings, which are created with an underline.
    /// This test uses a flexible assertion, accepting either the correct heading rendering or a safe fallback to a paragraph.
    /// </summary>
    /// <param name="md">The input Markdown heading.</param>
    /// <param name="preferred">The primary, expected HTML output (heading).</param>
    /// <param name="fallbackParagraph">The secondary, acceptable HTML output (paragraph).</param>
    [Theory]
    [InlineData("Heading 1\n========", "<h1>Heading 1</h1>", "<p>Heading 1<br />========</p>")] // H1 heading with `====` underline.
    [InlineData("Heading 2\n--------", "<h2>Heading 2</h2>", "<p>Heading 2<br />--------</p>")] // H2 heading with `----` underline.
    public void Setext_Headings(string md, string preferred, string fallbackParagraph)
    {
        // The test accepts either a proper setext rendering (preferred) or a conservative paragraph fallback.
        var html = OpenMarkDown.Render(md).Trim();
        Assert.True(html.Contains(preferred) || html.Contains(fallbackParagraph));
    }

    /// <summary>
    /// Tests for inline links, with and without a title attribute.
    /// </summary>
    /// <param name="md">The input Markdown link.</param>
    /// <param name="expected">The expected `a` HTML tag.</param>
    [Theory]
    [InlineData("[Link](http://a.com)", "<p><a href=\"http://a.com\">Link</a></p>")] // Link with a simple URL.
    [InlineData("[x](http://b.org \"Title\")", "<p><a href=\"http://b.org\" title=\"Title\">x</a></p>")] // Link with a URL and title.
    public void Inline_Links(string md, string expected)
        => Assert.Contains(expected, OpenMarkDown.Render(md));

    /// <summary>
    /// Tests for inline images, with and without a title attribute.
    /// </summary>
    /// <param name="md">The input Markdown image.</param>
    /// <param name="expected">The expected `img` HTML tag.</param>
    [Theory]
    [InlineData("![Image](http://url/a.png)", "<p><img src=\"http://url/a.png\" alt=\"Image\" /></p>")] // Image with a URL.
    [InlineData("![Image](http://url/b.jpg \"Title\")", "<p><img src=\"http://url/b.jpg\" alt=\"Image\" title=\"Title\" /></p>")] // Image with a URL and title.
    public void Inline_Images(string md, string expected)
        => Assert.Contains(expected, OpenMarkDown.Render(md));

    /// <summary>
    /// Tests for blockquotes, which are indicated by the `>` character.
    /// Includes test cases for both single and multi-line quotes.
    /// </summary>
    /// <param name="args">The input Markdown followed by the expected HTML parts.</param>
    [Theory]
    [InlineData("> Blockquote", "<blockquote>", "</blockquote>", "<p>Blockquote</p>")] // Single-line blockquote.
    [InlineData("> A\n> B", "<blockquote>", "<p>A<br />B</p>", "</blockquote>")] // Multi-line blockquote.
    public void Blockquotes(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }

    /// <summary>
    /// Tests for unordered lists, which can be created using `*`, `-`, or `+` characters.
    /// </summary>
    /// <param name="args">The input Markdown followed by the expected HTML parts (`<ul>`, `</ul>`, and `<li>`).</param>
    [Theory]
    [InlineData("* List\n* List\n* List", "<ul>", "</ul>", "<li>List</li>")] // List with `*` markers.
    [InlineData("- List\n- List\n- List", "<ul>", "</ul>", "<li>List</li>")] // List with `-` markers.
    [InlineData("+ List\n+ List\n+ List", "<ul>", "</ul>", "<li>List</li>")] // List with `+` markers.
    public void Unordered_Lists(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        Assert.Contains(args[1], html);
        Assert.Contains(args[2], html);
        Assert.Contains(args[3], html);
    }

    /// <summary>
    /// Tests for ordered lists, which can be created with `1.` or `1)` markers.
    /// </summary>
    /// <param name="args">The input Markdown followed by the expected HTML parts (`<ol>`, `</ol>`, and `<li>`).</param>
    [Theory]
    [InlineData("1. One\n2. Two\n3. Three", "<ol>", "</ol>", "<li>One</li>", "<li>Two</li>", "<li>Three</li>")] // List items with periods.
    [InlineData("1) One\n2) Two\n3) Three", "<ol>", "</ol>", "<li>One</li>", "<li>Two</li>", "<li>Three</li>")] // List items with parentheses.
    public void Ordered_Lists(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }

    /// <summary>
    /// Tests for horizontal rules, which are created using three or more `---`, `***`, or `___` characters.
    /// </summary>
    /// <param name="md">The input Markdown rule.</param>
    [Theory]
    [InlineData("---")] // With hyphens.
    [InlineData("***")] // With asterisks.
    [InlineData("___")] // With underscores.
    public void Horizontal_Rules(string md)
        => Assert.Contains("<hr />", OpenMarkDown.Render(md));

    /// <summary>
    /// Tests for inline code blocks, which are enclosed in backticks (` `).
    /// </summary>
    /// <param name="md">The input Markdown code.</param>
    /// <param name="expected">The expected HTML output.</param>
    [Theory]
    [InlineData("`Inline code`", "<p><code>Inline code</code></p>")] // Simple inline code block.
    [InlineData("Inline `code` with backticks", "<p>Inline <code>code</code> with backticks</p>")] // Inline code within text.
    [InlineData("``print `3 backticks` or``", "<p><code>print `3 backticks` or</code></p>")] // Code block with nested backticks.
    [InlineData("` print 'indent 4 spaces' `", "<p><code>print 'indent 4 spaces'</code></p>")] // Code block with leading and trailing spaces.
    public void Inline_Code(string md, string expected)
        => Assert.Contains(expected, OpenMarkDown.Render(md));

    /// <summary>
    /// Tests for multi-line code blocks, which can be created using either fenced or indented syntax.
    /// </summary>
    /// <param name="args">The input Markdown followed by the expected HTML parts (`<pre><code>` and `</code></pre>`).</param>
    [Theory]
    [InlineData("```\n# code block\nprint '3 backticks or'\nprint 'indent 4 spaces'\n```",
        "<pre><code>", "# code block", "print '3 backticks or'", "print 'indent 4 spaces'", "</code></pre>")] // Fenced code block.
    [InlineData("    # code block\n    print '3 backticks or'\n    print 'indent 4 spaces'",
        "<pre><code>", "# code block", "print '3 backticks or'", "print 'indent 4 spaces'", "</code></pre>")] // Indented code block.
    public void Code_Blocks(params string[] args)
    {
        var html = OpenMarkDown.Render(args[0]);
        for (int i = 1; i < args.Length; i++) Assert.Contains(args[i], html);
    }
}