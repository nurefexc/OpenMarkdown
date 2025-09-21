# OpenMarkdown

A lightweight, extensible Markdown parser for .NET, built with a clean, modular architecture. It provides a simple way to convert Markdown source into HTML, supporting multiple dialects.

- - -

## Key Features

*   **Extensible Architecture**: Easily add new Markdown dialects by inheriting from the `BaseMarkdown` class.
*   **CommonMark Support**: The default `CommonMarkdown` renderer handles standard Markdown syntax.
*   **GitHub Flavored Markdown (GFM)**: A dedicated `GitHubFlavoredMarkdown` renderer adds GFM features like tables, task lists, and strikethrough.
*   **Simple API**: A static `OpenMarkDown` utility class provides a clean, user-friendly interface for rendering.

- - -

## Usage

### 1\. Basic Usage (using the default renderer)

The library is pre-configured to use the `CommonMarkdown` renderer by default.

```csharp
using OpenMarkdown;

string markdown = "# Hello, Markdown!\nThis is a **bold** paragraph.";
string html = OpenMarkDown.Render(markdown);
Console.WriteLine(html);
// Output: <h1>Hello, Markdown!</h1><p>This is a <strong>bold</strong> paragraph.</p>
```

### 2\. Using a Specific Renderer

You can specify a different renderer for a one-off conversion using the generic `Render<T>` method.

```csharp
using OpenMarkdown;
using OpenMarkdown.Dialects;

string gfmMarkdown = "- [x] Task list item\n\n~~Strikethrough~~";
string html = OpenMarkDown.Render<GitHubFlavoredMarkdown>(gfmMarkdown);
Console.WriteLine(html);
// Output: <ul><li class="task-list-item"><input type="checkbox" disabled checked /> Task list item</li></ul><p><del>Strikethrough</del></p>
```

### 3\. Setting a Default Renderer

To change the default renderer for all subsequent `OpenMarkDown.Render()` calls, use the `UseRenderer` method.

```csharp
using OpenMarkdown;
using OpenMarkdown.Dialects;

// Set GFM as the new default renderer
OpenMarkDown.UseRenderer<GitHubFlavoredMarkdown>();

string markdown = "A raw link: https://example.com";
string html = OpenMarkDown.Render(markdown); // Uses the new GFM default
Console.WriteLine(html);
// Output: <p>A raw link: <a href="https://example.com">https://example.com</a></p>
```

- - -

## Project Structure

The project is designed to be modular and easy to navigate.

*   `BaseMarkdown.cs`: The abstract base class that defines the core rendering contract.
*   `OpenMarkDown.cs`: The static entry point for the library.
*   `Dialects/`: A folder containing specific Markdown dialect implementations.
    *   `CommonMarkdown.cs`: The standard CommonMark renderer.
    *   `GitHubFlavoredMarkdown.cs`: Extends `CommonMarkdown` to add GFM features.