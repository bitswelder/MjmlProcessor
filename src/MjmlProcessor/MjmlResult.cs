using System.Collections.ObjectModel;

namespace MjmlProcessor;

/// <summary>The outcome of converting a MJML document to HTML.</summary>
public sealed class MjmlResult
{
    internal MjmlResult(string html, IList<MjmlWarning> warnings)
    {
        Html = html;
        Warnings = new ReadOnlyCollection<MjmlWarning>(warnings);
    }

    /// <summary>The rendered HTML document.</summary>
    public string Html { get; }

    /// <summary>Validation problems collected while rendering. Empty on a clean document.</summary>
    public IReadOnlyList<MjmlWarning> Warnings { get; }

    /// <summary><c>true</c> when no warnings were produced.</summary>
    public bool IsValid => Warnings.Count == 0;

    /// <summary>Returns the rendered HTML.</summary>
    public override string ToString() => Html;
}

/// <summary>A single validation problem found while rendering a MJML document.</summary>
public sealed class MjmlWarning
{
    internal MjmlWarning(string tagName, string message, int line, int column)
    {
        TagName = tagName;
        Message = message;
        Line = line;
        Column = column;
    }

    /// <summary>The MJML tag the problem relates to.</summary>
    public string TagName { get; }

    /// <summary>A human readable description of the problem.</summary>
    public string Message { get; }

    /// <summary>1-based line in the source document.</summary>
    public int Line { get; }

    /// <summary>1-based column in the source document.</summary>
    public int Column { get; }

    /// <inheritdoc />
    public override string ToString() => $"Line {Line}, column {Column} ({TagName}): {Message}";
}
