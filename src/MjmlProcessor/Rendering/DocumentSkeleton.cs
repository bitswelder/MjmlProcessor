using System.Text;
using MjmlProcessor.Internal;
using MjmlProcessor.Parsing;

namespace MjmlProcessor.Rendering;

/// <summary>
/// Wraps the rendered body markup in the boilerplate email clients need: the resets, the
/// Outlook conditional blocks, the responsive media queries and the font imports.
/// </summary>
internal static class DocumentSkeleton
{
    public static string Build(string bodyMarkup, RenderContext context, string? backgroundColor)
    {
        var builder = new StringBuilder();

        builder.Append("<!doctype html>\n");
        builder.Append("<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        builder.Append(" lang=\"").Append(HtmlEntities.Encode(context.Options.Language)).Append('"');
        builder.Append(" dir=\"").Append(HtmlEntities.Encode(context.Options.Direction)).Append("\">\n");

        AppendHead(builder, context);

        var bodyStyle = new StyleBuilder()
            .Add("word-spacing", "normal")
            .Add("background-color", backgroundColor);

        builder.Append("<body style=\"").Append(bodyStyle.Build()).Append("\">\n");

        AppendPreview(builder, context);

        builder.Append(bodyMarkup);

        builder.Append("</body>\n</html>\n");

        return builder.ToString();
    }

    private static void AppendHead(StringBuilder builder, RenderContext context)
    {
        builder.Append("<head>\n");
        builder.Append("  <title>").Append(HtmlEntities.Encode(context.Title ?? string.Empty)).Append("</title>\n");
        builder.Append("  <!--[if !mso]><!-->\n");
        builder.Append("  <meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\n");
        builder.Append("  <!--<![endif]-->\n");
        builder.Append("  <meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\">\n");
        builder.Append("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");

        AppendResetStyles(builder);
        AppendOutlookBlocks(builder);
        AppendFonts(builder, context);
        AppendMediaQueries(builder, context);
        AppendComponentStyles(builder, context);
        AppendCustomStyles(builder, context);

        foreach (var raw in context.HeadRawMarkup)
        {
            builder.Append("  ").Append(raw).Append('\n');
        }

        builder.Append("</head>\n");
    }

    private static void AppendResetStyles(StringBuilder builder)
    {
        builder.Append("  <style type=\"text/css\">\n");
        builder.Append("    #outlook a { padding: 0; }\n");
        builder.Append("    body { margin: 0; padding: 0; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }\n");
        builder.Append("    table, td { border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }\n");
        builder.Append("    img { border: 0; height: auto; line-height: 100%; outline: none; text-decoration: none; -ms-interpolation-mode: bicubic; }\n");
        builder.Append("    p { display: block; margin: 13px 0; }\n");
        builder.Append("  </style>\n");
    }

    private static void AppendOutlookBlocks(StringBuilder builder)
    {
        builder.Append("  <!--[if mso]>\n");
        builder.Append("  <noscript><xml><o:OfficeDocumentSettings><o:AllowPNG/><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript>\n");
        builder.Append("  <![endif]-->\n");
        builder.Append("  <!--[if lte mso 11]>\n");
        builder.Append("  <style type=\"text/css\">.mj-outlook-group-fix { width:100% !important; }</style>\n");
        builder.Append("  <![endif]-->\n");
    }

    private static void AppendFonts(StringBuilder builder, RenderContext context)
    {
        var imports = new List<string>(context.ResolveFontImports());
        if (imports.Count == 0) return;

        builder.Append("  <!--[if !mso]><!-->\n");
        foreach (var href in imports)
        {
            builder.Append("  <link href=\"").Append(HtmlEntities.Encode(href))
                .Append("\" rel=\"stylesheet\" type=\"text/css\">\n");
        }

        builder.Append("  <style type=\"text/css\">\n");
        foreach (var href in imports)
        {
            builder.Append("    @import url(").Append(href).Append(");\n");
        }

        builder.Append("  </style>\n");
        builder.Append("  <!--<![endif]-->\n");
    }

    /// <summary>
    /// Emits the desktop width rules. The rules are duplicated for Thunderbird, which needs
    /// them outside an <c>@media</c> block, and the mobile helper for fluid images.
    /// </summary>
    private static void AppendMediaQueries(StringBuilder builder, RenderContext context)
    {
        var breakpoint = CssUtils.Number(context.Breakpoint) + "px";

        if (context.MediaQueries.Count > 0)
        {
            builder.Append("  <style type=\"text/css\">\n");
            builder.Append("    @media only screen and (min-width:").Append(breakpoint).Append(") {\n");
            foreach (var query in context.MediaQueries)
            {
                builder.Append("      .").Append(query.Key).Append(' ').Append(query.Value).Append('\n');
            }

            builder.Append("    }\n");
            builder.Append("  </style>\n");

            builder.Append("  <style media=\"screen and (min-width:").Append(breakpoint).Append(")\">\n");
            foreach (var query in context.MediaQueries)
            {
                builder.Append("    .moz-text-html .").Append(query.Key).Append(' ').Append(query.Value).Append('\n');
            }

            builder.Append("  </style>\n");
        }

        var mobileMax = CssUtils.Number(context.Breakpoint - 1) + "px";
        builder.Append("  <style type=\"text/css\">\n");
        builder.Append("    @media only screen and (max-width:").Append(mobileMax).Append(") {\n");
        builder.Append("      table.mj-full-width-mobile { width: 100% !important; }\n");
        builder.Append("      td.mj-full-width-mobile { width: auto !important; }\n");
        builder.Append("    }\n");
        builder.Append("  </style>\n");
    }

    private static void AppendComponentStyles(StringBuilder builder, RenderContext context)
    {
        var styles = new List<string>(context.ComponentStyles);
        if (styles.Count == 0) return;

        builder.Append("  <style type=\"text/css\">\n");
        foreach (var css in styles) builder.Append(Indent(css, "    ")).Append('\n');
        builder.Append("  </style>\n");
    }

    private static void AppendCustomStyles(StringBuilder builder, RenderContext context)
    {
        if (context.HeadStyles.Count == 0) return;

        builder.Append("  <style type=\"text/css\">\n");
        foreach (var css in context.HeadStyles) builder.Append(Indent(css.Trim(), "    ")).Append('\n');
        builder.Append("  </style>\n");
    }

    /// <summary>The hidden preheader shown next to the subject line in most inboxes.</summary>
    private static void AppendPreview(StringBuilder builder, RenderContext context)
    {
        if (string.IsNullOrEmpty(context.Preview)) return;

        builder.Append("<div style=\"display:none;font-size:1px;color:#ffffff;line-height:1px;")
            .Append("max-height:0px;max-width:0px;opacity:0;overflow:hidden;\">")
            .Append(HtmlEntities.Encode(context.Preview!))
            .Append("</div>\n");
    }

    private static string Indent(string text, string indent)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0) lines[i] = indent + lines[i];
        }

        return string.Join("\n", lines);
    }
}
