using System.Text;
using MjmlProcessor.Components;
using MjmlProcessor.Components.Body;
using MjmlProcessor.Components.Head;
using MjmlProcessor.Css;
using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor;

/// <summary>
/// Converts MJML markup into responsive, Outlook-compatible HTML. Instances are immutable
/// once constructed and safe to share across threads.
/// </summary>
public sealed class MjmlConverter
{
    private readonly MjmlOptions _options;

    /// <summary>Creates a converter using <see cref="MjmlOptions.Default"/>.</summary>
    public MjmlConverter() : this(null) { }

    /// <summary>Creates a converter with the supplied options.</summary>
    public MjmlConverter(MjmlOptions? options)
    {
        _options = (options ?? MjmlOptions.Default).Clone();
    }

    /// <summary>Converts <paramref name="mjml"/> and returns the HTML together with any warnings.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="mjml"/> is null.</exception>
    /// <exception cref="MjmlParseException">The markup is syntactically invalid.</exception>
    /// <exception cref="MjmlException">The document is invalid and validation is strict.</exception>
    public MjmlResult Convert(string mjml)
    {
        if (mjml is null) throw new ArgumentNullException(nameof(mjml));

        var root = MjmlParser.Parse(mjml);
        var context = new RenderContext(_options);

        ResolveIncludes(root, context, 0);

        MjmlNode? head = null;
        MjmlNode? body = null;

        foreach (var child in root.Children)
        {
            if (child.TagName == "mj-head" && head is null) head = child;
            else if (child.TagName == "mj-body" && body is null) body = child;
        }

        if (head is not null) HeadProcessor.Process(head, context);

        var bodyMarkup = string.Empty;
        string? backgroundColor = null;

        if (body is null)
        {
            context.Warn("mjml", "The document has no <mj-body> element.", root.Line, root.Column);
        }
        else
        {
            var component = ComponentFactory.Build(body, context, null) as BodyComponent;
            if (component is not null)
            {
                backgroundColor = component.BackgroundColor;
                component.ContainerWidth = component.Width;

                var writer = new HtmlWriter(_options.Beautify);
                component.Render(writer);
                bodyMarkup = writer.ToString();
            }
        }

        CssStylesheet? inlineSheet = null;
        if (context.InlineStyles.Count > 0)
        {
            inlineSheet = CssParser.Parse(context.InlineStyles);

            // Media queries and pseudo-classes have no inline equivalent, so whatever cannot
            // be inlined keeps a style block. This has to happen before the head is built.
            var preserved = inlineSheet.BuildPreservedCss();
            if (preserved.Length > 0) context.AddHeadStyle(preserved);
        }

        if (!_options.IncludeDocumentSkeleton && context.HeadStyles.Count > 0)
        {
            context.Warn("mj-style", "CSS that needs a <style> block was dropped because " +
                                     "MjmlOptions.IncludeDocumentSkeleton is false.", 0, 0);
        }

        var html = _options.IncludeDocumentSkeleton
            ? DocumentSkeleton.Build(bodyMarkup, context, backgroundColor)
            : bodyMarkup;

        // Inlining runs over the finished document so it reaches both the author's own HTML
        // inside mj-text and mj-raw and the elements the skeleton contributes, such as <body>.
        if (inlineSheet is not null) html = CssInliner.Apply(html, inlineSheet);

        if (_options.Minify) html = Minifier.Minify(html);

        return new MjmlResult(html, new List<MjmlWarning>(context.Warnings));
    }

    /// <summary>Converts <paramref name="mjml"/> and returns only the HTML.</summary>
    public string ConvertToHtml(string mjml) => Convert(mjml).Html;

    /// <summary>
    /// Reads a MJML file and converts it, resolving <c>mj-include</c> paths relative to the
    /// file's own directory unless a loader is already configured.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public MjmlResult ConvertFile(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("MJML file not found.", path);

