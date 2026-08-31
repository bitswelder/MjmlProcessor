using Xunit;

namespace MjmlProcessor.Tests;

public class IncludeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mjml-tests-" + Guid.NewGuid().ToString("N"));

    public IncludeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Includes_a_partial_relative_to_the_document()
    {
        Write("header.mjml", "<mj-section><mj-column><mj-text>Shared header</mj-text></mj-column></mj-section>");
        var main = Write("index.mjml", """
            <mjml><mj-body>
              <mj-include path="header.mjml" />
              <mj-section><mj-column><mj-text>Body</mj-text></mj-column></mj-section>
            </mj-body></mjml>
            """);

        var result = Mjml.RenderFile(main);

        Assert.True(result.IsValid, string.Join("; ", result.Warnings));
        Assert.Contains("Shared header", result.Html);
        Assert.Contains("Body", result.Html);
    }

    [Fact]
    public void Resolves_a_missing_extension()
    {
        Write("footer.mjml", "<mj-section><mj-column><mj-text>Footer</mj-text></mj-column></mj-section>");
        var main = Write("index.mjml",
            "<mjml><mj-body><mj-include path=\"footer\" /></mj-body></mjml>");

        Assert.Contains("Footer", Mjml.FileToHtml(main));
    }

    [Fact]
    public void Unwraps_an_included_full_document()
    {
        Write("part.mjml", """
            <mjml><mj-body>
              <mj-section><mj-column><mj-text>From a full document</mj-text></mj-column></mj-section>
            </mj-body></mjml>
            """);
        var main = Write("index.mjml",
            "<mjml><mj-body><mj-include path=\"part.mjml\" /></mj-body></mjml>");

        Assert.Contains("From a full document", Mjml.FileToHtml(main));
    }

    [Fact]
    public void Includes_html_verbatim()
    {
        Write("banner.html", "<div id=\"banner\">raw</div>");
        var main = Write("index.mjml", """
            <mjml><mj-body><mj-section><mj-column>
              <mj-include path="banner.html" type="html" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains("<div id=\"banner\">raw</div>", Mjml.FileToHtml(main));
    }

    [Fact]
    public void Includes_css_into_the_head()
    {
        Write("theme.css", ".included { color: teal; }");
        var main = Write("index.mjml", """
            <mjml><mj-body><mj-section><mj-column>
              <mj-include path="theme.css" type="css" />
            </mj-column></mj-section></mj-body></mjml>
            """);

        Assert.Contains(".included { color: teal; }", Mjml.FileToHtml(main));
    }

    [Fact]
    public void Warns_when_a_partial_is_missing()
    {
        var main = Write("index.mjml",
            "<mjml><mj-body><mj-include path=\"absent.mjml\" /></mj-body></mjml>");

        var result = Mjml.RenderFile(main);

        Assert.Contains(result.Warnings, w => w.Message.Contains("absent.mjml"));
    }

    [Fact]
    public void Warns_when_no_file_loader_is_configured()
    {
        var result = Mjml.Render("<mjml><mj-body><mj-include path=\"x.mjml\" /></mj-body></mjml>");

        Assert.Contains(result.Warnings, w => w.Message.Contains("FileLoader"));
    }

    [Fact]
    public void Refuses_to_escape_the_configured_root()
    {
        var loader = new DirectoryFileLoader(_root);

        Assert.Null(loader.Load("../../../etc/passwd"));
    }

    [Fact]
    public void Stops_runaway_recursive_includes()
    {
        Write("loop.mjml", "<mj-section><mj-column><mj-include path=\"loop.mjml\" /></mj-column></mj-section>");
        var main = Write("index.mjml",
            "<mjml><mj-body><mj-include path=\"loop.mjml\" /></mj-body></mjml>");

        var result = Mjml.RenderFile(main);

        Assert.Contains(result.Warnings, w => w.Message.Contains("nesting"));
    }

    [Fact]
    public void Missing_file_throws()
    {
        Assert.Throws<FileNotFoundException>(() => Mjml.RenderFile(Path.Combine(_root, "nope.mjml")));
    }
}
