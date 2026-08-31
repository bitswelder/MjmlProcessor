using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Body;

/// <summary>The built-in icon, colour and share URL for a known social network.</summary>
internal sealed class SocialNetwork
{
    public SocialNetwork(string backgroundColor, string iconName, string? shareUrl)
    {
        BackgroundColor = backgroundColor;
        IconName = iconName;
        ShareUrl = shareUrl;
    }

    public string BackgroundColor { get; }

    public string IconName { get; }

    /// <summary>Share URL template where <c>[[URL]]</c> is replaced by the target link.</summary>
    public string? ShareUrl { get; }

    private const string IconBase = "https://www.mailjet.com/images/theme/v1/icons/ico-social/";

    public string IconUrl => IconBase + IconName + ".png";

    /// <summary>Networks MJML ships with. The <c>-noshare</c> variants link straight to the href.</summary>
    public static readonly IReadOnlyDictionary<string, SocialNetwork> Known =
        new Dictionary<string, SocialNetwork>(StringComparer.OrdinalIgnoreCase)
        {
            ["facebook"] = new("#3b5998", "facebook", "https://www.facebook.com/sharer/sharer.php?u=[[URL]]"),
            ["facebook-noshare"] = new("#3b5998", "facebook", null),
            ["twitter"] = new("#55acee", "twitter", "https://twitter.com/intent/tweet?url=[[URL]]"),
            ["twitter-noshare"] = new("#55acee", "twitter", null),
            ["x"] = new("#000000", "twitter", "https://twitter.com/intent/tweet?url=[[URL]]"),
            ["x-noshare"] = new("#000000", "twitter", null),
            ["google"] = new("#dc4e41", "google-plus", "https://plus.google.com/share?url=[[URL]]"),
            ["google-noshare"] = new("#dc4e41", "google-plus", null),
            ["pinterest"] = new("#bd081c", "pinterest", "https://pinterest.com/pin/create/button/?url=[[URL]]&media=&description="),
            ["pinterest-noshare"] = new("#bd081c", "pinterest", null),
            ["linkedin"] = new("#0077b5", "linkedin", "https://www.linkedin.com/shareArticle?mini=true&url=[[URL]]&title=&summary=&source="),
            ["linkedin-noshare"] = new("#0077b5", "linkedin", null),
            ["instagram"] = new("#3f729b", "instagram", null),
            ["web"] = new("#4BADE9", "web", null),
            ["snapchat"] = new("#FFFA54", "snapchat", null),
            ["youtube"] = new("#EB3323", "youtube", null),
            ["tumblr"] = new("#344356", "tumblr", "https://www.tumblr.com/widgets/share/tool?canonicalUrl=[[URL]]"),
            ["tumblr-noshare"] = new("#344356", "tumblr", null),
            ["github"] = new("#000000", "github", null),
            ["vimeo"] = new("#53B4E7", "vimeo", null),
            ["medium"] = new("#000000", "medium", null),
            ["soundcloud"] = new("#EF7F31", "soundcloud", null),
            ["dribbble"] = new("#D95988", "dribbble", null),
            ["xing"] = new("#296366", "xing", "https://www.xing.com/app/user?op=share&url=[[URL]]"),
            ["xing-noshare"] = new("#296366", "xing", null),
        };
}

/// <summary>Renders <c>mj-social</c>: a row or column of social network icons.</summary>
internal sealed class SocialComponent : MjmlComponent
{
    public SocialComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "center",
            ["border-radius"] = "3px",
            ["color"] = "#333333",
            ["font-family"] = "Ubuntu, Helvetica, Arial, sans-serif",
            ["font-size"] = "13px",
            ["icon-size"] = "20px",
            ["line-height"] = "22px",
            ["mode"] = "horizontal",
            ["padding"] = "10px 25px",
            ["text-decoration"] = "none",
            ["inner-padding"] = "4px",
            ["text-padding"] = "4px 4px 4px 0",
        };

    private bool IsVertical => string.Equals(Attr("mode"), "vertical", StringComparison.OrdinalIgnoreCase);

    public override void Render(HtmlWriter writer)
    {
        TrackFont();

        if (IsVertical) RenderVertical(writer);
        else RenderHorizontal(writer);
    }

    private void RenderVertical(HtmlWriter writer)
    {
        writer.Open("table", new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .Add("style", "margin:0px;"));

        writer.Open("tbody", null);

        foreach (var child in Children)
        {
            if (child is SocialElementComponent element) element.RenderRow(writer, this);
            else child.Render(writer);
        }

        writer.Close("tbody");
        writer.Close("table");
    }

    private void RenderHorizontal(HtmlWriter writer)
    {
        writer.OutlookConditional(
            "<table align=\"" + AttrOr("align", "center") +
            "\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\"><tr>");

        foreach (var child in Children)
        {
            if (child is not SocialElementComponent element)
            {
                child.Render(writer);
                continue;
            }

            writer.OutlookConditional("<td>");

            writer.Open("table", new HtmlAttributes()
                .Add("align", AttrOr("align", "center"))
                .Add("border", "0")
                .Add("cellpadding", "0")
                .Add("cellspacing", "0")
                .Add("role", "presentation")
                .Add("style", "float:none;display:inline-table;"));

            writer.Open("tbody", null);
            element.RenderRow(writer, this);
            writer.Close("tbody");
            writer.Close("table");

            writer.OutlookConditional("</td>");
        }

        writer.OutlookConditional("</tr></table>");
    }
}

