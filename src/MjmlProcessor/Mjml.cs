namespace MjmlProcessor;

/// <summary>
/// The entry point for one-off conversions. For repeated use prefer a single
/// <see cref="MjmlConverter"/> instance, which avoids re-cloning the options each call.
/// </summary>
/// <example>
/// <code>
/// var html = Mjml.ToHtml("&lt;mjml&gt;&lt;mj-body&gt;&lt;mj-section&gt;&lt;mj-column&gt;" +
///                        "&lt;mj-text&gt;Hello&lt;/mj-text&gt;" +
///                        "&lt;/mj-column&gt;&lt;/mj-section&gt;&lt;/mj-body&gt;&lt;/mjml&gt;");
/// </code>
/// </example>
public static class Mjml
{
    /// <summary>Converts MJML markup to HTML.</summary>
    /// <param name="mjml">The MJML source document.</param>
    /// <param name="options">Conversion options, or <c>null</c> for the defaults.</param>
    /// <returns>The rendered HTML document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mjml"/> is null.</exception>
    /// <exception cref="MjmlParseException">The markup is syntactically invalid.</exception>
    public static string ToHtml(string mjml, MjmlOptions? options = null)
        => MjmlConverter.ToHtml(mjml, options);

    /// <summary>Converts MJML markup and returns the HTML together with any validation warnings.</summary>
    /// <param name="mjml">The MJML source document.</param>
    /// <param name="options">Conversion options, or <c>null</c> for the defaults.</param>
    public static MjmlResult Render(string mjml, MjmlOptions? options = null)
        => MjmlConverter.Render(mjml, options);

    /// <summary>Reads a MJML file and converts it, resolving includes relative to that file.</summary>
    /// <param name="path">Path to the <c>.mjml</c> file.</param>
    /// <param name="options">Conversion options, or <c>null</c> for the defaults.</param>
    public static MjmlResult RenderFile(string path, MjmlOptions? options = null)
        => new MjmlConverter(options).ConvertFile(path);

    /// <summary>Reads a MJML file and returns only the rendered HTML.</summary>
    /// <param name="path">Path to the <c>.mjml</c> file.</param>
    /// <param name="options">Conversion options, or <c>null</c> for the defaults.</param>
    public static string FileToHtml(string path, MjmlOptions? options = null)
        => RenderFile(path, options).Html;
}
