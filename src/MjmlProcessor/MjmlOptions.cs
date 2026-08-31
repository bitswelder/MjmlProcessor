namespace MjmlProcessor;

/// <summary>
/// Controls how a MJML document is converted to HTML.
/// </summary>
public sealed class MjmlOptions
{
    /// <summary>Default options used when none are supplied.</summary>
    public static MjmlOptions Default { get; } = new MjmlOptions();

    /// <summary>
    /// When <c>true</c> (the default) the rendered HTML is indented and line-broken.
    /// Set to <c>false</c> together with <see cref="Minify"/> for the smallest payload.
    /// </summary>
    public bool Beautify { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, collapses insignificant whitespace between tags. Content inside
    /// <c>mj-text</c>, <c>mj-raw</c> and other ending tags is never touched.
    /// </summary>
    public bool Minify { get; set; }

    /// <summary>
    /// When <c>true</c> (the default) the <c>&lt;!doctype html&gt;</c> declaration and the
    /// surrounding <c>&lt;html&gt;</c> skeleton are emitted. When <c>false</c>, only the body
    /// markup is produced, which is useful when embedding a fragment elsewhere.
    /// </summary>
    public bool IncludeDocumentSkeleton { get; set; } = true;

    /// <summary>
    /// Language emitted as the <c>lang</c> attribute of the <c>&lt;html&gt;</c> element.
    /// </summary>
    public string Language { get; set; } = "und";

    /// <summary>
    /// Text direction emitted as the <c>dir</c> attribute of the <c>&lt;html&gt;</c> element.
    /// </summary>
    public string Direction { get; set; } = "auto";

    /// <summary>
    /// How strictly validation problems are reported. Defaults to
    /// <see cref="MjmlValidationLevel.Soft"/>, which collects warnings instead of throwing.
    /// </summary>
    public MjmlValidationLevel ValidationLevel { get; set; } = MjmlValidationLevel.Soft;

    /// <summary>
    /// Resolves the documents referenced by <c>&lt;mj-include /&gt;</c>. When <c>null</c>,
    /// <c>mj-include</c> elements are ignored and reported as warnings.
    /// </summary>
    public IMjmlFileLoader? FileLoader { get; set; }

    /// <summary>
    /// Extra web fonts made available to the document, keyed by font family name.
    /// Equivalent to declaring <c>&lt;mj-font /&gt;</c> in the document head.
    /// </summary>
    public IDictionary<string, string> Fonts { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Open Sans"] = "https://fonts.googleapis.com/css?family=Open+Sans:300,400,500,700",
        ["Droid Sans"] = "https://fonts.googleapis.com/css?family=Droid+Sans:300,400,500,700",
        ["Lato"] = "https://fonts.googleapis.com/css?family=Lato:300,400,500,700",
        ["Roboto"] = "https://fonts.googleapis.com/css?family=Roboto:300,400,500,700",
        ["Ubuntu"] = "https://fonts.googleapis.com/css?family=Ubuntu:300,400,500,700",
    };

    internal MjmlOptions Clone()
    {
        var clone = new MjmlOptions
        {
            Beautify = Beautify,
            Minify = Minify,
            IncludeDocumentSkeleton = IncludeDocumentSkeleton,
            Language = Language,
            Direction = Direction,
            ValidationLevel = ValidationLevel,
            FileLoader = FileLoader,
        };
        clone.Fonts.Clear();
        foreach (var pair in Fonts) clone.Fonts[pair.Key] = pair.Value;
        return clone;
    }
}

/// <summary>Determines how the converter reacts to malformed or unknown markup.</summary>
public enum MjmlValidationLevel
{
    /// <summary>Ignore validation problems entirely.</summary>
    Skip = 0,

    /// <summary>Collect problems into <see cref="MjmlResult.Warnings"/> and keep rendering.</summary>
    Soft = 1,

    /// <summary>Throw a <see cref="MjmlException"/> on the first validation problem.</summary>
    Strict = 2,
}

/// <summary>Resolves documents referenced by <c>&lt;mj-include /&gt;</c>.</summary>
public interface IMjmlFileLoader
{
    /// <summary>
    /// Returns the contents of <paramref name="path"/>, or <c>null</c> when it cannot be found.
    /// </summary>
    string? Load(string path);
}
