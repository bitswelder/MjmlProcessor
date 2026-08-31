using System.Text;

namespace MjmlProcessor.Css;

/// <summary>A single CSS declaration, for example <c>color: red !important</c>.</summary>
internal sealed class CssDeclaration
{
    public CssDeclaration(string property, string value, bool isImportant)
    {
        Property = property;
        Value = value;
        IsImportant = isImportant;
    }

    public string Property { get; }

    public string Value { get; }

    public bool IsImportant { get; }
}

/// <summary>A style rule whose selectors can all be resolved statically.</summary>
internal sealed class CssRule
{
    public CssRule(ComplexSelector selector, IReadOnlyList<CssDeclaration> declarations, int order)
    {
        Selector = selector;
        Declarations = declarations;
        Order = order;
    }

    public ComplexSelector Selector { get; }

    public IReadOnlyList<CssDeclaration> Declarations { get; }

    /// <summary>Document order, used to break specificity ties.</summary>
    public int Order { get; }
}

/// <summary>
/// The result of parsing one or more stylesheets: the rules that can be inlined, and the
/// text of everything that cannot be (at-rules, pseudo-class selectors) so it can be kept
/// in a style block.
/// </summary>
internal sealed class CssStylesheet
{
    public List<CssRule> Rules { get; } = new();

    public List<string> Preserved { get; } = new();

    /// <summary>Renders the non-inlinable parts back into CSS text.</summary>
    public string BuildPreservedCss()
    {
        if (Preserved.Count == 0) return string.Empty;
        return string.Join("\n", Preserved);
    }
}

/// <summary>
/// A small CSS parser covering the subset that appears in email stylesheets: style rules,
/// at-rules and declarations. It does not validate; anything it cannot interpret is preserved
/// verbatim rather than dropped.
/// </summary>
internal static class CssParser
{
    public static CssStylesheet Parse(IEnumerable<string> sources)
    {
        var stylesheet = new CssStylesheet();
        var order = 0;

        foreach (var source in sources)
        {
            if (!string.IsNullOrWhiteSpace(source)) ParseInto(source, stylesheet, ref order);
        }

        return stylesheet;
    }

    private static void ParseInto(string css, CssStylesheet stylesheet, ref int order)
    {
        var position = 0;

        while (position < css.Length)
        {
            SkipTrivia(css, ref position);
            if (position >= css.Length) break;

            if (css[position] == '@')
            {
                ParseAtRule(css, ref position, stylesheet);
                continue;
            }

            var selectorStart = position;
            var braceIndex = FindBlockStart(css, position);
            if (braceIndex < 0) break;

            var selectorText = css.Substring(selectorStart, braceIndex - selectorStart).Trim();
            position = braceIndex + 1;

            var blockEnd = FindBlockEnd(css, position);
            var body = css.Substring(position, blockEnd - position);
            position = blockEnd < css.Length ? blockEnd + 1 : css.Length;

            if (selectorText.Length == 0) continue;

            var declarations = ParseDeclarations(body);
            if (declarations.Count == 0) continue;

            var preservedSelectors = new List<string>();

            foreach (var candidate in SplitTopLevel(selectorText, ','))
            {
                var trimmed = candidate.Trim();
                if (trimmed.Length == 0) continue;

                if (SelectorParser.TryParse(trimmed, out var selector))
                {
                    stylesheet.Rules.Add(new CssRule(selector!, declarations, order++));
                }
                else
                {
                    // Rules such as :hover or ::before have no static equivalent on an
                    // element, so they have to stay in a style block.
                    preservedSelectors.Add(trimmed);
                }
            }

            if (preservedSelectors.Count > 0)
            {
                stylesheet.Preserved.Add(
                    string.Join(", ", preservedSelectors) + " { " + RenderDeclarations(declarations) + " }");
            }
        }
    }

