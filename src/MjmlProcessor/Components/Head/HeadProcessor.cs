using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Head;

/// <summary>
/// Applies the contents of <c>mj-head</c> to the render context. Head elements do not render
/// markup themselves; they configure the document and the defaults body components inherit.
/// </summary>
internal static class HeadProcessor
{
    public static void Process(MjmlNode head, RenderContext context)
    {
        foreach (var node in head.Children)
        {
            switch (node.TagName)
            {
                case "mj-title":
                    context.Title = node.Content?.Trim();
                    break;

                case "mj-preview":
                    context.Preview = node.Content?.Trim();
                    break;

                case "mj-breakpoint":
                    ApplyBreakpoint(node, context);
                    break;

                case "mj-font":
                    ApplyFont(node, context);
                    break;

                case "mj-style":
                    ApplyStyle(node, context);
                    break;

                case "mj-attributes":
                    ApplyAttributes(node, context);
                    break;

                case "mj-raw":
                    context.AddHeadRawMarkup(node.Content ?? string.Empty);
                    break;

                case "mj-html-attributes":
                    context.Warn(node, "mj-html-attributes is not supported and was ignored.");
                    break;

                default:
                    context.Warn(node, "Unknown head element <" + node.TagName + "> was ignored.");
                    break;
            }
        }
    }

    private static void ApplyBreakpoint(MjmlNode node, RenderContext context)
    {
        var width = node.GetAttribute("width");
        if (width is null)
        {
            context.Warn(node, "mj-breakpoint requires a width attribute.");
            return;
        }

        context.Breakpoint = CssUtils.ParseNumber(width, 480);
    }

    private static void ApplyFont(MjmlNode node, RenderContext context)
    {
        var name = node.GetAttribute("name");
        var href = node.GetAttribute("href");

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(href))
        {
            context.Warn(node, "mj-font requires both name and href attributes.");
            return;
        }

        context.Fonts[name!] = href!;
    }

    private static void ApplyStyle(MjmlNode node, RenderContext context)
    {
        if (string.Equals(node.GetAttribute("inline"), "inline", StringComparison.OrdinalIgnoreCase))
        {
            context.AddInlineStyle(node.Content ?? string.Empty);
            return;
        }

        context.AddHeadStyle(node.Content ?? string.Empty);
    }

    /// <summary>
    /// Reads <c>mj-attributes</c>: <c>mj-all</c> sets global defaults, a tag name sets defaults
    /// for that component, and a leading dot declares a named set referenced by <c>mj-class</c>.
    /// </summary>
    private static void ApplyAttributes(MjmlNode node, RenderContext context)
    {
        foreach (var child in node.Children)
        {
            if (child.TagName.Equals("mj-all", StringComparison.OrdinalIgnoreCase))
            {
                Merge(context.GlobalDefaults, child);
                continue;
            }

            // Named sets are declared as <mj-class name="big" ... />. A leading-dot tag name
            // is also accepted because it reads naturally and costs nothing to support.
            var isNamedClass = child.TagName.Equals("mj-class", StringComparison.OrdinalIgnoreCase);
            if (isNamedClass || child.TagName.StartsWith(".", StringComparison.Ordinal))
            {
                var className = isNamedClass ? child.GetAttribute("name") : child.TagName.Substring(1);
                if (string.IsNullOrWhiteSpace(className))
                {
                    context.Warn(child, "mj-class requires a name attribute.");
                    continue;
                }

                if (!context.ClassDefaults.TryGetValue(className!, out var target))
                {
                    target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    context.ClassDefaults[className!] = target;
                }

                Merge(target, child, skip: isNamedClass ? "name" : null);
                continue;
            }

            if (!context.TagDefaults.TryGetValue(child.TagName, out var tagTarget))
            {
                tagTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                context.TagDefaults[child.TagName] = tagTarget;
            }

            Merge(tagTarget, child);
        }
    }

    private static void Merge(IDictionary<string, string> target, MjmlNode source, string? skip = null)
    {
        foreach (var attribute in source.Attributes)
        {
            if (skip is not null && attribute.Key.Equals(skip, StringComparison.OrdinalIgnoreCase)) continue;
            target[attribute.Key] = attribute.Value;
        }
    }
}
