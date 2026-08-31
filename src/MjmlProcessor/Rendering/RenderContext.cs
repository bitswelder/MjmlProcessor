using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;

namespace MjmlProcessor.Rendering;

/// <summary>
/// State shared by every component while a single document is rendered: head data collected
/// from <c>mj-head</c>, the responsive media queries registered by columns, and the fonts and
/// component styles that must end up in the document head.
/// </summary>
internal sealed class RenderContext
{
    private readonly List<MjmlWarning> _warnings = new();
    private readonly Dictionary<string, string> _mediaQueries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _componentStyles = new(StringComparer.Ordinal);
    private readonly List<string> _headStyles = new();
    private readonly List<string> _inlineStyles = new();
    private readonly List<string> _headRawMarkup = new();
    private readonly HashSet<string> _usedFonts = new(StringComparer.OrdinalIgnoreCase);

    public RenderContext(MjmlOptions options)
    {
        Options = options;
        Fonts = new Dictionary<string, string>(options.Fonts, StringComparer.OrdinalIgnoreCase);
    }

    public MjmlOptions Options { get; }

    public IReadOnlyList<MjmlWarning> Warnings => _warnings;

    /// <summary>Fonts available to the document, keyed by family name.</summary>
    public Dictionary<string, string> Fonts { get; }

    /// <summary>Document title from <c>mj-title</c>.</summary>
    public string? Title { get; set; }

    /// <summary>Inbox preview text from <c>mj-preview</c>.</summary>
    public string? Preview { get; set; }

    /// <summary>Mobile breakpoint in pixels, set by <c>mj-breakpoint</c>.</summary>
    public double Breakpoint { get; set; } = 480;

    /// <summary>Language for the html element, overridable by <c>mj-html-attributes</c>.</summary>
    public string? Language { get; set; }

    /// <summary>Per-tag defaults declared through <c>mj-attributes</c>.</summary>
    public Dictionary<string, Dictionary<string, string>> TagDefaults { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Named attribute sets declared through <c>mj-attributes</c>, referenced by <c>mj-class</c>.</summary>
    public Dictionary<string, Dictionary<string, string>> ClassDefaults { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Defaults declared through <c>mj-all</c> and applied to every component.</summary>
    public Dictionary<string, string> GlobalDefaults { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Custom CSS collected from <c>mj-style</c>.</summary>
    public IReadOnlyList<string> HeadStyles => _headStyles;

    /// <summary>Raw markup collected from <c>mj-raw position="file-start"</c> and head-level raw blocks.</summary>
    public IReadOnlyList<string> HeadRawMarkup => _headRawMarkup;

    /// <summary>Responsive rules registered by columns, keyed by class name.</summary>
    public IReadOnlyDictionary<string, string> MediaQueries => _mediaQueries;

    /// <summary>Static stylesheets contributed by components such as mj-navbar and mj-accordion.</summary>
    public IEnumerable<string> ComponentStyles => _componentStyles.Values;

    /// <summary>CSS collected from <c>mj-style inline="inline"</c>, to be merged into style attributes.</summary>
    public IReadOnlyList<string> InlineStyles => _inlineStyles;

    public void AddHeadStyle(string css)
    {
        if (!string.IsNullOrWhiteSpace(css)) _headStyles.Add(css);
    }

    public void AddInlineStyle(string css)
    {
        if (!string.IsNullOrWhiteSpace(css)) _inlineStyles.Add(css);
    }

    public void AddHeadRawMarkup(string markup)
    {
        if (!string.IsNullOrWhiteSpace(markup)) _headRawMarkup.Add(markup);
    }

    /// <summary>Registers a component stylesheet once, no matter how many instances there are.</summary>
    public void AddComponentStyle(string key, string css)
    {
        if (!_componentStyles.ContainsKey(key)) _componentStyles[key] = css;
    }

    /// <summary>Registers the responsive width rule for a column class.</summary>
    public void AddMediaQuery(string className, CssSize width)
    {
        if (_mediaQueries.ContainsKey(className)) return;

        var value = width.IsPercent
            ? CssUtils.Number(width.Value) + "%"
            : CssUtils.Px(width.Value);

        _mediaQueries[className] = "{ width:" + value + " !important; max-width: " + value + "; }";
    }

    /// <summary>Records that a font family is referenced somewhere in the document.</summary>
    public void UseFont(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily)) return;

        foreach (var part in fontFamily!.Split(','))
        {
            var name = part.Trim().Trim('"', '\'');
            if (name.Length > 0) _usedFonts.Add(name);
        }
    }

    /// <summary>Returns the stylesheet URLs of every declared font actually used by the document.</summary>
    public IEnumerable<string> ResolveFontImports()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var font in Fonts)
        {
            if (_usedFonts.Contains(font.Key) && seen.Add(font.Value))
            {
                yield return font.Value;
            }
        }
    }

    /// <summary>Reports a validation problem according to the configured validation level.</summary>
    public void Warn(MjmlNode node, string message) => Warn(node.TagName, message, node.Line, node.Column);

    /// <summary>Reports a validation problem according to the configured validation level.</summary>
    public void Warn(string tagName, string message, int line, int column)
    {
        switch (Options.ValidationLevel)
        {
            case MjmlValidationLevel.Skip:
                return;
            case MjmlValidationLevel.Strict:
                throw new MjmlException(tagName + ": " + message, line, column);
            default:
                _warnings.Add(new MjmlWarning(tagName, message, line, column));
                return;
        }
    }
}
