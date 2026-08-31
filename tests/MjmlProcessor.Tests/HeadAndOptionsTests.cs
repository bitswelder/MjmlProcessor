using Xunit;

namespace MjmlProcessor.Tests;

public class HeadTests
{
    [Fact]
    public void Renders_the_title()
    {
        var html = Render("<mj-title>My newsletter</mj-title>");

        Assert.Contains("<title>My newsletter</title>", html);
    }

    [Fact]
    public void Renders_preview_text_in_a_hidden_div()
    {
        var html = Render("<mj-preview>Peek at this</mj-preview>");

        Assert.Contains("Peek at this", html);
        Assert.Contains("display:none;font-size:1px;", html);
        Assert.Contains("max-height:0px;", html);
    }

    [Fact]
    public void Applies_mj_all_defaults_to_every_component()
    {
        var html = Render("<mj-attributes><mj-all font-family=\"Georgia, serif\" /></mj-attributes>");

        Assert.Contains("font-family:Georgia, serif", html);
    }

    [Fact]
    public void Applies_per_tag_defaults()
    {
        var html = Render("<mj-attributes><mj-text color=\"#ff0000\" /></mj-attributes>");

        Assert.Contains("color:#ff0000", html);
    }

    [Fact]
    public void Applies_named_mj_class_attributes()
    {
        var html = Mjml.ToHtml("""
            <mjml>
              <mj-head>
                <mj-attributes>
                  <mj-class name="lead" font-size="22px" color="#0000ff" />
                </mj-attributes>
              </mj-head>
              <mj-body><mj-section><mj-column>
                <mj-text mj-class="lead">Lead</mj-text>
              </mj-column></mj-section></mj-body>
            </mjml>
            """);

        Assert.Contains("font-size:22px", html);
        Assert.Contains("color:#0000ff", html);
    }

    [Fact]
    public void Inline_attributes_beat_mj_class_which_beats_tag_defaults()
    {
        var html = Mjml.ToHtml("""
            <mjml>
              <mj-head>
                <mj-attributes>
                  <mj-text color="#111111" font-size="10px" />
                  <mj-class name="big" font-size="30px" />
                </mj-attributes>
              </mj-head>
              <mj-body><mj-section><mj-column>
                <mj-text mj-class="big" color="#222222">Text</mj-text>
              </mj-column></mj-section></mj-body>
            </mjml>
            """);

        Assert.Contains("font-size:30px", html);
        Assert.Contains("color:#222222", html);
        Assert.DoesNotContain("color:#111111", html);
    }

    [Fact]
    public void Emits_custom_css_from_mj_style()
    {
        var html = Render("<mj-style>.promo { color: green; }</mj-style>");

        Assert.Contains(".promo { color: green; }", html);
    }

    [Fact]
    public void Registers_custom_fonts_from_mj_font()
    {
        var html = Mjml.ToHtml("""
            <mjml>
              <mj-head>
                <mj-font name="Comic" href="https://fonts.example/comic.css" />
              </mj-head>
              <mj-body><mj-section><mj-column>
                <mj-text font-family="Comic">Hi</mj-text>
              </mj-column></mj-section></mj-body>
            </mjml>
            """);

        Assert.Contains("https://fonts.example/comic.css", html);
    }

    [Fact]
    public void Only_imports_fonts_that_are_actually_used()
    {
        var html = Mjml.ToHtml("""
            <mjml>
              <mj-head><mj-font name="Comic" href="https://fonts.example/comic.css" /></mj-head>
              <mj-body><mj-section><mj-column>
                <mj-text font-family="Arial, sans-serif">Hi</mj-text>
              </mj-column></mj-section></mj-body>
            </mjml>
            """);

        Assert.DoesNotContain("comic.css", html);
    }

    [Fact]
    public void Applies_a_custom_breakpoint()
    {
        var html = Render("<mj-breakpoint width=\"320px\" />");

        Assert.Contains("min-width:320px", html);
        Assert.Contains("max-width:319px", html);
    }

    private static string Render(string head) => Mjml.ToHtml(
        "<mjml><mj-head>" + head + "</mj-head><mj-body><mj-section><mj-column>" +
        "<mj-text>Body</mj-text></mj-column></mj-section></mj-body></mjml>");
}

public class OptionsTests
{
    private const string Source = """
        <mjml><mj-body><mj-section><mj-column>
        <mj-text>Hello</mj-text>
        </mj-column></mj-section></mj-body></mjml>
        """;

    [Fact]
    public void Can_omit_the_document_skeleton()
    {
        var html = Mjml.ToHtml(Source, new MjmlOptions { IncludeDocumentSkeleton = false });

        Assert.DoesNotContain("<!doctype html>", html);
        Assert.DoesNotContain("<head>", html);
        Assert.Contains("Hello", html);
    }

    [Fact]
    public void Minify_removes_whitespace_between_tags()
    {
        var pretty = Mjml.ToHtml(Source);
        var minified = Mjml.ToHtml(Source, new MjmlOptions { Minify = true });

        Assert.True(minified.Length < pretty.Length);
        Assert.DoesNotContain("\n  <", minified);
        Assert.Contains("Hello", minified);
    }

    [Fact]
    public void Minify_preserves_spaces_inside_text_content()
    {
        var minified = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-text>one two three</mj-text>
            </mj-column></mj-section></mj-body></mjml>
            """, new MjmlOptions { Minify = true });

        Assert.Contains("one two three", minified);
    }

    [Fact]
    public void Language_and_direction_are_configurable()
    {
        var html = Mjml.ToHtml(Source, new MjmlOptions { Language = "fr", Direction = "rtl" });

        Assert.Contains("lang=\"fr\"", html);
        Assert.Contains("dir=\"rtl\"", html);
    }

    [Fact]
    public void Soft_validation_collects_warnings_instead_of_throwing()
    {
        var result = Mjml.Render("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-nonsense />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.False(result.IsValid);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("mj-nonsense", warning.TagName);
    }

    [Fact]
    public void Strict_validation_throws()
    {
        var options = new MjmlOptions { ValidationLevel = MjmlValidationLevel.Strict };

        Assert.Throws<MjmlException>(() => Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-nonsense />
            </mj-column></mj-section></mj-body></mjml>
            """, options));
    }

    [Fact]
    public void Skip_validation_reports_nothing()
    {
        var result = Mjml.Render("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-nonsense />
            </mj-column></mj-section></mj-body></mjml>
            """, new MjmlOptions { ValidationLevel = MjmlValidationLevel.Skip });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void A_document_without_a_body_warns_but_still_renders()
    {
        var result = Mjml.Render("<mjml><mj-head><mj-title>Empty</mj-title></mj-head></mjml>");

        Assert.Contains("<title>Empty</title>", result.Html);
        Assert.Contains(result.Warnings, w => w.Message.Contains("mj-body"));
    }

    [Fact]
    public void Null_input_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => Mjml.ToHtml(null!));
    }

    [Fact]
    public void Options_are_snapshotted_at_construction()
    {
        var options = new MjmlOptions { IncludeDocumentSkeleton = false };
        var converter = new MjmlConverter(options);

        options.IncludeDocumentSkeleton = true;

        Assert.DoesNotContain("<!doctype html>", converter.ConvertToHtml(Source));
    }
}
