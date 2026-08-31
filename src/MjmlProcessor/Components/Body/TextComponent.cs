using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Body;

/// <summary>Renders <c>mj-text</c> as a styled div wrapping the author's HTML.</summary>
internal sealed class TextComponent : MjmlComponent
{
    public TextComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "left",
            ["color"] = "#000000",
            ["font-family"] = "Ubuntu, Helvetica, Arial, sans-serif",
            ["font-size"] = "13px",
            ["line-height"] = "1",
            ["padding"] = "10px 25px",
        };

    public override void Render(HtmlWriter writer)
    {
        TrackFont();

        var style = new StyleBuilder()
            .Add("font-family", Attr("font-family"))
            .Add("font-size", Attr("font-size"))
            .Add("font-style", Attr("font-style"))
            .Add("font-weight", Attr("font-weight"))
            .Add("letter-spacing", Attr("letter-spacing"))
            .Add("line-height", Attr("line-height"))
            .Add("text-align", Attr("align"))
            .Add("text-decoration", Attr("text-decoration"))
            .Add("text-transform", Attr("text-transform"))
            .Add("color", Attr("color"))
            .Add("height", Attr("height"));

        var attributes = new HtmlAttributes().AddStyle(style);

        if (Content.Length == 0)
        {
            writer.Element("div", attributes, string.Empty);
            return;
        }

        writer.Open("div", attributes);
        writer.WriteRaw(Content);
        writer.Close("div");
    }
}

/// <summary>Renders <c>mj-button</c> as a bulletproof single cell table with an anchor inside.</summary>
internal sealed class ButtonComponent : MjmlComponent
{
    public ButtonComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "center",
            ["background-color"] = "#414141",
            ["border"] = "none",
            ["border-radius"] = "3px",
            ["color"] = "#ffffff",
            ["font-family"] = "Ubuntu, Helvetica, Arial, sans-serif",
            ["font-size"] = "13px",
            ["font-weight"] = "normal",
            ["inner-padding"] = "10px 25px",
            ["line-height"] = "120%",
            ["padding"] = "10px 25px",
            ["target"] = "_blank",
            ["text-decoration"] = "none",
            ["text-transform"] = "none",
            ["vertical-align"] = "middle",
        };

    public override void Render(HtmlWriter writer)
    {
        TrackFont();

        var backgroundColor = Attr("background-color");
        var innerPadding = Attr("inner-padding");
        var borderRadius = Attr("border-radius");
        var width = Attr("width");

        var tableStyle = new StyleBuilder()
            .Add("border-collapse", "separate")
            .Add("width", width)
            .Add("line-height", "100%");

        var tableAttributes = new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .AddStyle(tableStyle);

        var cellStyle = new StyleBuilder()
            .Add("border", Attr("border"))
            .Add("border-bottom", Attr("border-bottom"))
            .Add("border-left", Attr("border-left"))
            .Add("border-radius", borderRadius)
            .Add("border-right", Attr("border-right"))
            .Add("border-top", Attr("border-top"))
            .Add("cursor", "auto")
            .Add("font-style", Attr("font-style"))
            .Add("height", Attr("height"))
            .Add("mso-padding-alt", innerPadding)
            .Add("text-align", Attr("text-align"))
            .Add("background", backgroundColor);

        var cellAttributes = new HtmlAttributes()
            .Add("align", "center")
            .Add("bgcolor", string.Equals(backgroundColor, "none", StringComparison.OrdinalIgnoreCase) ? null : backgroundColor)
            .Add("role", "presentation")
            .AddStyle(cellStyle)
            .Add("valign", Attr("vertical-align"));

        var isLink = Attr("href") is not null;
        var contentTag = isLink ? "a" : "p";

        var contentStyle = new StyleBuilder()
            .Add("display", "inline-block")
            .Add("width", CalculateContentWidth())
            .Add("background", backgroundColor)
            .Add("color", Attr("color"))
            .Add("font-family", Attr("font-family"))
            .Add("font-size", Attr("font-size"))
            .Add("font-style", Attr("font-style"))
            .Add("font-weight", Attr("font-weight"))
            .Add("line-height", Attr("line-height"))
            .Add("letter-spacing", Attr("letter-spacing"))
            .Add("margin", "0")
            .Add("text-decoration", Attr("text-decoration"))
            .Add("text-transform", Attr("text-transform"))
            .Add("padding", innerPadding)
            .Add("mso-padding-alt", "0px")
            .Add("border-radius", borderRadius);

        var contentAttributes = new HtmlAttributes();
        if (isLink)
        {
            contentAttributes
                .Add("href", Attr("href"))
                .Add("rel", Attr("rel"))
                .Add("name", Attr("name"))
                .Add("target", Attr("target"));
        }

        contentAttributes.AddStyle(contentStyle);

        writer.Open("table", tableAttributes);
        writer.Open("tbody", null);
        writer.Open("tr", null);
        writer.Open("td", cellAttributes);

        // The label stays on one line: whitespace inside the anchor would show up as a gap
        // in the rendered button, and would be underlined when text-decoration is set.
        writer.Element(contentTag, contentAttributes, Content);

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
    }

    /// <summary>A percentage width has to be resolved against the button's own content box.</summary>
    private string? CalculateContentWidth()
    {
        var width = Attr("width");
        if (width is null) return null;

        var size = CssUtils.ParseSize(width);
        if (size.IsPercent) return null;

        var horizontalInnerPadding =
            CssUtils.ParseNumber(CssUtils.BoxSide(Attr("inner-padding"), null, BoxSide.Left)) +
            CssUtils.ParseNumber(CssUtils.BoxSide(Attr("inner-padding"), null, BoxSide.Right));

        var borders = HorizontalBorder;
        var content = size.Value - horizontalInnerPadding - borders;
        return content > 0 ? CssUtils.Px(content) : null;
    }
}

