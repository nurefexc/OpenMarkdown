using OpenMarkdown.Dialects;

namespace OpenMarkdown;

/// <summary>
/// A static utility class that serves as the primary entry point for the OpenMarkdown library.
/// It manages the selection of a default markdown renderer and provides several
/// convenience methods for rendering markdown strings to HTML.
/// </summary>
public static class OpenMarkDown
{
    // A private static field to hold the Type of the default renderer.
    // It is initialized to use the CommonMarkdown dialect by default.
    private static Type _defaultRendererType = typeof(CommonMarkdown);

    /// <summary>
    /// Sets the default markdown renderer type that will be used by the parameterless Render(string) method.
    /// This allows for easy configuration of the library's behavior at runtime.
    /// </summary>
    /// <typeparam name="T">The type of the renderer. It must inherit from BaseMarkdown and have a parameterless constructor.</typeparam>
    public static void UseRenderer<T>() where T : BaseMarkdown, new()
    {
        _defaultRendererType = typeof(T);
    }

    /// <summary>
    /// Sets the default markdown renderer type using a runtime Type object.
    /// </summary>
    /// <param name="rendererType">The Type of the renderer to use.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided rendererType is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the provided rendererType does not inherit from BaseMarkdown.</exception>
    public static void UseRenderer(Type rendererType)
    {
        if (rendererType == null) throw new ArgumentNullException(nameof(rendererType));
        if (!typeof(BaseMarkdown).IsAssignableFrom(rendererType))
            throw new ArgumentException("Renderer type must inherit from BaseMarkdown.", nameof(rendererType));
        _defaultRendererType = rendererType;
    }

    /// <summary>
    /// Renders a markdown string to HTML using the currently configured default renderer.
    /// </summary>
    /// <param name="markdown">The markdown string to render.</param>
    /// <returns>The rendered HTML string. Returns an empty string if the input is null.</returns>
    public static string Render(string markdown)
    {
        if (markdown is null) return string.Empty;
        // The Activator.CreateInstance method is used to create a new instance of the default renderer type.
        var instance = (BaseMarkdown)Activator.CreateInstance(_defaultRendererType)!;
        return instance.Render(markdown);
    }

    /// <summary>
    /// Renders a markdown string using a specified renderer type, without changing the default renderer.
    /// This is useful for one-off rendering tasks.
    /// </summary>
    /// <typeparam name="T">The type of the renderer to use for this specific call.</typeparam>
    /// <param name="markdown">The markdown string to render.</param>
    /// <returns>The rendered HTML string. Returns an empty string if the input is null.</returns>
    public static string Render<T>(string markdown) where T : BaseMarkdown, new()
    {
        if (markdown is null) return string.Empty;
        T markdownDialect = new T();
        return markdownDialect.Render(markdown);
    }

    /// <summary>
    /// Renders a markdown string using a provided renderer instance.
    /// This is the most flexible rendering method, as it allows for the use of a pre-configured or dependency-injected renderer instance.
    /// </summary>
    /// <param name="markdown">The markdown string to render.</param>
    /// <param name="renderer">The renderer instance to use.</param>
    /// <returns>The rendered HTML string. Returns an empty string if the input is null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided renderer instance is null.</exception>
    public static string Render(string markdown, BaseMarkdown renderer)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (markdown is null) return string.Empty;
        return renderer.Render(markdown);
    }
}