using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Body;

/// <summary>
/// Renders <c>mj-accordion</c>: collapsible panels built from the CSS-only checkbox toggle,
/// degrading to fully expanded content in clients that do not support it.
/// </summary>
internal sealed class AccordionComponent : MjmlComponent
{
    public AccordionComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["border"] = "2px solid black",
            ["font-family"] = "Ubuntu, Helvetica, Arial, sans-serif",
            ["icon-align"] = "middle",
            ["icon-height"] = "32px",
            ["icon-width"] = "32px",
            ["icon-position"] = "right",
            ["icon-wrapped-url"] = "https://i.imgur.com/bIXv1bk.png",
            ["icon-wrapped-alt"] = "+",
            ["icon-unwrapped-url"] = "https://i.imgur.com/w4uTygT.png",
            ["icon-unwrapped-alt"] = "-",
            ["padding"] = "10px 25px",
        };

    public override void Render(HtmlWriter writer)
    {
        Context.AddComponentStyle("mj-accordion", Styles);
        TrackFont();

        var style = new StyleBuilder()
            .Add("width", "100%")
            .Add("border-collapse", "collapse")
            .Add("border", Attr("border"))
            .Add("border-bottom", "none")
            .Add("table-layout", "fixed")
            .Add("border-spacing", "0")
            .Add("border-radius", Attr("border-radius"));

        writer.Open("table", new HtmlAttributes()
            .Add("cellspacing", "0")
            .Add("cellpadding", "0")
            .Add("class", CssUtils.JoinClasses("mj-accordion", Attr("css-class")))
            .AddStyle(style));

        writer.Open("tbody", null);

        foreach (var child in Children)
        {
            if (child is AccordionElementComponent element) element.Render(writer, this);
            else child.Render(writer);
        }

        writer.Close("tbody");
        writer.Close("table");
    }

    /// <summary>Reads an accordion-level default for a child element.</summary>
    public string? Shared(string name) => Attr(name);

    private const string Styles =
        "noinput.mj-accordion-checkbox { display:block!important; }\n" +
        "@media yahoo, only screen and (min-width:0) {\n" +
        "  .mj-accordion-element { display:block; }\n" +
        "  input.mj-accordion-checkbox, .mj-accordion-less { display:none!important; }\n" +
        "  input.mj-accordion-checkbox+* .mj-accordion-title { cursor:pointer; touch-action:manipulation; -webkit-user-select:none; -moz-user-select:none; user-select:none; }\n" +
        "  input.mj-accordion-checkbox+* .mj-accordion-content { overflow:hidden; display:none; }\n" +
        "  input.mj-accordion-checkbox+* .mj-accordion-more { display:block!important; }\n" +
        "  input.mj-accordion-checkbox:checked+* .mj-accordion-content { display:block; }\n" +
        "  input.mj-accordion-checkbox:checked+* .mj-accordion-more { display:none!important; }\n" +
        "  input.mj-accordion-checkbox:checked+* .mj-accordion-less { display:block!important; }\n" +
        "}\n" +
        ".moz-text-html input.mj-accordion-checkbox+* .mj-accordion-title { cursor:auto; touch-action:auto; -webkit-user-select:auto; -moz-user-select:auto; user-select:auto; }\n" +
        ".moz-text-html input.mj-accordion-checkbox+* .mj-accordion-content { overflow:hidden; display:block; }\n" +
        ".moz-text-html input.mj-accordion-checkbox+* .mj-accordion-ico { display:none; }";
}

/// <summary>Renders one <c>mj-accordion-element</c>: a title row plus its collapsible content.</summary>
internal sealed class AccordionElementComponent : MjmlComponent
{
    public AccordionElementComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    public override void Render(HtmlWriter writer) => Render(writer, null);

    public void Render(HtmlWriter writer, AccordionComponent? accordion)
    {
        var style = new StyleBuilder()
            .Add("background-color", Inherited("background-color", accordion))
            .Add("border-bottom", Inherited("border", accordion))
            .Add("font-family", Inherited("font-family", accordion));

        writer.Open("tr", new HtmlAttributes().Add("class", Attr("css-class")));
        writer.Open("td", new HtmlAttributes().Add("style", "padding:0px;"));

        writer.SelfClosing("input", new HtmlAttributes()
            .Add("class", "mj-accordion-checkbox")
            .Add("type", "checkbox")
            .Add("style", "display:none;"));

        writer.Open("div", new HtmlAttributes().AddStyle(style));

        foreach (var child in Children)
        {
            switch (child)
            {
                case AccordionTitleComponent title:
                    title.Render(writer, accordion, this);
                    break;
                case AccordionTextComponent text:
                    text.Render(writer, accordion, this);
                    break;
                default:
                    child.Render(writer);
                    break;
            }
        }

        writer.Close("div");
        writer.Close("td");
        writer.Close("tr");
    }

    /// <summary>Reads an attribute from this element, then from the parent accordion.</summary>
    public string? Inherited(string name, AccordionComponent? accordion) => Attr(name) ?? accordion?.Shared(name);
}

