using Xunit;

namespace MjmlProcessor.Tests;

public class InlineStyleTests
{
    /// <summary>Wraps <paramref name="css"/> and <paramref name="body"/> in a minimal document.</summary>
    private static string Render(string css, string body) => Mjml.ToHtml(
        "<mjml><mj-head><mj-style inline=\"inline\">" + css + "</mj-style></mj-head>" +
        "<mj-body><mj-section><mj-column>" + body + "</mj-column></mj-section></mj-body></mjml>");

    [Fact]
    public void Inlines_a_class_rule_onto_the_matching_element()
    {
        var html = Render(".promo { color: red; }", "<mj-text css-class=\"promo\">Hi</mj-text>");

        Assert.Contains("color:red;", html);
        // The rule is consumed rather than left behind in a style block.
        Assert.DoesNotContain(".promo { color: red; }", html);
    }

    [Fact]
    public void Reaches_the_authors_own_markup_inside_mj_text()
    {
        var html = Render("p { color: #336699; }", "<mj-text><p>Paragraph</p></mj-text>");

        Assert.Contains("<p style=\"color:#336699;\">Paragraph</p>", html);
    }

    [Fact]
    public void Reaches_markup_inside_mj_raw()
    {
        var html = Render("span { font-weight: bold; }", "<mj-raw><span>Raw</span></mj-raw>");

        Assert.Contains("<span style=\"font-weight:bold;\">Raw</span>", html);
    }

    [Fact]
    public void Merges_into_an_existing_style_attribute()
    {
        var html = Render("a { border: 1px solid black; }",
            "<mj-text><a href=\"#\" style=\"color:blue;\">Link</a></mj-text>");

        Assert.Contains("border:1px solid black;", html);
        Assert.Contains("color:blue;", html);
    }

    [Fact]
    public void An_existing_inline_declaration_wins_over_the_stylesheet()
    {
        var html = Render("a { color: red; }",
            "<mj-text><a href=\"#\" style=\"color:blue;\">Link</a></mj-text>");

        Assert.Contains("color:blue;", html);
        Assert.DoesNotContain("color:red;", html);
    }

    [Fact]
    public void An_important_rule_beats_an_existing_inline_declaration()
    {
        var html = Render("a { color: red !important; }",
            "<mj-text><a href=\"#\" style=\"color:blue;\">Link</a></mj-text>");

        Assert.Contains("color:red !important;", html);
        Assert.DoesNotContain("color:blue;", html);
    }

    [Fact]
    public void Higher_specificity_wins()
    {
        var html = Render(
            "p { color: red; } .lead { color: green; } #hero { color: blue; }",
            "<mj-text><p id=\"hero\" class=\"lead\">Text</p></mj-text>");

        Assert.Contains("color:blue;", html);
        Assert.DoesNotContain("color:green;", html);
        Assert.DoesNotContain("color:red;", html);
    }

    [Fact]
    public void Source_order_breaks_a_specificity_tie()
    {
        var html = Render(".a { color: red; } .b { color: green; }",
            "<mj-text><p class=\"a b\">Text</p></mj-text>");

        Assert.Contains("color:green;", html);
    }

    [Theory]
    [InlineData("div p { color: red; }")]
    [InlineData("div > p { color: red; }")]
    [InlineData("h1 + p { color: red; }")]
    [InlineData("h1 ~ p { color: red; }")]
    [InlineData("p:last-child { color: red; }")]
    [InlineData("p:nth-child(2) { color: red; }")]
    [InlineData("p:not(.skip) { color: red; }")]
    [InlineData("[data-role=\"body\"] { color: red; }")]
    [InlineData("p[data-role^=\"bo\"] { color: red; }")]
    public void Supports_the_common_selector_forms(string css)
    {
        var html = Render(css, "<mj-text><div><h1>Title</h1><p data-role=\"body\">Text</p></div></mj-text>");

        Assert.Contains("color:red;", html);
    }

    [Fact]
    public void Does_not_match_the_wrong_element()
    {
        var html = Render("h1 + p { color: red; }", "<mj-text><p>First</p><h1>Title</h1></mj-text>");

        Assert.DoesNotContain("color:red;", html);
    }

