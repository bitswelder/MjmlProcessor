namespace MjmlProcessor.Parsing;

/// <summary>A single element in a parsed MJML document tree.</summary>
public sealed class MjmlNode
{
    internal MjmlNode(string tagName, int line, int column)
    {
        TagName = tagName;
        Line = line;
        Column = column;
    }

    /// <summary>The lower-cased tag name, for example <c>mj-section</c>.</summary>
    public string TagName { get; internal set; }

    /// <summary>Attributes declared on the element, keyed case-insensitively.</summary>
    public Dictionary<string, string> Attributes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Child elements. Always empty for ending tags.</summary>
    public List<MjmlNode> Children { get; } = new List<MjmlNode>();

    /// <summary>
    /// The verbatim inner markup for ending tags such as <c>mj-text</c>, or <c>null</c>
    /// for container elements.
    /// </summary>
    public string? Content { get; internal set; }

    /// <summary>1-based line of the opening tag in the source document.</summary>
    public int Line { get; }

    /// <summary>1-based column of the opening tag in the source document.</summary>
    public int Column { get; }

    /// <summary>Returns the value of <paramref name="name"/>, or <c>null</c> when absent.</summary>
    public string? GetAttribute(string name)
        => Attributes.TryGetValue(name, out var value) ? value : null;

    /// <inheritdoc />
    public override string ToString() => $"<{TagName}> (line {Line})";
}
