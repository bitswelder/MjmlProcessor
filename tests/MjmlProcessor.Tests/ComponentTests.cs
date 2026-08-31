using Xunit;

namespace MjmlProcessor.Tests;

public class ComponentTests
{
    [Fact]
    public void Full_width_section_spans_the_viewport()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section full-width="full-width" background-color="#123456">
            <mj-column><mj-text>Wide</mj-text></mj-column>
            </mj-section></mj-body></mjml>
            """);

        Assert.Contains("background-color:#123456;width:100%;", html);
        Assert.Contains("max-width:600px;", html);
    }

    [Fact]
    public void Section_background_image_gets_a_vml_fallback()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section background-url="https://example.com/bg.png" background-size="cover" background-repeat="no-repeat">
            <mj-column><mj-text>On an image</mj-text></mj-column>
            </mj-section></mj-body></mjml>
            """);

        Assert.Contains("<v:rect", html);
        Assert.Contains("<v:fill", html);
        Assert.Contains("</v:textbox></v:rect>", html);
        Assert.Contains("src=\"https://example.com/bg.png\"", html);
        Assert.Contains("url(https://example.com/bg.png)", html);
    }

    [Fact]
    public void Group_keeps_columns_side_by_side_on_mobile()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-group>
              <mj-column><mj-text>A</mj-text></mj-column>
              <mj-column><mj-text>B</mj-text></mj-column>
            </mj-group></mj-section></mj-body></mjml>
            """);

        // Columns inside a group keep a percentage width rather than stacking at 100%.
        Assert.Contains("width:50%;", html);
        Assert.Contains("mj-column-per-100", html);
    }

    [Fact]
    public void Column_padding_adds_a_gutter_table()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column padding="15px" background-color="#eeeeee">
            <mj-text>Padded</mj-text>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("background-color:#eeeeee;vertical-align:top;padding:15px;", html);
    }

    [Fact]
    public void Column_padding_shrinks_the_content_width()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section padding="0"><mj-column padding="20px">
            <mj-image src="https://example.com/a.png" padding="0" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        // 600 body - 40 column padding = 560.
        Assert.Contains("width=\"560\"", html);
    }

    [Fact]
    public void Wrapper_nests_sections_under_a_shared_background()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-wrapper background-color="#ff9900" padding="10px">
              <mj-section><mj-column><mj-text>One</mj-text></mj-column></mj-section>
              <mj-section><mj-column><mj-text>Two</mj-text></mj-column></mj-section>
            </mj-wrapper></mj-body></mjml>
            """);

        Assert.Contains("background-color:#ff9900", html);
        Assert.Contains("One", html);
        Assert.Contains("Two", html);
        // The wrapper owns the ghost table, so each nested section gets a row of its own.
        Assert.Contains("</td></tr><tr><td class=\"\"", html);
    }

    [Fact]
    public void Hero_renders_a_background_and_a_vml_image()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-hero background-url="https://example.com/hero.jpg" background-color="#2a3448" height="300px">
            <mj-text>Hero copy</mj-text>
            </mj-hero></mj-body></mjml>
            """);

        Assert.Contains("mj-hero-content", html);
        Assert.Contains("<v:image", html);
        Assert.Contains("Hero copy", html);
    }

    [Fact]
    public void Social_renders_known_networks_with_share_links()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column><mj-social>
              <mj-social-element name="facebook" href="https://example.com">Share</mj-social-element>
              <mj-social-element name="linkedin" href="https://example.com" />
            </mj-social></mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("facebook.com/sharer/sharer.php?u=https://example.com", html);
        Assert.Contains("linkedin.com/shareArticle", html);
        Assert.Contains("background:#3b5998", html);
        Assert.Contains(">Share</a>", html);
    }

    [Fact]
    public void Social_noshare_variants_link_straight_to_the_href()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column><mj-social>
              <mj-social-element name="facebook-noshare" href="https://example.com/page" />
            </mj-social></mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("href=\"https://example.com/page\"", html);
        Assert.DoesNotContain("sharer.php", html);
    }

    [Fact]
    public void Social_vertical_mode_stacks_the_icons()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column><mj-social mode="vertical">
              <mj-social-element name="github" href="https://example.com" />
            </mj-social></mj-column></mj-section></mj-body></mjml>
            """);

        Assert.DoesNotContain("display:inline-table", html);
    }

    [Fact]
    public void Unknown_social_network_warns_but_renders()
    {
        var result = Mjml.Render("""
            <mjml><mj-body><mj-section><mj-column><mj-social>
              <mj-social-element name="myspace" href="https://example.com" src="https://example.com/i.png" />
            </mj-social></mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains(result.Warnings, w => w.Message.Contains("myspace"));
        Assert.Contains("https://example.com/i.png", result.Html);
    }

    [Fact]
    public void Navbar_renders_inline_links()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column><mj-navbar base-url="https://example.com">
              <mj-navbar-link href="/about">About</mj-navbar-link>
              <mj-navbar-link href="/contact">Contact</mj-navbar-link>
            </mj-navbar></mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("mj-inline-links", html);
        Assert.Contains("href=\"https://example.com/about\"", html);
        Assert.Contains("href=\"https://example.com/contact\"", html);
    }

    [Fact]
    public void Navbar_hamburger_adds_the_checkbox_toggle()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column><mj-navbar hamburger="hamburger">
              <mj-navbar-link href="/a">A</mj-navbar-link>
            </mj-navbar></mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("mj-menu-checkbox", html);
        Assert.Contains("mj-menu-label", html);
        Assert.Contains("noinput.mj-menu-checkbox", html);
    }

    [Fact]
    public void Accordion_renders_panels_and_its_stylesheet()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column><mj-accordion>
              <mj-accordion-element>
                <mj-accordion-title>Question</mj-accordion-title>
                <mj-accordion-text>Answer</mj-accordion-text>
              </mj-accordion-element>
            </mj-accordion></mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("mj-accordion-checkbox", html);
        Assert.Contains("mj-accordion-title", html);
        Assert.Contains("mj-accordion-content", html);
        Assert.Contains("Question", html);
        Assert.Contains("Answer", html);
    }

    [Fact]
    public void Component_stylesheets_are_emitted_once_per_document()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section>
              <mj-column><mj-navbar><mj-navbar-link href="/a">A</mj-navbar-link></mj-navbar></mj-column>
              <mj-column><mj-navbar><mj-navbar-link href="/b">B</mj-navbar-link></mj-navbar></mj-column>
            </mj-section></mj-body></mjml>
            """);

        var occurrences = html.Split(new[] { "noinput.mj-menu-checkbox" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Fluid_on_mobile_images_get_the_helper_class()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-image src="https://example.com/a.png" fluid-on-mobile="true" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("class=\"mj-full-width-mobile\"", html);
    }

    [Fact]
    public void Container_background_color_lands_on_the_wrapping_cell()
    {
        var html = Mjml.ToHtml("""
            <mjml><mj-body><mj-section><mj-column>
            <mj-text container-background-color="#abcdef">Tinted</mj-text>
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("background:#abcdef;font-size:0px;", html);
    }
}
