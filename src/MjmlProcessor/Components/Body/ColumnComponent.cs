using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components.Body;

/// <summary>
/// Base for the two elements a section can hold directly. Both render as an inline-block div
/// preceded by an Outlook-only table cell that pins their width.
/// </summary>
internal abstract class ColumnLikeComponent : MjmlComponent
{
    protected ColumnLikeComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    /// <summary>The declared width, defaulting to an equal share of the parent section.</summary>
    public CssSize ParsedWidth
    {
        get
        {
            var declared = Attr("width");
            if (declared is not null) return CssUtils.ParseSize(declared);

            var siblings = Parent?.LayoutChildren.Count ?? 1;
            if (siblings < 1) siblings = 1;
            return new CssSize(100.0 / siblings, "%");
        }
    }

    /// <summary>The resolved width in pixels, used for the Outlook fallback cell.</summary>
    public double PixelWidth
    {
        get
        {
            var width = ParsedWidth;
            return width.IsPercent ? ContainerWidth * width.Value / 100.0 : width.Value;
        }
    }

    /// <summary>The generated class name that carries this column's desktop width.</summary>
    protected string ColumnClass
    {
        get
        {
            var width = ParsedWidth;
            var className = width.IsPercent
                ? "mj-column-per-" + CssUtils.Number(Math.Truncate(width.Value))
                : "mj-column-px-" + CssUtils.Number(Math.Truncate(width.Value));

            Context.AddMediaQuery(className, width);
            return className;
        }
    }

    /// <summary>Columns keep their width on mobile only when they sit inside an mj-group.</summary>
    protected bool KeepsWidthOnMobile => Parent is GroupComponent;

    /// <summary>Emits the Outlook-only table cell that surrounds this element inside a section.</summary>
    protected void WriteOutlookCellStart(HtmlWriter writer)
    {
        var style = new StyleBuilder()
            .Add("vertical-align", AttrOr("vertical-align", "top"))
            .Add("width", CssUtils.Px(PixelWidth));

        writer.OutlookConditional(
            "<td class=\"" + (CssUtils.SuffixClasses(Attr("css-class"), "-outlook") ?? string.Empty) +
            "\" style=\"" + style.Build() + "\" >");
    }

    /// <summary>Closes the Outlook-only table cell.</summary>
    protected void WriteOutlookCellEnd(HtmlWriter writer) => writer.OutlookConditional("</td>");
}