    /// <summary>At-rules cannot be inlined, so they are captured verbatim.</summary>
    private static void ParseAtRule(string css, ref int position, CssStylesheet stylesheet)
    {
        var start = position;
        var depth = 0;
        var quote = '\0';

        while (position < css.Length)
        {
            var c = css[position];

            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                position++;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                quote = c;
                position++;
                continue;
            }

            if (c == ';' && depth == 0)
            {
                position++;
                stylesheet.Preserved.Add(css.Substring(start, position - start).Trim());
                return;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    position++;
                    stylesheet.Preserved.Add(css.Substring(start, position - start).Trim());
                    return;
                }
            }

            position++;
        }

        stylesheet.Preserved.Add(css.Substring(start).Trim());
    }

    private static void SkipTrivia(string css, ref int position)
    {
        while (position < css.Length)
        {
            if (char.IsWhiteSpace(css[position]))
            {
                position++;
                continue;
            }

            if (position + 1 < css.Length && css[position] == '/' && css[position + 1] == '*')
            {
                var end = css.IndexOf("*/", position + 2, StringComparison.Ordinal);
                position = end < 0 ? css.Length : end + 2;
                continue;
            }

            return;
        }
    }

    /// <summary>Finds the opening brace of a rule body, ignoring braces inside strings.</summary>
    private static int FindBlockStart(string css, int position)
    {
        var quote = '\0';

        for (var i = position; i < css.Length; i++)
        {
            var c = css[i];

            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }

            if (c == '"' || c == '\'') quote = c;
            else if (c == '{') return i;
            else if (c == '}') return -1;
        }

        return -1;
    }

    private static int FindBlockEnd(string css, int position)
    {
        var quote = '\0';
        var depth = 0;

        for (var i = position; i < css.Length; i++)
        {
            var c = css[i];

            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }

            if (c == '"' || c == '\'') quote = c;
            else if (c == '{') depth++;
            else if (c == '}')
            {
                if (depth == 0) return i;
                depth--;
            }
        }

        return css.Length;
    }

    public static List<CssDeclaration> ParseDeclarations(string body)
    {
        var declarations = new List<CssDeclaration>();

        foreach (var part in SplitTopLevel(body, ';'))
        {
            var text = part.Trim();
            if (text.Length == 0) continue;

            var colon = IndexOfTopLevel(text, ':');
            if (colon <= 0) continue;

            var property = text.Substring(0, colon).Trim().ToLowerInvariant();
            var value = text.Substring(colon + 1).Trim();

            if (property.Length == 0 || value.Length == 0) continue;

            var isImportant = false;
            var bang = value.LastIndexOf('!');
            if (bang >= 0 && value.Substring(bang + 1).Trim().Equals("important", StringComparison.OrdinalIgnoreCase))
            {
                isImportant = true;
                value = value.Substring(0, bang).Trim();
            }

            if (value.Length == 0) continue;

            declarations.Add(new CssDeclaration(property, value, isImportant));
        }

        return declarations;
    }

    public static string RenderDeclarations(IReadOnlyList<CssDeclaration> declarations)
    {
        var builder = new StringBuilder();

        foreach (var declaration in declarations)
        {
            builder.Append(declaration.Property).Append(':').Append(declaration.Value);
            if (declaration.IsImportant) builder.Append(" !important");
            builder.Append("; ");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Splits on a separator that sits outside strings, parentheses and brackets.</summary>
    public static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var quote = '\0';
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }

            switch (c)
            {
                case '"':
                case '\'':
                    quote = c;
                    break;
                case '(':
                case '[':
                    depth++;
                    break;
                case ')':
                case ']':
                    if (depth > 0) depth--;
                    break;
                default:
                    if (c == separator && depth == 0)
                    {
                        parts.Add(text.Substring(start, i - start));
                        start = i + 1;
                    }

                    break;
            }
        }

        parts.Add(text.Substring(start));
        return parts;
    }

    private static int IndexOfTopLevel(string text, char target)
    {
        var depth = 0;
        var quote = '\0';

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }

            if (c == '"' || c == '\'') quote = c;
            else if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') { if (depth > 0) depth--; }
            else if (c == target && depth == 0) return i;
        }

        return -1;
    }
}