/// <summary>Renders <c>mj-accordion-title</c> with the open and close icons.</summary>
internal sealed class AccordionTitleComponent : MjmlComponent
{
    public AccordionTitleComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["font-size"] = "13px",
            ["padding"] = "16px",
        };

    public override void Render(HtmlWriter writer) => Render(writer, null, null);

    public void Render(HtmlWriter writer, AccordionComponent? accordion, AccordionElementComponent? element)
    {
        string? Inherited(string name) => Attr(name) ?? element?.Attr(name) ?? accordion?.Shared(name);

        var iconPosition = Inherited("icon-position") ?? "right";
        var iconFirst = string.Equals(iconPosition, "left", StringComparison.OrdinalIgnoreCase);

        var titleStyle = new StyleBuilder()
            .Add("width", "100%")
            .Add("border-bottom", "none");

        writer.Open("div", new HtmlAttributes().Add("class", "mj-accordion-title"));

        writer.Open("table", new HtmlAttributes()
            .Add("cellspacing", "0")
            .Add("cellpadding", "0")
            .AddStyle(titleStyle));

        writer.Open("tbody", null);
        writer.Open("tr", null);

        if (iconFirst) RenderIconCell(writer, Inherited);

        var textStyle = new StyleBuilder()
            .Add("background-color", Inherited("background-color"))
            .Add("color", Inherited("color"))
            .Add("font-size", Inherited("font-size"))
            .Add("font-family", Inherited("font-family"))
            .Add("padding", Attr("padding"))
            .Add("padding-top", Attr("padding-top"))
            .Add("padding-right", Attr("padding-right"))
            .Add("padding-bottom", Attr("padding-bottom"))
            .Add("padding-left", Attr("padding-left"));

        writer.Open("td", new HtmlAttributes().AddStyle(textStyle));
        writer.WriteRaw(Content);
        writer.Close("td");

        if (!iconFirst) RenderIconCell(writer, Inherited);

        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
        writer.Close("div");
    }

    private static void RenderIconCell(HtmlWriter writer, Func<string, string?> inherited)
    {
        var cellStyle = new StyleBuilder()
            .Add("padding", "16px")
            .Add("background", inherited("background-color"))
            .Add("vertical-align", inherited("icon-align") ?? "middle");

        writer.Open("td", new HtmlAttributes().Add("class", "mj-accordion-ico").AddStyle(cellStyle));

        writer.SelfClosing("img", new HtmlAttributes()
            .Add("src", inherited("icon-wrapped-url"))
            .Add("alt", inherited("icon-wrapped-alt"))
            .Add("class", "mj-accordion-more")
            .Add("width", CssUtils.Number(CssUtils.ParseNumber(inherited("icon-width"), 32)))
            .Add("height", CssUtils.Number(CssUtils.ParseNumber(inherited("icon-height"), 32)))
            .Add("style", "display:none;"));

        writer.SelfClosing("img", new HtmlAttributes()
            .Add("src", inherited("icon-unwrapped-url"))
            .Add("alt", inherited("icon-unwrapped-alt"))
            .Add("class", "mj-accordion-less")
            .Add("width", CssUtils.Number(CssUtils.ParseNumber(inherited("icon-width"), 32)))
            .Add("height", CssUtils.Number(CssUtils.ParseNumber(inherited("icon-height"), 32)))
            .Add("style", "display:none;"));

        writer.Close("td");
    }
}

/// <summary>Renders <c>mj-accordion-text</c>: the collapsible body of a panel.</summary>
internal sealed class AccordionTextComponent : MjmlComponent
{
    public AccordionTextComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["font-size"] = "13px",
            ["line-height"] = "1",
            ["padding"] = "16px",
        };

    public override void Render(HtmlWriter writer) => Render(writer, null, null);

    public void Render(HtmlWriter writer, AccordionComponent? accordion, AccordionElementComponent? element)
    {
        string? Inherited(string name) => Attr(name) ?? element?.Attr(name) ?? accordion?.Shared(name);

        writer.Open("div", new HtmlAttributes().Add("class", "mj-accordion-content"));

        writer.Open("table", new HtmlAttributes()
            .Add("cellspacing", "0")
            .Add("cellpadding", "0")
            .Add("style", "width:100%;"));

        writer.Open("tbody", null);
        writer.Open("tr", null);

        var cellStyle = new StyleBuilder()
            .Add("background", Inherited("background-color"))
            .Add("font-size", Inherited("font-size"))
            .Add("font-family", Inherited("font-family"))
            .Add("line-height", Attr("line-height"))
            .Add("color", Inherited("color"))
            .Add("padding", Attr("padding"))
            .Add("padding-top", Attr("padding-top"))
            .Add("padding-right", Attr("padding-right"))
            .Add("padding-bottom", Attr("padding-bottom"))
            .Add("padding-left", Attr("padding-left"));

        writer.Open("td", new HtmlAttributes().AddStyle(cellStyle));
        writer.WriteRaw(Content);
        writer.Close("td");

        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
        writer.Close("div");
    }
}