/// <summary>Renders a single <c>mj-social-element</c>: an icon and an optional label.</summary>
internal sealed class SocialElementComponent : MjmlComponent
{
    public SocialElementComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["align"] = "left",
            ["target"] = "_blank",
            ["vertical-align"] = "middle",
        };

    public override void Render(HtmlWriter writer) => RenderRow(writer, null);

    /// <summary>
    /// Renders the table row for this icon. Values not set on the element fall back to the
    /// parent <c>mj-social</c>, which is where MJML expects shared styling to live.
    /// </summary>
    public void RenderRow(HtmlWriter writer, SocialComponent? social)
    {
        var name = Attr("name");
        SocialNetwork? network = null;
        if (name is not null && !SocialNetwork.Known.TryGetValue(name, out network))
        {
            Context.Warn(Node, "Unknown social network \"" + name + "\"; falling back to a plain icon.");
        }

        var iconSize = Inherited("icon-size", social) ?? "20px";
        var iconHeight = Inherited("icon-height", social) ?? iconSize;
        var borderRadius = Inherited("border-radius", social) ?? "3px";
        var backgroundColor = Attr("background-color") ?? network?.BackgroundColor;
        var src = Attr("src") ?? network?.IconUrl;
        var href = BuildHref(network);

        var innerPadding = Inherited("inner-padding", social) ?? "4px";

        writer.Open("tr", new HtmlAttributes().Add("class", Attr("css-class")));

        var iconCellStyle = new StyleBuilder()
            .Add("padding", Attr("padding") ?? innerPadding)
            .Add("padding-top", Attr("padding-top"))
            .Add("padding-right", Attr("padding-right"))
            .Add("padding-bottom", Attr("padding-bottom"))
            .Add("padding-left", Attr("padding-left"))
            .Add("vertical-align", Inherited("vertical-align", social) ?? "middle");

        writer.Open("td", new HtmlAttributes().AddStyle(iconCellStyle));

        var iconTableStyle = new StyleBuilder()
            .Add("background", backgroundColor)
            .Add("border-radius", borderRadius)
            .Add("width", iconSize);

        writer.Open("table", new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .AddStyle(iconTableStyle));

        writer.Open("tbody", null);
        writer.Open("tr", null);

        var iconInnerStyle = new StyleBuilder()
            .Add("font-size", "0")
            .Add("height", iconHeight)
            .Add("vertical-align", "middle")
            .Add("width", iconSize);

        writer.Open("td", new HtmlAttributes().AddStyle(iconInnerStyle));

        var imageStyle = new StyleBuilder()
            .Add("border-radius", borderRadius)
            .Add("display", "block");

        var image = new HtmlAttributes()
            .Add("alt", Attr("alt") ?? string.Empty)
            .Add("height", CssUtils.Number(CssUtils.ParseNumber(iconHeight)))
            .Add("src", src)
            .Add("title", Attr("title"))
            .AddStyle(imageStyle)
            .Add("width", CssUtils.Number(CssUtils.ParseNumber(iconSize)));

        if (href is not null)
        {
            writer.Open("a", new HtmlAttributes()
                .Add("href", href)
                .Add("rel", Attr("rel"))
                .Add("target", AttrOr("target", "_blank")));
            writer.SelfClosing("img", image);
            writer.Close("a");
        }
        else
        {
            writer.SelfClosing("img", image);
        }

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
        writer.Close("td");

        if (Content.Length > 0)
        {
            var labelCellStyle = new StyleBuilder()
                .Add("vertical-align", "middle")
                .Add("padding", Inherited("text-padding", social) ?? "4px 4px 4px 0");

            writer.Open("td", new HtmlAttributes().AddStyle(labelCellStyle));

            var labelStyle = new StyleBuilder()
                .Add("color", Inherited("color", social) ?? "#333333")
                .Add("font-size", Inherited("font-size", social) ?? "13px")
                .Add("font-weight", Inherited("font-weight", social))
                .Add("font-style", Inherited("font-style", social))
                .Add("font-family", Inherited("font-family", social) ?? "Ubuntu, Helvetica, Arial, sans-serif")
                .Add("line-height", Inherited("line-height", social) ?? "22px")
                .Add("text-decoration", Inherited("text-decoration", social) ?? "none");

            // Kept on one line so no stray whitespace is underlined inside the link.
            if (href is not null)
            {
                writer.Element("a", new HtmlAttributes()
                    .Add("href", href)
                    .Add("rel", Attr("rel"))
                    .Add("target", AttrOr("target", "_blank"))
                    .AddStyle(labelStyle), Content);
            }
            else
            {
                writer.Element("span", new HtmlAttributes().AddStyle(labelStyle), Content);
            }

            writer.Close("td");
        }

        writer.Close("tr");
    }

    /// <summary>Reads an attribute from this element, then from the parent mj-social.</summary>
    private string? Inherited(string name, SocialComponent? social) => Attr(name) ?? social?.Attr(name);

    private string? BuildHref(SocialNetwork? network)
    {
        var href = Attr("href");
        if (href is null) return null;

        var template = network?.ShareUrl;
        if (template is null) return href;

        return template.Replace("[[URL]]", href);
    }
}
