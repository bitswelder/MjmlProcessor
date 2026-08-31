using MjmlProcessor.Parsing;
using Xunit;

namespace MjmlProcessor.Tests;

public class ParserTests
{
    [Fact]
    public void Parses_nested_elements()
    {
        var root = MjmlParser.Parse("<mjml><mj-body><mj-section><mj-column /></mj-section></mj-body></mjml>");

        Assert.Equal("mjml", root.TagName);
        var body = Assert.Single(root.Children);
        var section = Assert.Single(body.Children);
        Assert.Equal("mj-section", section.TagName);
        Assert.Equal("mj-column", Assert.Single(section.Children).TagName);
    }

    [Fact]
    public void Captures_ending_tag_content_verbatim()
    {
        var root = MjmlParser.Parse("<mjml><mj-body><mj-text>a <br> b &nbsp; <b>c</b></mj-text></mj-body></mjml>");
        var text = root.Children[0].Children[0];

        Assert.Equal("a <br> b &nbsp; <b>c</b>", text.Content);
        Assert.Empty(text.Children);
    }

    [Fact]
    public void Keeps_conditional_comments_inside_raw_content()
    {
        const string inner = "<!--[if mso]><table><tr><td>x</td></tr></table><![endif]-->";
        var root = MjmlParser.Parse("<mjml><mj-body><mj-raw>" + inner + "</mj-raw></mj-body></mjml>");

        Assert.Equal(inner, root.Children[0].Children[0].Content);
    }

    [Fact]
    public void Decodes_entities_in_attribute_values()
    {
        var root = MjmlParser.Parse("<mjml><mj-body><mj-button href=\"https://x.test/?a=1&amp;b=2\" /></mj-body></mjml>");

        Assert.Equal("https://x.test/?a=1&b=2", root.Children[0].Children[0].GetAttribute("href"));
    }

    [Fact]
    public void Accepts_single_quoted_and_unquoted_attributes()
    {
        var root = MjmlParser.Parse("<mjml><mj-body><mj-section padding='10px' full-width=full-width /></mj-body></mjml>");
        var section = root.Children[0].Children[0];

        Assert.Equal("10px", section.GetAttribute("padding"));
        Assert.Equal("full-width", section.GetAttribute("full-width"));
    }

    [Fact]
    public void Skips_the_xml_declaration_and_comments()
    {
        var root = MjmlParser.Parse("<?xml version=\"1.0\"?><!-- note --><mjml><mj-body /></mjml>");

        Assert.Equal("mjml", root.TagName);
        Assert.Equal("mj-body", Assert.Single(root.Children).TagName);
    }

    [Fact]
    public void Records_source_positions()
    {
        var root = MjmlParser.Parse("<mjml>\n  <mj-body>\n    <mj-section />\n  </mj-body>\n</mjml>");
        var section = root.Children[0].Children[0];

        Assert.Equal(3, section.Line);
        Assert.Equal(5, section.Column);
    }

    [Fact]
    public void Reports_mismatched_closing_tags()
    {
        var exception = Assert.Throws<MjmlParseException>(
            () => MjmlParser.Parse("<mjml><mj-body></mj-section></mj-body></mjml>"));

        Assert.Contains("mj-section", exception.Message);
        Assert.Equal(1, exception.Line);
    }

    [Fact]
    public void Reports_unclosed_tags()
    {
        var exception = Assert.Throws<MjmlParseException>(() => MjmlParser.Parse("<mjml><mj-body>"));

        Assert.Contains("Unclosed", exception.Message);
    }

    [Fact]
    public void Reports_an_unclosed_ending_tag()
    {
        var exception = Assert.Throws<MjmlParseException>(
            () => MjmlParser.Parse("<mjml><mj-body><mj-text>oops</mj-body></mjml>"));

        Assert.Contains("mj-text", exception.Message);
    }

    [Fact]
    public void Wraps_a_fragment_that_has_no_mjml_root()
    {
        var root = MjmlParser.Parse("<mj-body><mj-section /></mj-body>");

        Assert.Equal("mjml", root.TagName);
        Assert.Equal("mj-body", Assert.Single(root.Children).TagName);
    }

    [Theory]
    [InlineData("<mjml><mj-body/></mjml>")]
    [InlineData("<MJML><MJ-BODY/></MJML>")]
    public void Tag_names_are_case_insensitive(string source)
    {
        var root = MjmlParser.Parse(source);

        Assert.Equal("mjml", root.TagName);
        Assert.Equal("mj-body", Assert.Single(root.Children).TagName);
    }
}
