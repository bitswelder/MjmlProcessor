using MjmlProcessor.Parsing;

namespace MjmlProcessor.Css;

/// <summary>
/// An element discovered in rendered HTML, tracked by its offsets in the source so the
/// inliner can rewrite a single attribute without re-serialising the document.
/// </summary>
internal sealed class HtmlElement
{
    public HtmlElement(string tagName) => TagName = tagName;

    public string TagName { get; }

    /// <summary>Attribute values, decoded, keyed case-insensitively.</summary>
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HtmlElement? Parent { get; set; }

    public List<HtmlElement> Children { get; } = new();

    /// <summary>1-based position among the element children of the parent.</summary>
    public int ChildIndex { get; set; }

    /// <summary>True when the start tag already carries a style attribute.</summary>
    public bool HasStyle { get; set; }

    /// <summary>Offset of the first character of the style attribute's raw value.</summary>
    public int StyleValueStart { get; set; } = -1;

    /// <summary>Offset one past the last character of the style attribute's raw value.</summary>
    public int StyleValueEnd { get; set; } = -1;

    /// <summary>Where a new attribute can be inserted, immediately after the last existing one.</summary>
    public int AttributeInsertPosition { get; set; }

    private string[]? _classes;

    /// <summary>The element's class names, split once and cached.</summary>
    public string[] Classes
    {
        get
        {
            if (_classes is null)
            {
                _classes = Attributes.TryGetValue("class", out var value) && value.Length > 0
                    ? value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    : Array.Empty<string>();
            }

            return _classes;
        }
    }