/// <summary>Renders <c>mj-image</c>, including the Outlook safe fixed width table.</summary>
internal sealed class ImageComponent : MjmlComponent
{
    public ImageComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "center",
            ["border"] = "0",
            ["height"] = "auto",
            ["padding"] = "10px 25px",
            ["target"] = "_blank",
            ["font-size"] = "13px",
        };

    public override void Render(HtmlWriter writer)
    {
        var isFluid = AttrFlag("fluid-on-mobile");
        var width = ResolveWidth();

        var tableStyle = new StyleBuilder()
            .Add("border-collapse", "collapse")
            .Add("border-spacing", "0px");

        var tableAttributes = new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .AddStyle(tableStyle)
            .Add("class", isFluid ? "mj-full-width-mobile" : null);

        var cellAttributes = new HtmlAttributes()
            .Add("style", "width:" + CssUtils.Px(width) + ";")
            .Add("class", isFluid ? "mj-full-width-mobile" : null);

        var imageStyle = new StyleBuilder()
            .Add("border", Attr("border"))
            .Add("border-top", Attr("border-top"))
            .Add("border-right", Attr("border-right"))
            .Add("border-bottom", Attr("border-bottom"))
            .Add("border-left", Attr("border-left"))
            .Add("border-radius", Attr("border-radius"))
            .Add("display", "block")
            .Add("outline", "none")
            .Add("text-decoration", "none")
            .Add("height", Attr("height"))
            .Add("max-height", Attr("max-height"))
            .Add("width", "100%")
            .Add("font-size", Attr("font-size"));

        var height = Attr("height");
        var imageAttributes = new HtmlAttributes()
            .AddAllowingEmpty("alt", Attr("alt"))
            .Add("height", string.Equals(height, "auto", StringComparison.OrdinalIgnoreCase)
                ? "auto"
                : height is null ? "auto" : CssUtils.Number(CssUtils.ParseNumber(height)))
            .Add("src", Attr("src"))
            .Add("srcset", Attr("srcset"))
            .Add("sizes", Attr("sizes"))
            .Add("title", Attr("title"))
            .AddStyle(imageStyle)
            .Add("width", CssUtils.Number(width))
            .Add("usemap", Attr("usemap"));

        if (Attr("src") is null)
        {
            Context.Warn(Node, "mj-image requires a src attribute.");
        }

        writer.Open("table", tableAttributes);
        writer.Open("tbody", null);
        writer.Open("tr", null);
        writer.Open("td", cellAttributes);

        var href = Attr("href");
        if (href is not null)
        {
            var linkAttributes = new HtmlAttributes()
                .Add("href", href)
                .Add("target", Attr("target"))
                .Add("rel", Attr("rel"))
                .Add("name", Attr("name"));

            writer.Open("a", linkAttributes);
            writer.SelfClosing("img", imageAttributes);
            writer.Close("a");
        }
        else
        {
            writer.SelfClosing("img", imageAttributes);
        }

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
    }

    /// <summary>
    /// The image never grows past its own content box: the column width less this image's
    /// padding and border.
    /// </summary>
    private double ResolveWidth()
    {
        var available = InnerWidth;
        var declared = Attr("width");
        if (declared is null) return available;

        var size = CssUtils.ParseSize(declared);
        var value = size.IsPercent ? available * size.Value / 100.0 : size.Value;
        return Math.Min(value, available);
    }
}

