using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Body;

/// <summary>Renders <c>mj-body</c>: the outermost div that carries the page background.</summary>
internal sealed class BodyComponent : MjmlComponent
{
    public const double DefaultWidth = 600;

    public BodyComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    /// <summary>The document width in pixels, defaulting to MJML's 600px.</summary>
    public double Width => AttrSize("width", DefaultWidth).Value;

    /// <summary>The page background colour, applied to both the body element and the wrapper div.</summary>
    public string? BackgroundColor => Attr("background-color");

    public override void Render(HtmlWriter writer)
    {
        var style = new StyleBuilder().Add("background-color", BackgroundColor);

        var attributes = new HtmlAttributes()
            .Add("class", Attr("css-class"))
            .AddStyle(style);

        writer.Open("div", attributes);
        RenderChildren(writer, Width);
        writer.Close("div");
    }
}

/// <summary>
/// Renders <c>mj-hero</c>: a full-bleed banner whose background image is reproduced for
/// Outlook with a VML image behind the content.
/// </summary>
internal sealed class HeroComponent : MjmlComponent
{
    public HeroComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mode"] = "fixed-height",
            ["height"] = "0px",
            ["background-color"] = "#ffffff",
            ["background-position"] = "center center",
            ["padding"] = "0px",
            ["vertical-align"] = "top",
        };

    private bool IsFluidHeight => string.Equals(Attr("mode"), "fluid-height", StringComparison.OrdinalIgnoreCase);

    public override void Render(HtmlWriter writer)
    {
        var backgroundUrl = Attr("background-url");
        var width = ContainerWidth;

        var outlookOpen =
            "<table align=\"center\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"width:" +
            CssUtils.Number(width) + "px;\" width=\"" + CssUtils.Number(width) +
            "\" ><tr><td style=\"line-height:0px;font-size:0px;mso-line-height-rule:exactly;\">";

        if (backgroundUrl is not null) outlookOpen += BuildVmlImage(backgroundUrl, width);

        writer.OutlookConditional(outlookOpen);

        var divStyle = new StyleBuilder()
            .Add("margin", "0 auto")
            .Add("max-width", CssUtils.Px(width));

        writer.Open("div", new HtmlAttributes().Add("class", Attr("css-class")).AddStyle(divStyle));

        writer.Open("table", new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .Add("style", "width:100%;"));

        writer.Open("tbody", null);
        writer.Open("tr", new HtmlAttributes().Add("style", "vertical-align:top;"));

        var cellStyle = new StyleBuilder();
        if (backgroundUrl is not null)
        {
            var position = AttrOr("background-position", "center center");
            cellStyle
                .Add("background", AttrOr("background-color", "#ffffff") + " url(" + backgroundUrl + ") no-repeat " + position + " / cover")
                .Add("background-position", position)
                .Add("background-repeat", "no-repeat")
                .Add("background-size", "cover");
        }

        cellStyle
            .Add("background-color", Attr("background-color"))
            .Add("padding", Attr("padding"))
            .Add("padding-top", Attr("padding-top"))
            .Add("padding-right", Attr("padding-right"))
            .Add("padding-bottom", Attr("padding-bottom"))
            .Add("padding-left", Attr("padding-left"))
            .Add("vertical-align", Attr("vertical-align"))
            .Add("height", IsFluidHeight ? null : ComputedHeight());

        writer.Open("td", new HtmlAttributes()
            .Add("background", backgroundUrl)
            .AddStyle(cellStyle));

        writer.Open("div", new HtmlAttributes()
            .Add("class", "mj-hero-content")
            .Add("style", "margin:0px auto;"));

        writer.Open("table", new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .Add("style", "width:100%;margin:0px;"));

        writer.Open("tbody", null);
        writer.Open("tr", null);
        writer.Open("td", new HtmlAttributes().Add("style", "font-size:0px;"));

        writer.Open("table", new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .Add("style", "width:100%;margin:0px;"));

        writer.Open("tbody", null);
        RenderChildRows(writer, Children, InnerWidth);
        writer.Close("tbody");
        writer.Close("table");

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
        writer.Close("div");

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
        writer.Close("div");

        writer.OutlookConditional((backgroundUrl is not null ? "</v:textbox></v:image>" : string.Empty) + "</td></tr></table>");
    }

    /// <summary>The hero height less its vertical padding, matching the CSS box.</summary>
    private string ComputedHeight()
    {
        var height = AttrSize("height", 0);
        var content = height.Value - VerticalPadding;
        return CssUtils.Px(Math.Max(0, content));
    }

    private string BuildVmlImage(string url, double width)
    {
        var height = CssUtils.ParseNumber(Attr("background-height"), AttrSize("height", 0).Value);
        var backgroundWidth = CssUtils.ParseNumber(Attr("background-width"), width);

        return "<v:image style=\"border:0;height:" + CssUtils.Number(height) + "px;mso-position-horizontal:center;position:absolute;top:0;width:" +
               CssUtils.Number(backgroundWidth) + "px;z-index:-3;\" src=\"" + HtmlEntities.Encode(url) +
               "\" xmlns:v=\"urn:schemas-microsoft-com:vml\">" +
               "<v:textbox style=\"mso-fit-shape-to-text:true\" inset=\"0,0,0,0\">";
    }
}