        var source = File.ReadAllText(path);
        if (_options.FileLoader is not null) return Convert(source);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        var options = _options.Clone();
        options.FileLoader = new DirectoryFileLoader(directory ?? ".");
        return new MjmlConverter(options).Convert(source);
    }

    /// <summary>
    /// Replaces <c>mj-include</c> elements with the parsed contents of the referenced file.
    /// </summary>
    private void ResolveIncludes(MjmlNode node, RenderContext context, int depth)
    {
        const int maxDepth = 10;

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];

            if (child.TagName != "mj-include")
            {
                ResolveIncludes(child, context, depth);
                continue;
            }

            node.Children.RemoveAt(i);

            var path = child.GetAttribute("path");
            if (string.IsNullOrWhiteSpace(path))
            {
                context.Warn(child, "mj-include requires a path attribute.");
                i--;
                continue;
            }

            if (depth >= maxDepth)
            {
                context.Warn(child, "mj-include nesting exceeded " + maxDepth + " levels; the include was skipped.");
                i--;
                continue;
            }

            if (_options.FileLoader is null)
            {
                context.Warn(child, "mj-include was ignored because no MjmlOptions.FileLoader is configured.");
                i--;
                continue;
            }

            var content = _options.FileLoader.Load(path!);
            if (content is null)
            {
                context.Warn(child, "mj-include could not resolve \"" + path + "\".");
                i--;
                continue;
            }

            var type = child.GetAttribute("type");

            if (string.Equals(type, "css", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(child.GetAttribute("css-inline"), "inline", StringComparison.OrdinalIgnoreCase))
                {
                    context.AddInlineStyle(content);
                }
                else
                {
                    context.AddHeadStyle(content);
                }

                i--;
                continue;
            }

            if (string.Equals(type, "html", StringComparison.OrdinalIgnoreCase))
            {
                var raw = new MjmlNode("mj-raw", child.Line, child.Column) { Content = content };
                node.Children.Insert(i, raw);
                continue;
            }

            var included = MjmlParser.Parse(content);
            ResolveIncludes(included, context, depth + 1);

            // An included file may be a full document or a bare fragment; splice in whichever it is.
            var inserted = 0;
            foreach (var candidate in included.Children)
            {
                if (candidate.TagName == "mj-body" || candidate.TagName == "mj-head")
                {
                    foreach (var grandChild in candidate.Children)
                    {
                        node.Children.Insert(i + inserted, grandChild);
                        inserted++;
                    }
                }
                else
                {
                    node.Children.Insert(i + inserted, candidate);
                    inserted++;
                }
            }

            i--;
        }
    }

    /// <summary>Converts <paramref name="mjml"/> to HTML using the supplied options.</summary>
    public static string ToHtml(string mjml, MjmlOptions? options = null)
        => new MjmlConverter(options).ConvertToHtml(mjml);

    /// <summary>Converts <paramref name="mjml"/> using the supplied options.</summary>
    public static MjmlResult Render(string mjml, MjmlOptions? options = null)
        => new MjmlConverter(options).Convert(mjml);
}

/// <summary>Resolves <c>mj-include</c> paths against a directory on disk.</summary>
public sealed class DirectoryFileLoader : IMjmlFileLoader
{
    private readonly string _root;

    /// <summary>Creates a loader rooted at <paramref name="root"/>.</summary>
    public DirectoryFileLoader(string root)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        _root = Path.GetFullPath(root);
    }

    /// <inheritdoc />
    public string? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var candidate = Path.GetFullPath(Path.Combine(_root, path));

        // Keep includes inside the configured root so a template cannot read arbitrary files.
        if (!candidate.StartsWith(_root, StringComparison.OrdinalIgnoreCase)) return null;

        if (File.Exists(candidate)) return File.ReadAllText(candidate);

        var withExtension = candidate + ".mjml";
        return File.Exists(withExtension) ? File.ReadAllText(withExtension) : null;
    }
}

/// <summary>Collapses the whitespace the renderer adds between tags.</summary>
internal static class Minifier
{
    public static string Minify(string html)
    {
        var builder = new StringBuilder(html.Length);

        for (var i = 0; i < html.Length; i++)
        {
            var c = html[i];

            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
                continue;
            }

            var end = i;
            var sawNewline = false;
            while (end < html.Length && char.IsWhiteSpace(html[end]))
            {
                if (html[end] == '\n') sawNewline = true;
                end++;
            }

            var previous = builder.Length > 0 ? builder[builder.Length - 1] : '\0';
            var next = end < html.Length ? html[end] : '\0';

            // Only whitespace the renderer introduced between tags is safe to drop; a run
            // inside text content still separates words.
            if (sawNewline && (previous == '>' || previous == '\0') && (next == '<' || next == '\0'))
            {
                i = end - 1;
                continue;
            }

            builder.Append(' ');
            i = end - 1;
        }

        return builder.ToString();
    }
}