    public string? GetAttribute(string name)
        => Attributes.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// A tolerant, read-only HTML scanner. It exists only to locate elements and their style
/// attributes in already-rendered markup, so it never rewrites or normalises the source.
/// Comment contents — which is where Outlook conditional markup lives — are skipped entirely.
/// </summary>
internal static class HtmlDocument
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr",
    };

    private static readonly HashSet<string> RawTextElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "textarea", "title",
    };

    /// <summary>Elements that close an open paragraph when they start.</summary>
    private static readonly HashSet<string> ClosesParagraph = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "div", "dl", "fieldset", "figure",
        "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "main",
        "nav", "ol", "p", "pre", "section", "table", "ul",
    };

    /// <summary>Parses <paramref name="html"/> and returns every element it contains, in document order.</summary>
    public static List<HtmlElement> Parse(string html)
    {
        var all = new List<HtmlElement>();
        var roots = new List<HtmlElement>();
        var stack = new List<HtmlElement>();
        var position = 0;

        while (position < html.Length)
        {
            if (html[position] != '<')
            {
                position++;
                continue;
            }

            if (StartsWith(html, position, "<!--"))
            {
                var end = html.IndexOf("-->", position + 4, StringComparison.Ordinal);
                position = end < 0 ? html.Length : end + 3;
                continue;
            }

            if (StartsWith(html, position, "<!") || StartsWith(html, position, "<?"))
            {
                var end = html.IndexOf('>', position);
                position = end < 0 ? html.Length : end + 1;
                continue;
            }

            if (StartsWith(html, position, "</"))
            {
                position += 2;
                var name = ReadName(html, ref position);
                var close = html.IndexOf('>', position);
                position = close < 0 ? html.Length : close + 1;

                CloseElement(stack, name);
                continue;
            }

            if (position + 1 >= html.Length || !IsNameStart(html[position + 1]))
            {
                position++;
                continue;
            }

            var element = ParseStartTag(html, ref position, out var selfClosing);
            if (element is null) continue;

            ApplyImpliedEndTags(stack, element.TagName);

            if (stack.Count > 0)
            {
                var parent = stack[stack.Count - 1];
                element.Parent = parent;
                parent.Children.Add(element);
                element.ChildIndex = parent.Children.Count;
            }
            else
            {
                roots.Add(element);
                element.ChildIndex = roots.Count;
            }

            all.Add(element);

            if (selfClosing || VoidElements.Contains(element.TagName)) continue;

            if (RawTextElements.Contains(element.TagName))
            {
                SkipRawText(html, ref position, element.TagName);
                continue;
            }

            stack.Add(element);
        }

        return all;
    }

    private static HtmlElement? ParseStartTag(string html, ref int position, out bool selfClosing)
    {
        selfClosing = false;
        position++; // consume '<'

        var name = ReadName(html, ref position);
        if (name.Length == 0) return null;

        var element = new HtmlElement(name.ToLowerInvariant())
        {
            AttributeInsertPosition = position,
        };

        while (position < html.Length)
        {
            SkipWhitespace(html, ref position);
            if (position >= html.Length) break;

            var c = html[position];

            if (c == '>')
            {
                position++;
                return element;
            }

            if (c == '/')
            {
                position++;
                SkipWhitespace(html, ref position);
                if (position < html.Length && html[position] == '>')
                {
                    position++;
                    selfClosing = true;
                }

                return element;
            }

            var attributeName = ReadAttributeName(html, ref position);
            if (attributeName.Length == 0)
            {
                position++;
                continue;
            }

            SkipWhitespace(html, ref position);

            var value = string.Empty;
            var valueStart = -1;
            var valueEnd = -1;

            if (position < html.Length && html[position] == '=')
            {
                position++;
                SkipWhitespace(html, ref position);
                value = ReadAttributeValue(html, ref position, out valueStart, out valueEnd);
            }

            element.AttributeInsertPosition = position;

            if (attributeName.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                element.HasStyle = true;
                element.StyleValueStart = valueStart;
                element.StyleValueEnd = valueEnd;
            }

            element.Attributes[attributeName] = HtmlEntities.Decode(value);
        }

        return element;
    }

    /// <summary>Closes elements whose end tag is optional when an incompatible element starts.</summary>
    private static void ApplyImpliedEndTags(List<HtmlElement> stack, string tagName)
    {
        while (stack.Count > 0)
        {
            var open = stack[stack.Count - 1].TagName;

            var implied =
                (open == "p" && ClosesParagraph.Contains(tagName)) ||
                (open == "li" && tagName == "li") ||
                (open == "dt" && (tagName == "dt" || tagName == "dd")) ||
                (open == "dd" && (tagName == "dt" || tagName == "dd")) ||
                (open == "option" && tagName == "option") ||
                ((open == "td" || open == "th") && (tagName == "td" || tagName == "th" || tagName == "tr")) ||
                (open == "tr" && tagName == "tr") ||
                ((open == "td" || open == "th" || open == "tr") &&
                 (tagName == "thead" || tagName == "tbody" || tagName == "tfoot"));

            if (!implied) return;

            stack.RemoveAt(stack.Count - 1);
        }
    }

    /// <summary>Pops to the matching open element, ignoring a close tag that was never opened.</summary>
    private static void CloseElement(List<HtmlElement> stack, string name)
    {
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (!stack[i].TagName.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;

            stack.RemoveRange(i, stack.Count - i);
            return;
        }
    }

    private static void SkipRawText(string html, ref int position, string tagName)
    {
        var search = "</" + tagName;

        while (position < html.Length)
        {
            var index = html.IndexOf(search, position, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                position = html.Length;
                return;
            }

            var after = index + search.Length;
            if (after >= html.Length || html[after] == '>' || char.IsWhiteSpace(html[after]) || html[after] == '/')
            {
                var close = html.IndexOf('>', after);
                position = close < 0 ? html.Length : close + 1;
                return;
            }

            position = after;
        }
    }

    private static string ReadAttributeValue(string html, ref int position, out int valueStart, out int valueEnd)
    {
        valueStart = -1;
        valueEnd = -1;

        if (position >= html.Length) return string.Empty;

        var quote = html[position];
        if (quote == '"' || quote == '\'')
        {
            position++;
            valueStart = position;
            while (position < html.Length && html[position] != quote) position++;
            valueEnd = position;
            var value = html.Substring(valueStart, valueEnd - valueStart);
            if (position < html.Length) position++;
            return value;
        }

        valueStart = position;
        while (position < html.Length && !char.IsWhiteSpace(html[position]) && html[position] != '>') position++;
        valueEnd = position;
        return html.Substring(valueStart, valueEnd - valueStart);
    }

    private static string ReadName(string html, ref int position)
    {
        var start = position;
        while (position < html.Length)
        {
            var c = html[position];
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':') position++;
            else break;
        }

        return html.Substring(start, position - start);
    }

    private static string ReadAttributeName(string html, ref int position)
    {
        var start = position;
        while (position < html.Length)
        {
            var c = html[position];
            if (char.IsWhiteSpace(c) || c == '=' || c == '>' || c == '/' || c == '<' || c == '"' || c == '\'') break;
            position++;
        }

        return html.Substring(start, position - start);
    }

    private static void SkipWhitespace(string html, ref int position)
    {
        while (position < html.Length && char.IsWhiteSpace(html[position])) position++;
    }

    private static bool StartsWith(string html, int position, string value)
        => position + value.Length <= html.Length
           && string.CompareOrdinal(html, position, value, 0, value.Length) == 0;

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_' || c >= 0x80;
}
