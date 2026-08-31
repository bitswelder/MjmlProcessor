using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Body;

/// <summary>
/// Renders <c>mj-navbar</c>: a row of inline links with an optional CSS-only hamburger menu
/// for mobile clients that support the checkbox toggle trick.
/// </summary>
internal sealed class NavbarComponent : MjmlComponent
{
    public NavbarComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "center",
            ["base-url"] = "",
            ["hamburger"] = "",
            ["ico-align"] = "center",
            ["ico-open"] = "&#9776;",
            ["ico-close"] = "&#8855;",
            ["ico-color"] = "#000000",
            ["ico-font-size"] = "30px",
            ["ico-font-family"] = "Ubuntu, Helvetica, Arial, sans-serif",
            ["ico-text-transform"] = "uppercase",
            ["ico-padding"] = "10px",
            ["ico-text-decoration"] = "none",
            ["ico-line-height"] = "30px",
        };

    private bool HasHamburger => string.Equals(Attr("hamburger"), "hamburger", StringComparison.OrdinalIgnoreCase);

    public override void Render(HtmlWriter writer)
    {
        Context.AddComponentStyle("mj-navbar", BuildStyles(Context.Breakpoint));

        if (HasHamburger) RenderHamburger(writer);

        writer.OutlookConditional("<table role=\"presentation\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\"><tr><td style=\"vertical-align:top;\" >");

        writer.Open("div", new HtmlAttributes()
            .Add("class", CssUtils.JoinClasses("mj-inline-links", Attr("css-class"))));

        foreach (var child in Children)
        {
            if (child is NavbarLinkComponent link) link.Render(writer, this);
            else child.Render(writer);
        }

        writer.Close("div");

        writer.OutlookConditional("</td></tr></table>");
    }

    /// <summary>The label and checkbox that drive the mobile menu.</summary>
    private void RenderHamburger(HtmlWriter writer)
    {
        writer.WriteLine("<!--[if !mso><!-->");

        writer.SelfClosing("input", new HtmlAttributes()
            .Add("type", "checkbox")
            .Add("id", "mj-menu-toggle")
            .Add("class", "mj-menu-checkbox")
            .Add("style", "display:none !important; max-height:0; visibility:hidden;"));

        var labelStyle = new StyleBuilder()
            .Add("display", "block")
            .Add("cursor", "pointer")
            .Add("mso-hide", "all")
            .Add("-moz-user-select", "none")
            .Add("user-select", "none")
            .Add("padding", Attr("ico-padding"));

        writer.Open("label", new HtmlAttributes()
            .Add("for", "mj-menu-toggle")
            .Add("class", "mj-menu-label")
            .Add("align", Attr("ico-align"))
            .AddStyle(labelStyle));

        var iconStyle = new StyleBuilder()
            .Add("font-family", Attr("ico-font-family"))
            .Add("font-size", Attr("ico-font-size"))
            .Add("line-height", Attr("ico-line-height"))
            .Add("text-transform", Attr("ico-text-transform"))
            .Add("text-decoration", Attr("ico-text-decoration"))
            .Add("color", Attr("ico-color"));

        writer.Element("span", new HtmlAttributes().Add("class", "mj-menu-icon-open").AddStyle(iconStyle), Attr("ico-open"));
        writer.Element("span", new HtmlAttributes().Add("class", "mj-menu-icon-close").AddStyle(iconStyle), Attr("ico-close"));

        writer.Close("label");
        writer.WriteLine("<!--<![endif]-->");
    }

    /// <summary>Prefix applied to relative link hrefs.</summary>
    public string BaseUrl => AttrOr("base-url", string.Empty);

    private static string BuildStyles(double breakpoint)
    {
        var max = CssUtils.Number(breakpoint - 1) + "px";

        return
            "noinput.mj-menu-checkbox { display:block!important; max-height:none!important; visibility:visible!important; }\n" +
            "@media only screen and (max-width:" + max + ") {\n" +
            "  .mj-menu-checkbox[type=\"checkbox\"] ~ .mj-inline-links { display:none!important; }\n" +
            "  .mj-menu-checkbox[type=\"checkbox\"]:checked ~ .mj-inline-links,\n" +
            "  .mj-menu-checkbox[type=\"checkbox\"] ~ .mj-menu-trigger { display:block!important; max-width:none!important; max-height:none!important; font-size:inherit!important; }\n" +
            "  .mj-menu-checkbox[type=\"checkbox\"] ~ .mj-inline-links > a { display:block!important; }\n" +
            "  .mj-menu-checkbox[type=\"checkbox\"]:checked ~ .mj-menu-trigger .mj-menu-icon-close { display:block!important; }\n" +
            "  .mj-menu-checkbox[type=\"checkbox\"]:checked ~ .mj-menu-trigger .mj-menu-icon-open { display:none!important; }\n" +
            "}";
    }
}

/// <summary>Renders a single <c>mj-navbar-link</c>.</summary>
internal sealed class NavbarLinkComponent : MjmlComponent
{
    public NavbarLinkComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["color"] = "#000000",
            ["font-family"] = "Ubuntu, Helvetica, Arial, sans-serif",
            ["font-size"] = "13px",
            ["font-weight"] = "normal",
            ["line-height"] = "22px",
            ["padding"] = "15px 10px",
            ["target"] = "_blank",
            ["text-decoration"] = "none",
            ["text-transform"] = "uppercase",
        };

    public override void Render(HtmlWriter writer) => Render(writer, null);

    public void Render(HtmlWriter writer, NavbarComponent? navbar)
    {
        TrackFont();

        var style = new StyleBuilder()
            .Add("display", "inline-block")
            .Add("color", Attr("color"))
            .Add("font-family", Attr("font-family"))
            .Add("font-size", Attr("font-size"))
            .Add("font-style", Attr("font-style"))
            .Add("font-weight", Attr("font-weight"))
            .Add("letter-spacing", Attr("letter-spacing"))
            .Add("line-height", Attr("line-height"))
            .Add("text-decoration", Attr("text-decoration"))
            .Add("text-transform", Attr("text-transform"))
            .Add("padding", Attr("padding"))
            .Add("padding-top", Attr("padding-top"))
            .Add("padding-right", Attr("padding-right"))
            .Add("padding-bottom", Attr("padding-bottom"))
            .Add("padding-left", Attr("padding-left"));

        var href = Attr("href");
        if (href is not null && navbar is not null && navbar.BaseUrl.Length > 0)
        {
            href = navbar.BaseUrl + href;
        }

        var attributes = new HtmlAttributes()
            .Add("class", CssUtils.JoinClasses("mj-link", Attr("css-class")))
            .Add("href", href)
            .Add("rel", Attr("rel"))
            .Add("target", Attr("target"))
            .Add("name", Attr("name"))
            .AddStyle(style);

        // Kept on one line so no stray whitespace is underlined inside the link.
        writer.Element("a", attributes, Content);
    }
}
