namespace MjmlProcessor.Tests;

public class ConversionTests
{
    private const string Minimal = """
        <mjml>
          <mj-body>
            <mj-section>
              <mj-column>
                <mj-text>Hello world</mj-text>
              </mj-column>
            </mj-section>
          </mj-body>
        </mjml>
        """;

    [Fact]
    public void Produces_a_complete_html_document()
    {
        var html = Mjml.ToHtml(Minimal);

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<html", html);
        Assert.Contains("</html>", html);
        Assert.Contains("Hello world", html);
    }

    [Fact]
    public void Emits_the_email_client_resets()
    {
        var html = Mjml.ToHtml(Minimal);

        Assert.Contains("#outlook a { padding: 0; }", html);
        Assert.Contains("mso-table-lspace: 0pt", html);
        Assert.Contains("-ms-interpolation-mode: bicubic", html);
    }

    [Fact]
    public void Wraps_sections_in_outlook_ghost_tables()
    {
        var html = Mjml.ToHtml(Minimal);

        Assert.Contains("<!--[if mso | IE]>", html);
        Assert.Contains("mso-line-height-rule:exactly", html);
        Assert.Contains("mj-outlook-group-fix", html);
    }

    [Fact]
    public void Defaults_to_a_600_pixel_document()
    {
        var html = Mjml.ToHtml(Minimal);

        Assert.Contains("max-width:600px;", html);
        Assert.Contains("width:600px;", html);
    }

    [Fact]
    public void Honours_a_custom_body_width()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body width="800px"><mj-section><mj-column>
            <mj-text>Wide</mj-text>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("max-width:800px;", html);
        Assert.DoesNotContain("max-width:600px;", html);
    }

    [Fact]
    public void Splits_column_widths_evenly_when_undeclared()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section>
              <mj-column><mj-text>A</mj-text></mj-column>
              <mj-column><mj-text>B</mj-text></mj-column>
            </mj-section></mj-body></mjml>
            """);

        Assert.Contains("mj-column-per-50", html);
        Assert.Contains(".mj-column-per-50 { width:50% !important; max-width: 50%; }", html);
    }

    [Fact]
    public void Computes_outlook_column_widths_from_the_section_content_box()
    {
        // 600px body, default 20px 0 section padding leaves 600px, split 60/40.
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section>
              <mj-column width="60%"><mj-text>A</mj-text></mj-column>
              <mj-column width="40%"><mj-text>B</mj-text></mj-column>
            </mj-section></mj-body></mjml>
            """);

        Assert.Contains("width:360px;", html);
        Assert.Contains("width:240px;", html);
    }

    [Fact]
    public void Subtracts_section_padding_from_the_column_width()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section padding="20px"><mj-column>
            <mj-text>A</mj-text>
            </mj-column></mj-section></mj-body></mjml>
            """);

        // 600 - 20 left - 20 right = 560.
        Assert.Contains("width:560px;", html);
    }

    [Fact]
    public void Caps_image_width_at_the_available_content_width()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-image src="https://example.com/a.png" width="900px" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        // 600 column less the image's own 25px horizontal padding on each side.
        Assert.Contains("width=\"550\"", html);
        Assert.DoesNotContain("width=\"900\"", html);
    }

    [Fact]
    public void Renders_an_image_without_a_width_at_full_content_width()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-image src="https://example.com/a.png" alt="Logo" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("width=\"550\"", html);
        Assert.Contains("alt=\"Logo\"", html);
    }

    [Fact]
    public void Always_emits_an_alt_attribute_for_screen_readers()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-image src="https://example.com/a.png" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("alt=\"\"", html);
    }

    [Fact]
    public void Renders_a_button_as_a_bulletproof_table()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-button href="https://example.com" background-color="#ff0000">Go</mj-button>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("border-collapse:separate", html);
        Assert.Contains("bgcolor=\"#ff0000\"", html);
        Assert.Contains("mso-padding-alt:10px 25px", html);
        Assert.Contains("<a href=\"https://example.com\"", html);
        Assert.Contains(">Go</a>", html);
    }

    [Fact]
    public void Renders_a_button_without_href_as_a_paragraph()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-button>Inert</mj-button>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains(">Inert</p>", html);
    }

    [Fact]
    public void Renders_divider_with_an_outlook_fallback()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-divider border-color="#cccccc" border-width="2px" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("border-top:solid 2px #cccccc", html);
        Assert.Contains("width:550px", html);
    }

    [Fact]
    public void Renders_spacer_with_matching_height_and_line_height()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-spacer height="40px" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("height:40px;line-height:40px;", html);
    }

    [Fact]
    public void Passes_table_markup_through_untouched()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-table><tr><th>Item</th></tr><tr><td>Value</td></tr></mj-table>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("<tr><th>Item</th></tr><tr><td>Value</td></tr>", html);
    }

    [Fact]
    public void Passes_raw_markup_through_without_wrapping()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-raw><span class="marker">untouched</span></mj-raw>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("<span class=\"marker\">untouched</span>", html);
    }
}