/// <summary>Renders <c>mj-column</c>: an inline-block div holding one table row per child.</summary>
internal sealed class ColumnComponent : ColumnLikeComponent
{
    public ColumnComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["direction"] = "ltr",
            ["vertical-align"] = "top",
        };

    /// <summary>Padding on a column needs an extra table so it does not collapse in Outlook.</summary>
    private bool HasGutter =>
        Attr("padding") is not null ||
        Attr("padding-top") is not null ||
        Attr("padding-right") is not null ||
        Attr("padding-bottom") is not null ||
        Attr("padding-left") is not null;

    public override void Render(HtmlWriter writer)
    {
        WriteOutlookCellStart(writer);

        var divStyle = new StyleBuilder()
            .Add("font-size", "0px")
            .Add("text-align", "left")
            .Add("direction", AttrOr("direction", "ltr"))
            .Add("display", "inline-block")
            .Add("vertical-align", AttrOr("vertical-align", "top"))
            .Add("width", MobileWidth());

        var divAttributes = new HtmlAttributes()
            .Add("class", CssUtils.JoinClasses(ColumnClass, "mj-outlook-group-fix", Attr("css-class")))
            .AddStyle(divStyle);

        writer.Open("div", divAttributes);

        if (HasGutter) RenderGutter(writer);
        else RenderContentTable(writer);

        writer.Close("div");

        WriteOutlookCellEnd(writer);
    }

    /// <summary>Wraps the content table in a padded cell.</summary>
    private void RenderGutter(HtmlWriter writer)
    {
        var gutterStyle = new StyleBuilder()
            .Add("background-color", Attr("background-color"))
            .Add("border", Attr("border"))
            .Add("border-bottom", Attr("border-bottom"))
            .Add("border-left", Attr("border-left"))
            .Add("border-right", Attr("border-right"))
            .Add("border-top", Attr("border-top"))
            .Add("border-radius", Attr("border-radius"))
            .Add("vertical-align", AttrOr("vertical-align", "top"))
            .Add("padding", Attr("padding"))
            .Add("padding-top", Attr("padding-top"))
            .Add("padding-right", Attr("padding-right"))
            .Add("padding-bottom", Attr("padding-bottom"))
            .Add("padding-left", Attr("padding-left"));

        var tableAttributes = new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .Add("width", "100%");

        writer.Open("table", tableAttributes);
        writer.Open("tbody", null);
        writer.Open("tr", null);
        writer.Open("td", new HtmlAttributes().AddStyle(gutterStyle));

        RenderContentTable(writer, includeDecoration: false);

        writer.Close("td");
        writer.Close("tr");
        writer.Close("tbody");
        writer.Close("table");
    }

    /// <summary>Renders the table that holds one row per child component.</summary>
    private void RenderContentTable(HtmlWriter writer, bool includeDecoration = true)
    {
        var tableStyle = new StyleBuilder();
        if (includeDecoration)
        {
            tableStyle
                .Add("background-color", Attr("background-color"))
                .Add("border", Attr("border"))
                .Add("border-bottom", Attr("border-bottom"))
                .Add("border-left", Attr("border-left"))
                .Add("border-right", Attr("border-right"))
                .Add("border-top", Attr("border-top"))
                .Add("border-radius", Attr("border-radius"));
        }

        tableStyle.Add("vertical-align", AttrOr("vertical-align", "top"));

        var tableAttributes = new HtmlAttributes()
            .Add("border", "0")
            .Add("cellpadding", "0")
            .Add("cellspacing", "0")
            .Add("role", "presentation")
            .AddStyle(tableStyle)
            .Add("width", "100%");

        writer.Open("table", tableAttributes);
        writer.Open("tbody", null);

        RenderChildRows(writer, Children, ChildContainerWidth());

        writer.Close("tbody");
        writer.Close("table");
    }

    /// <summary>The width children can occupy: this column less its own padding and borders.</summary>
    private double ChildContainerWidth()
        => Math.Max(0, PixelWidth - HorizontalPadding - HorizontalBorder);

    /// <summary>Columns stack to full width on mobile unless they sit inside an mj-group.</summary>
    private string MobileWidth()
    {
        if (!KeepsWidthOnMobile) return "100%";

        var width = ParsedWidth;
        return width.IsPercent ? CssUtils.Number(width.Value) + "%" : CssUtils.Px(width.Value);
    }
}

/// <summary>
/// Renders <c>mj-group</c>: columns that stay side by side on mobile instead of stacking.
/// </summary>
internal sealed class GroupComponent : ColumnLikeComponent
{
    public GroupComponent(MjmlNode node, RenderContext context, MjmlComponent? parent)
        : base(node, context, parent) { }

    protected override IReadOnlyDictionary<string, string> DefaultAttributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["direction"] = "ltr",
            ["vertical-align"] = "top",
        };

    public override void Render(HtmlWriter writer)
    {
        WriteOutlookCellStart(writer);

        var divStyle = new StyleBuilder()
            .Add("font-size", "0")
            .Add("line-height", "0")
            .Add("text-align", "left")
            .Add("display", "inline-block")
            .Add("width", "100%")
            .Add("direction", AttrOr("direction", "ltr"))
            .Add("vertical-align", AttrOr("vertical-align", "top"))
            .Add("background-color", Attr("background-color"));

        var divAttributes = new HtmlAttributes()
            .Add("class", CssUtils.JoinClasses(ColumnClass, "mj-outlook-group-fix", Attr("css-class")))
            .AddStyle(divStyle);

        writer.Open("div", divAttributes);
        writer.OutlookConditional("<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\"><tr>");

        // Columns inside a group divide the group's own width, not the section's.
        RenderChildren(writer, PixelWidth);

        writer.OutlookConditional("</tr></table>");
        writer.Close("div");

        WriteOutlookCellEnd(writer);
    }
}
