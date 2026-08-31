namespace MjmlProcessor;

/// <summary>Thrown when a MJML document cannot be parsed or rendered.</summary>
public class MjmlException : Exception
{
    /// <summary>Creates a new instance.</summary>
    public MjmlException(string message) : base(message) { }

    /// <summary>Creates a new instance.</summary>
    public MjmlException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>Creates a new instance carrying source position information.</summary>
    public MjmlException(string message, int line, int column)
        : base($"Line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }

    /// <summary>1-based line in the source document, or 0 when unknown.</summary>
    public int Line { get; }

    /// <summary>1-based column in the source document, or 0 when unknown.</summary>
    public int Column { get; }
}

/// <summary>Thrown when the MJML source is syntactically invalid.</summary>
public sealed class MjmlParseException : MjmlException
{
    /// <summary>Creates a new instance.</summary>
    public MjmlParseException(string message, int line, int column) : base(message, line, column) { }
}
