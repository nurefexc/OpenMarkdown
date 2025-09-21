namespace OpenMarkdown;

/// <summary>
/// This abstract base class defines the fundamental contract for all Markdown renderers.
/// It establishes a common interface for converting Markdown source text into an HTML string,
/// ensuring that any specific Markdown dialect (such as CommonMark or GitHub Flavored Markdown)
/// can be used interchangeably.
/// </summary>
public abstract class BaseMarkdown
{
    /// <summary>
    /// When implemented in a derived class, renders a Markdown string to HTML.
    /// </summary>
    /// <param name="markdown">The input Markdown string to be rendered.</param>
    /// <returns>The resulting HTML string.</returns>
    public abstract string Render(string markdown);
}