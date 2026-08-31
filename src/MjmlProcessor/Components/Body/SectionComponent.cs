using System.Globalization;
using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Body;

/// <summary>
/// Renders <c>mj-section</c>: one horizontal band of the email. Emits the centred fixed-width
/// table, the Outlook ghost table around it, and the VML fallback for background images.
/// </summary>
internal class SectionComponent : MjmlComponent
{
    public SectionComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["background-repeat"] = "repeat",
            ["background-size"] = "auto",
            ["background-position"] = "top center",
            ["direction"] = "ltr",
            ["padding"] = "20px 0",
            ["text-align"] = "center",
        };

    /// <summary>Set by <see cref="WrapperComponent"/>, which supplies the Outlook cell itself.</summary>
    public bool SuppressOutlookWrapper { get; set; }

    public bool IsFullWidth => string.Equals(Attr("full-width"), "full-width", StringComparison.OrdinalIgnoreCase);

    protected string? BackgroundUrl => Attr("background-url");

    public override void Render(HtmlWriter writer)
    {
        if (IsFullWidth) RenderFullWidth(writer);
        else RenderFixedWidth(writer);
    }

    private void RenderFixedWidth(HtmlWriter writer)
    {
        WriteOutlookOpen(writer);

        writer.Open("div", new HtmlAttributes()
            .Add("class", Attr("css-class"))
            .AddStyle(BuildDivStyle(includeBackground: true)));

        RenderInnerTable(writer, includeBackground: true);

        writer.Close("div");

        WriteOutlookClose(writer);
    }

    private void RenderFullWidth(HtmlWriter writer)
    {
        var tableStyle = BuildBackgroundStyle();
        tableStyle.Add("width", "100%");
        tableStyle.Add("border-radius", Attr("border-radius"));

        var tableAttributes = new HtmlAttributes()
            .Add("align", "center")
            .Add("background", BackgroundUrl)
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .Add("class", Attr("css-class"))
            .AddStyle(tableStyle);

        writer.Open("table", tableAttributes);
        writer.Open("tbody", null);
        writer.Open("tr", null);
        writer.Open("td", null);

        WriteOutlookOpen(writer);

        writer.Open("div", new HtmlAttributes().AddStyle(BuildDivStyle(includeBackground: false)));
        RenderInnerTable(writer, includeBackground: false);
        writer.Close("div");

        WriteOutlookClose(writer);

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
    }

    /// <summary>The centred table that holds the section's single content cell.</summary>
    private void RenderInnerTable(HtmlWriter writer, bool includeBackground)
    {
        var tableStyle = includeBackground ? BuildBackgroundStyle() : new StyleBuilder();
        tableStyle.Add("width", "100%");
        tableStyle.Add("border-radius", Attr("border-radius"));

        var tableAttributes = new HtmlAttributes()
            .Add("align", "center")
            .Add("background", includeBackground ? BackgroundUrl : null)
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .AddStyle(tableStyle);

        var cellStyle = new StyleBuilder()
            .Add("border", Attr("border"))
            .Add("border-bottom", Attr("border-bottom"))
            .Add("border-left", Attr("border-left"))
            .Add("border-right", Attr("border-right"))
            .Add("border-top", Attr("border-top"))
            .Add("direction", AttrOr("direction", "ltr"))
            .Add("font-size", "0px")
            .Add("padding", Attr("padding"))
            .Add("padding-top", Attr("padding-top"))
            .Add("padding-right", Attr("padding-right"))
            .Add("padding-bottom", Attr("padding-bottom"))
            .Add("padding-left", Attr("padding-left"))
            .Add("text-align", Attr("text-align"));

        writer.Open("table", tableAttributes);
        writer.Open("tbody", null);
        writer.Open("tr", null);
        writer.Open("td", new HtmlAttributes().AddStyle(cellStyle));

        RenderColumns(writer);

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
    }

    /// <summary>Emits the ghost row that keeps columns side by side in Outlook.</summary>
    protected virtual void RenderColumns(HtmlWriter writer)
    {
        writer.OutlookConditional("<table role=\"presentation\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\"><tr>");

        var children = new List<MjmlComponent>(Children);
        if (string.Equals(Attr("direction"), "rtl", StringComparison.OrdinalIgnoreCase))
        {
            children.Reverse();
        }

        var childWidth = ContentWidth;
        foreach (var child in children)
        {
            child.ContainerWidth = childWidth;
            child.Render(writer);
        }

        writer.OutlookConditional("</tr></table>");
    }

    /// <summary>The width columns share: the section width less its padding and borders.</summary>
    protected double ContentWidth => Math.Max(0, ContainerWidth - HorizontalPadding - HorizontalBorder);

    private StyleBuilder BuildDivStyle(bool includeBackground)
    {
        var style = includeBackground ? BuildBackgroundStyle() : new StyleBuilder();
        style.Add("margin", "0px auto");
        style.Add("border-radius", Attr("border-radius"));
        style.Add("max-width", CssUtils.Px(ContainerWidth));
        return style;
    }

    /// <summary>Builds the background shorthand plus the longhand fallbacks clients need.</summary>
    protected StyleBuilder BuildBackgroundStyle()
    {
        var style = new StyleBuilder();
        var color = Attr("background-color");
        var url = BackgroundUrl;

        if (url is null)
        {
            style.Add("background", color);
            style.Add("background-color", color);
            return style;
        }

        var position = AttrOr("background-position", "top center");
        var size = AttrOr("background-size", "auto");
        var repeat = AttrOr("background-repeat", "repeat");

        var shorthand = (color is null ? string.Empty : color + " ") +
                        "url(" + url + ") " + position + " / " + size + " " + repeat;

        style.Add("background", shorthand);
        style.Add("background-color", color);
        style.Add("background-position", position);
        style.Add("background-repeat", repeat);
        style.Add("background-size", size);
        return style;
    }

    private void WriteOutlookOpen(HtmlWriter writer)
    {
        if (SuppressOutlookWrapper) return;

        var width = CssUtils.Number(ContainerWidth);
        var markup =
            "<table align=\"center\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"" +
            (CssUtils.SuffixClasses(Attr("css-class"), "-outlook") ?? string.Empty) +
            "\" role=\"presentation\" style=\"width:" + width + "px;\" width=\"" + width +
            "\" ><tr><td style=\"line-height:0px;font-size:0px;mso-line-height-rule:exactly;\">";

        if (BackgroundUrl is not null) markup += BuildVmlOpen();

        writer.OutlookConditional(markup);
    }

    private void WriteOutlookClose(HtmlWriter writer)
    {
        if (SuppressOutlookWrapper) return;

        var markup = BackgroundUrl is not null ? "</v:textbox></v:rect>" : string.Empty;
        writer.OutlookConditional(markup + "</td></tr></table>");
    }

    /// <summary>
    /// Outlook ignores CSS background images, so a VML rectangle stands in for them.
    /// </summary>
    private string BuildVmlOpen()
    {
        var repeat = !string.Equals(AttrOr("background-repeat", "repeat"), "no-repeat", StringComparison.OrdinalIgnoreCase);
        var size = AttrOr("background-size", "auto");
        var position = ParseBackgroundPosition();

        var vmlType = repeat ? "tile" : "frame";
        var vmlSize = string.Empty;

        if (!repeat)
        {
            if (size.Equals("cover", StringComparison.OrdinalIgnoreCase) ||
                size.Equals("contain", StringComparison.OrdinalIgnoreCase))
            {
                vmlSize = " size=\"1,1\" aspect=\"" +
                          (size.Equals("cover", StringComparison.OrdinalIgnoreCase) ? "atleast" : "atmost") + "\"";
            }
            else if (!size.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                var parts = size.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    var w = CssUtils.ParseNumber(parts[0]) / Math.Max(1, ContainerWidth);
                    var h = parts.Length > 1 ? CssUtils.ParseNumber(parts[1]) / Math.Max(1, ContainerWidth) : w;
                    vmlSize = " size=\"" + Fraction(w) + "," + Fraction(h) + "\"";
                }
            }
        }

        var origin = Fraction(position.X - 0.5) + ", " + Fraction(position.Y - 0.5);
        var vmlPosition = Fraction(position.X - 0.5) + ", " + Fraction(position.Y - 0.5);

        return "<v:rect style=\"width:" + CssUtils.Number(ContainerWidth) +
               "px;\" xmlns:v=\"urn:schemas-microsoft-com:vml\" fill=\"true\" stroke=\"false\">" +
               "<v:fill origin=\"" + origin + "\" position=\"" + vmlPosition + "\" src=\"" +
               HtmlEntities.Encode(BackgroundUrl!) + "\" color=\"" + AttrOr("background-color", "#ffffff") +
               "\" type=\"" + vmlType + "\"" + vmlSize + " />" +
               "<v:textbox style=\"mso-fit-shape-to-text:true\" inset=\"0,0,0,0\">";
    }

    private static string Fraction(double value)
        => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Reduces a CSS background-position to a pair of 0..1 fractions.</summary>
    private (double X, double Y) ParseBackgroundPosition()
    {
        var value = AttrOr("background-position", "top center");
        var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        double x = 0.5, y = 0.5;
        var sawX = false;
        var sawY = false;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "left": x = 0; sawX = true; break;
                case "right": x = 1; sawX = true; break;
                case "top": y = 0; sawY = true; break;
                case "bottom": y = 1; sawY = true; break;
                case "center":
                    if (!sawX) { x = 0.5; sawX = true; }
                    else { y = 0.5; sawY = true; }

                    break;
                default:
                {
                    var size = CssUtils.ParseSize(part);
                    var fraction = size.IsPercent
                        ? size.Value / 100.0
                        : ContainerWidth > 0 ? size.Value / ContainerWidth : 0;

                    if (!sawX) { x = fraction; sawX = true; }
                    else { y = fraction; sawY = true; }

                    break;
                }
            }
        }

        // "top center" and "center top" both mean the same thing; a lone keyword centres the other axis.
        if (!sawY) y = 0.5;
        if (!sawX) x = 0.5;

        return (x, y);
    }
}