    [Fact]
    public void Not_excludes_the_negated_element()
    {
        var html = Render("p:not(.skip) { color: red; }",
            "<mj-text><p class=\"skip\">Skipped</p></mj-text>");

        Assert.DoesNotContain("color:red;", html);
    }

    [Fact]
    public void Media_queries_are_preserved_in_a_style_block()
    {
        var html = Render("@media only screen and (max-width:480px) { .m { display: none; } }",
            "<mj-text css-class=\"m\">Hi</mj-text>");

        Assert.Contains("@media only screen and (max-width:480px)", html);
        Assert.Contains("display: none", html);
    }

    [Fact]
    public void Pseudo_class_rules_are_preserved_in_a_style_block()
    {
        var html = Render("a:hover { color: purple; } a { color: teal; }",
            "<mj-text><a href=\"#\">Link</a></mj-text>");

        // The static rule is inlined, the stateful one keeps a style block.
        Assert.Contains("color:teal;", html);
        Assert.Contains("a:hover { color:purple; }", html);
    }

    [Fact]
    public void Pseudo_elements_are_preserved_in_a_style_block()
    {
        var html = Render("p::before { content: \"x\"; }", "<mj-text><p>Text</p></mj-text>");

        Assert.Contains("p::before", html);
    }

    [Fact]
    public void A_selector_group_splits_between_inlined_and_preserved()
    {
        var html = Render("p, a:hover { color: olive; }", "<mj-text><p>Text</p></mj-text>");

        Assert.Contains("<p style=\"color:olive;\">", html);
        Assert.Contains("a:hover { color:olive; }", html);
    }

    [Fact]
    public void Comments_and_multiple_declarations_are_handled()
    {
        var html = Render("/* leading */ p { color: red; /* mid */ font-size: 20px }",
            "<mj-text><p>Text</p></mj-text>");

        Assert.Contains("color:red;", html);
        Assert.Contains("font-size:20px;", html);
    }