/// <summary>Renders <c>mj-divider</c> as a bordered paragraph, with an Outlook fallback table.</summary>
internal sealed class DividerComponent : MjmlComponent
{
    public DividerComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "center",
            ["border-color"] = "#000000",
            ["border-style"] = "solid",
            ["border-width"] = "4px",
            ["padding"] = "10px 25px",
            ["width"] = "100%",
        };

    public override void Render(HtmlWriter writer)
    {
        var borderTop = AttrOr("border-style", "solid") + " " +
                        AttrOr("border-width", "4px") + " " +
                        AttrOr("border-color", "#000000");

        var style = new StyleBuilder()
            .Add("border-top", borderTop)
            .Add("font-size", "1px")
            .Add("margin", "0px auto")
            .Add("width", Attr("width"));

        writer.Element("p", new HtmlAttributes().AddStyle(style), string.Empty);

        var outlookWidth = OutlookWidth();
        var outlookStyle = new StyleBuilder()
            .Add("border-top", borderTop)
            .Add("font-size", "1px")
            .Add("margin", "0px auto")
            .Add("width", CssUtils.Px(outlookWidth));

        writer.OutlookConditional(
            "<table align=\"center\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" style=\"" +
            outlookStyle.Build() + "\" role=\"presentation\" width=\"" + CssUtils.Px(outlookWidth) +
            "\" ><tr><td style=\"height:0;line-height:0;\"> &nbsp;</td></tr></table>");
    }

    private double OutlookWidth()
    {
        var size = AttrSize("width", 100, "%");
        var available = Math.Max(0, ContainerWidth - HorizontalPadding);
        return size.IsPercent ? available * size.Value / 100.0 : Math.Min(size.Value, available);
    }
}

/// <summary>Renders <c>mj-spacer</c> as a fixed height div.</summary>
internal sealed class SpacerComponent : MjmlComponent
{
    public SpacerComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["height"] = "20px",
        };

    public override void Render(HtmlWriter writer)
    {
        var height = AttrOr("height", "20px");
        var style = new StyleBuilder()
            .Add("height", height)
            .Add("line-height", height);

        // A hair space keeps the div from collapsing in clients that ignore empty blocks.
        writer.Element("div", new HtmlAttributes().AddStyle(style), "&#8202;");
    }
}

/// <summary>Renders <c>mj-table</c>, passing the author's rows through untouched.</summary>
internal sealed class TableComponent : MjmlComponent
{
    public TableComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "left",
            ["border"] = "none",
            ["cellpadding"] = "0",
            ["cellspacing"] = "0",
            ["color"] = "#000000",
            ["font-family"] = "Ubuntu, Helvetica, Arial, sans-serif",
            ["font-size"] = "13px",
            ["line-height"] = "22px",
            ["padding"] = "10px 25px",
            ["table-layout"] = "auto",
            ["width"] = "100%",
        };

    public override void Render(HtmlWriter writer)
    {
        TrackFont();

        var style = new StyleBuilder()
            .Add("color", Attr("color"))
            .Add("font-family", Attr("font-family"))
            .Add("font-size", Attr("font-size"))
            .Add("line-height", Attr("line-height"))
            .Add("table-layout", Attr("table-layout"))
            .Add("width", Attr("width"))
            .Add("border", Attr("border"));

        var attributes = new HtmlAttributes()
            .Add("cellpadding", Attr("cellpadding"))
            .Add("cellspacing", Attr("cellspacing"))
            .Add("width", ResolveWidth())
            .Add("border", "0")
            .AddStyle(style);

        writer.Open("table", attributes);
        writer.WriteRaw(Content);
        writer.Close("table");
    }

    private string ResolveWidth()
    {
        var size = AttrSize("width", 100, "%");
        return size.IsPercent ? CssUtils.Number(size.Value) + "%" : CssUtils.Number(size.Value);
    }
}

/// <summary>Renders <c>mj-raw</c>, emitting the author's markup with no wrapping at all.</summary>
internal sealed class RawComponent : MjmlComponent
{
    public RawComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    public override bool IsRawElement => true;

    public override void Render(HtmlWriter writer) => writer.WriteRaw(Content);
}