/// <summary>
/// Renders <c>mj-wrapper</c>: a section that contains other sections, letting a shared
/// background and padding span several bands.
/// </summary>
internal sealed class WrapperComponent : SectionComponent
{
    public WrapperComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    public override void Render(HtmlWriter writer)
    {
        // Child sections must not emit their own ghost table; the wrapper supplies one cell each.
        foreach (var child in Children)
        {
            if (child is SectionComponent section) section.SuppressOutlookWrapper = true;
        }

        base.Render(writer);
    }

    /// <summary>
    /// A wrapper stacks its children vertically, so each one gets its own ghost row rather
    /// than sharing a single row of cells the way a section's columns do.
    /// </summary>
    protected override void RenderColumns(HtmlWriter writer)
    {
        var childWidth = ContentWidth;
        var cell = "<td class=\"\" width=\"" + CssUtils.Px(childWidth) + "\" >";

        writer.OutlookConditional(
            "<table role=\"presentation\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\"><tr>" + cell);

        for (var i = 0; i < Children.Count; i++)
        {
            if (i > 0) writer.OutlookConditional("</td></tr><tr>" + cell);

            Children[i].ContainerWidth = childWidth;
            Children[i].Render(writer);
        }

        writer.OutlookConditional("</td></tr></table>");
    }
}