    [Fact]
    public void Several_inline_style_blocks_are_combined()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-head>
              <mj-style inline="inline">p { color: red; }</mj-style>
              <mj-style inline="inline">p { font-size: 21px; }</mj-style>
            </mj-head><mj-body><mj-section><mj-column>
              <mj-text><p>Text</p></mj-text>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("color:red;", html);
        Assert.Contains("font-size:21px;", html);
    }

    [Fact]
    public void Non_inline_mj_style_still_goes_to_a_style_block()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-head>
              <mj-style>p { color: red; }</mj-style>
            </mj-head><mj-body><mj-section><mj-column>
              <mj-text><p>Text</p></mj-text>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("p { color: red; }", html);
        Assert.DoesNotContain("<p style=", html);
    }

    [Fact]
    public void Values_containing_semicolons_and_urls_survive()
    {
        var html = Render("p { background: url(\"http://e.test/a;b.png\") no-repeat; }",
            "<mj-text><p>Text</p></mj-text>");

        Assert.Contains("background:url(&quot;http://e.test/a;b.png&quot;) no-repeat;", html);
    }

    [Fact]
    public void Attribute_values_are_escaped_when_written_back()
    {
        var html = Render("p { font-family: \"Times New Roman\", serif; }", "<mj-text><p>Text</p></mj-text>");

        Assert.Contains("&quot;Times New Roman&quot;", html);
        Assert.DoesNotContain("style=\"font-family:\"Times", html);
    }

    [Fact]
    public void Markup_inside_outlook_conditional_comments_is_left_alone()
    {
        var html = Render("td { color: red; }", "<mj-text>Hi</mj-text>");

        // The ghost tables live inside comments and must not gain style attributes.
        Assert.Contains("<!--[if mso | IE]><table role=\"presentation\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\"><tr><![endif]-->", html);
    }

    [Fact]
    public void Void_elements_receive_a_style_attribute_correctly()
    {
        var html = Render("img { border-radius: 4px; }",
            "<mj-image src=\"https://example.com/a.png\" />");

        Assert.Contains("border-radius:4px;", html);
        Assert.Contains("/>", html);
    }

    [Fact]
    public void Unmatched_elements_are_returned_byte_for_byte()
    {
        const string body = "<mj-text><p>Text</p></mj-text>";

        var withInlining = Render(".nothing-matches { color: red; }", body);
        var without = Mjml.ToHtml(
            "<mjml><mj-body><mj-section><mj-column>" + body + "</mj-column></mj-section></mj-body></mjml>");

        Assert.Equal(without, withInlining);
    }

    [Fact]
    public void Malformed_author_markup_does_not_corrupt_the_output()
    {
        var html = Render("b { color: red; }", "<mj-text><p>One<p>Two<b>Bold</mj-text>");

        Assert.Contains("<b style=\"color:red;\">Bold", html);
        Assert.Contains("One", html);
        Assert.Contains("Two", html);
    }

    [Fact]
    public void Inlining_works_without_the_document_skeleton()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-head><mj-style inline="inline">p { color: red; }</mj-style></mj-head>
            <mj-body><mj-section><mj-column><mj-text><p>Text</p></mj-text></mj-column></mj-section></mj-body></mjml>
            """, new MjmlOptions { IncludeDocumentSkeleton = false });

        Assert.Contains("<p style=\"color:red;\">Text</p>", html);
    }

    [Fact]
    public void Inlining_survives_minification()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-head><mj-style inline="inline">p { color: red; }</mj-style></mj-head>
            <mj-body><mj-section><mj-column><mj-text><p>Text</p></mj-text></mj-column></mj-section></mj-body></mjml>
            """, new MjmlOptions { Minify = true });

        Assert.Contains("<p style=\"color:red;\">Text</p>", html);
    }

    [Fact]
    public void Style_blocks_in_raw_markup_are_not_treated_as_elements()
    {
        var html = Render("i { color: red; }",
            "<mj-raw><style>i { color: blue; }</style><i>Italic</i></mj-raw>");

        Assert.Contains("<style>i { color: blue; }</style>", html);
        Assert.Contains("<i style=\"color:red;\">Italic</i>", html);
    }

    [Fact]
    public void Warns_when_preserved_css_has_nowhere_to_go()
    {
        var result = Mjml.Render("""
            <mjml><mj-head><mj-style inline="inline">a:hover { color: red; }</mj-style></mj-head>
            <mj-body><mj-section><mj-column><mj-text>Hi</mj-text></mj-column></mj-section></mj-body></mjml>
            """, new MjmlOptions { IncludeDocumentSkeleton = false });

        Assert.Contains(result.Warnings, w => w.Message.Contains("IncludeDocumentSkeleton"));
    }

    [Fact]
    public void Reaches_the_body_element_of_the_skeleton()
    {
        var html = Render("body { background-color: #101010; }", "<mj-text>Hi</mj-text>");

        // Inlined declarations are written ahead of the ones already on the element.
        Assert.Contains("<body style=\"background-color:#101010;word-spacing:normal;\">", html);
    }

    [Fact]
    public void Style_blocks_in_the_head_are_not_treated_as_elements()
    {
        var html = Render("td { color: red; }", "<mj-text>Hi</mj-text>");

        // The reset stylesheet must come through untouched.
        Assert.Contains("#outlook a { padding: 0; }", html);
        Assert.Contains("table, td { border-collapse: collapse;", html);
    }

    [Fact]
    public void Include_can_supply_inlined_css()
    {
        var root = Path.Combine(Path.GetTempPath(), "mjml-inline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "theme.css"), "p { color: #654321; }");
            var main = Path.Combine(root, "index.mjml");
            File.WriteAllText(main, """
                <mjml><mj-body><mj-section><mj-column>
                  <mj-include path="theme.css" type="css" css-inline="inline" />
                  <mj-text><p>Text</p></mj-text>
                </mj-column></mj-section></mj-body></mjml>
                """);

            Assert.Contains("<p style=\"color:#654321;\">Text</p>", Mjml.FileToHtml(main));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
