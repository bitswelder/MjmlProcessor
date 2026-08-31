using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components;

/// <summary>
/// Base class for every renderable MJML body element. Handles attribute resolution and the
/// padding and border arithmetic that drives MJML's fixed-width table layout.
/// </summary>
internal abstract class MjmlComponent
{
    private readonly Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);

    protected MjmlComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
    {
        Node = node;
        Context = context;
        Parent = parent;
    }

    public MjmlNode Node { get; }

    public RenderContext Context { get; }

    public MjmlComponent? Parent { get; }

    public List<MjmlComponent> Children { get; } = new();

    /// <summary>Width in pixels available to this component, set by its parent before rendering.</summary>
    public double ContainerWidth { get; set; }

    /// <summary>
    /// Raw elements (mj-raw) are emitted straight into their parent without the wrapping
    /// table row a normal component receives.
    /// </summary>
    public virtual bool IsRawElement => false;

    /// <summary>Component level defaults, overridden by everything the document declares.</summary>
    protected virtual IReadOnlyDictionary<string, string> DefaultAttributes => EmptyDefaults;

    protected static readonly IReadOnlyDictionary<string, string> EmptyDefaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolves the effective attribute set. Called once, after construction.</summary>
    public void ResolveAttributes()
    {
        foreach (var pair in DefaultAttributes) _attributes[pair.Key] = pair.Value;

        foreach (var pair in Context.GlobalDefaults) _attributes[pair.Key] = pair.Value;

        if (Context.TagDefaults.TryGetValue(Node.TagName, out var tagDefaults))
        {
            foreach (var pair in tagDefaults) _attributes[pair.Key] = pair.Value;
        }

        foreach (var className in CssUtils.SplitClasses(Node.GetAttribute("mj-class")))
        {
            if (Context.ClassDefaults.TryGetValue(className, out var classDefaults))
            {
                foreach (var pair in classDefaults) _attributes[pair.Key] = pair.Value;
            }
            else
            {
                Context.Warn(Node, "mj-class \"" + className + "\" is not declared in mj-attributes.");
            }
        }

        foreach (var pair in Node.Attributes)
        {
            if (pair.Key.Equals("mj-class", StringComparison.OrdinalIgnoreCase)) continue;
            _attributes[pair.Key] = pair.Value;
        }
    }

    /// <summary>Returns an attribute value, or <c>null</c> when it is absent or empty.</summary>
    public string? Attr(string name)
        => _attributes.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>Returns an attribute value, falling back to <paramref name="fallback"/>.</summary>
    public string AttrOr(string name, string fallback) => Attr(name) ?? fallback;

    /// <summary>Returns an attribute parsed as a CSS length.</summary>
    public CssSize AttrSize(string name, double fallback = 0, string fallbackUnit = "px")
        => CssUtils.ParseSize(Attr(name), fallback, fallbackUnit);

    /// <summary>True when the attribute is present and not literally "false".</summary>
    public bool AttrFlag(string name)
    {
        var value = Attr(name);
        return value is not null && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The verbatim inner content of an ending tag, trimmed of surrounding whitespace.</summary>
    public string Content => Node.Content?.Trim() ?? string.Empty;

    /// <summary>Resolves one side of the padding shorthand.</summary>
    public string? Padding(BoxSide side)
        => CssUtils.BoxSide(Attr("padding"), Attr(PaddingName(side)), side);

    /// <summary>Resolves one side of the padding shorthand in pixels.</summary>
    public double PaddingPx(BoxSide side) => CssUtils.ParseNumber(Padding(side));

    /// <summary>Total horizontal padding in pixels.</summary>
    public double HorizontalPadding => PaddingPx(BoxSide.Left) + PaddingPx(BoxSide.Right);

    /// <summary>Total vertical padding in pixels.</summary>
    public double VerticalPadding => PaddingPx(BoxSide.Top) + PaddingPx(BoxSide.Bottom);

    /// <summary>Resolves one side of the border shorthand in pixels.</summary>
    public double BorderPx(BoxSide side)
    {
        var specific = Attr(BorderName(side));
        return CssUtils.BorderWidth(specific ?? Attr("border"));
    }

    /// <summary>Total horizontal border width in pixels.</summary>
    public double HorizontalBorder => BorderPx(BoxSide.Left) + BorderPx(BoxSide.Right);

    /// <summary>
    /// The width available to this component's children: its own width less its horizontal
    /// padding and borders.
    /// </summary>
    public double InnerWidth => Math.Max(0, ContainerWidth - HorizontalPadding - HorizontalBorder);

    /// <summary>The value used for the <c>align</c> attribute of the wrapping table cell.</summary>
    public virtual string? CellAlign => Attr("align");

    /// <summary>The value used for the <c>vertical-align</c> attribute of the wrapping table cell.</summary>
    public virtual string? CellVerticalAlign => Attr("vertical-align");

    /// <summary>Extra classes applied to the wrapping table cell.</summary>
    public virtual string? CellCssClass => Attr("css-class");

    /// <summary>Renders this component into <paramref name="writer"/>.</summary>
    public abstract void Render(HtmlWriter writer);

    /// <summary>Renders every child in order, propagating the available width.</summary>
    protected void RenderChildren(HtmlWriter writer, double childContainerWidth)
    {
        foreach (var child in Children)
        {
            child.ContainerWidth = childContainerWidth;
            child.Render(writer);
        }
    }

    /// <summary>Children that participate in layout, ignoring raw passthrough elements.</summary>
    public IReadOnlyList<MjmlComponent> LayoutChildren
    {
        get
        {
            var result = new List<MjmlComponent>();
            foreach (var child in Children)
            {
                if (!child.IsRawElement) result.Add(child);
            }

            return result;
        }
    }

    /// <summary>
    /// Emits one table row per child, which is how both columns and heroes lay out their
    /// contents. Raw children are written straight through with no row around them.
    /// </summary>
    protected static void RenderChildRows(HtmlWriter writer, IEnumerable<MjmlComponent> children, double childWidth)
    {
        foreach (var child in children)
        {
            child.ContainerWidth = childWidth;

            if (child.IsRawElement)
            {
                child.Render(writer);
                continue;
            }

            var cellStyle = new StyleBuilder()
                .Add("background", child.Attr("container-background-color"))
                .Add("font-size", "0px")
                .Add("padding", child.Attr("padding"))
                .Add("padding-top", child.Attr("padding-top"))
                .Add("padding-right", child.Attr("padding-right"))
                .Add("padding-bottom", child.Attr("padding-bottom"))
                .Add("padding-left", child.Attr("padding-left"))
                .Add("word-break", "break-word");

            var cellAttributes = new HtmlAttributes()
                .Add("align", child.CellAlign)
                .Add("vertical-align", child.CellVerticalAlign)
                .Add("class", child.CellCssClass)
                .AddStyle(cellStyle);

            writer.Open("tr", null);
            writer.Open("td", cellAttributes);
            child.Render(writer);
            writer.Close("td");
            writer.Close("tr");
        }
    }

    /// <summary>Registers this component's font family so the head can import it.</summary>
    protected void TrackFont(string attributeName = "font-family") => Context.UseFont(Attr(attributeName));

    private static string PaddingName(BoxSide side) => side switch
    {
        BoxSide.Top => "padding-top",
        BoxSide.Right => "padding-right",
        BoxSide.Bottom => "padding-bottom",
        _ => "padding-left",
    };

    private static string BorderName(BoxSide side) => side switch
    {
        BoxSide.Top => "border-top",
        BoxSide.Right => "border-right",
        BoxSide.Bottom => "border-bottom",
        _ => "border-left",
    };
}
