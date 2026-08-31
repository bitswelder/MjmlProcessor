using System.Text;
using MjmlProcessor.Parsing;

namespace MjmlProcessor.Internal;

/// <summary>Ordered set of CSS declarations rendered into a <c>style</c> attribute.</summary>
internal sealed class StyleBuilder
{
    private readonly List<KeyValuePair<string, string>> _declarations = new();

    /// <summary>Adds a declaration. Null and empty values are dropped.</summary>
    public StyleBuilder Add(string property, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _declarations.Add(new KeyValuePair<string, string>(property, value!.Trim()));
        }

        return this;
    }

    /// <summary>Adds a declaration only when <paramref name="condition"/> holds.</summary>
    public StyleBuilder AddIf(bool condition, string property, string? value)
        => condition ? Add(property, value) : this;

    public bool IsEmpty => _declarations.Count == 0;

    /// <summary>Renders the declarations, or null when there are none.</summary>
    public string? Build()
    {
        if (_declarations.Count == 0) return null;

        var builder = new StringBuilder();
        foreach (var declaration in _declarations)
        {
            builder.Append(declaration.Key).Append(':').Append(declaration.Value).Append(';');
        }

        return builder.ToString();
    }

    public override string ToString() => Build() ?? string.Empty;
}

/// <summary>Ordered set of HTML attributes.</summary>
internal sealed class HtmlAttributes
{
    // A null value means a bare attribute; an empty string still renders as name="".
    private readonly List<KeyValuePair<string, string?>> _attributes = new();

    /// <summary>Adds an attribute. Null and empty values are dropped.</summary>
    public HtmlAttributes Add(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _attributes.Add(new KeyValuePair<string, string?>(name, value!));
        }

        return this;
    }

    /// <summary>Adds an attribute with no value, such as <c>controls</c>.</summary>
    public HtmlAttributes AddBoolean(string name)
    {
        _attributes.Add(new KeyValuePair<string, string?>(name, null));
        return this;
    }

    /// <summary>
    /// Adds an attribute, keeping it even when the value is empty. Used for attributes such as
    /// <c>alt</c>, where the empty form is meaningful to screen readers.
    /// </summary>
    public HtmlAttributes AddAllowingEmpty(string name, string? value)
    {
        _attributes.Add(new KeyValuePair<string, string?>(name, value ?? string.Empty));
        return this;
    }

    /// <summary>Adds the rendered declarations of <paramref name="style"/> as a style attribute.</summary>
    public HtmlAttributes AddStyle(StyleBuilder style) => Add("style", style.Build());

    /// <summary>Renders the attributes, each prefixed by a space.</summary>
    public string Build()
    {
        if (_attributes.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        foreach (var attribute in _attributes)
        {
            builder.Append(' ').Append(attribute.Key);
            if (attribute.Value is not null)
            {
                builder.Append("=\"").Append(HtmlEntities.Encode(attribute.Value)).Append('"');
            }
        }

        return builder.ToString();
    }

    public override string ToString() => Build();
}

/// <summary>
/// Accumulates the rendered HTML. Indentation is applied to markup the renderer emits, while
/// user content coming from ending tags is written through verbatim.
/// </summary>
internal sealed class HtmlWriter
{
    private readonly StringBuilder _builder = new();
    private readonly bool _beautify;
    private int _indent;
    private bool _atLineStart = true;

    public HtmlWriter(bool beautify)
    {
        _beautify = beautify;
    }

    /// <summary>Opens an element and increases indentation.</summary>
    public void Open(string tagName, HtmlAttributes? attributes = null)
    {
        WriteLine("<" + tagName + (attributes?.Build() ?? string.Empty) + ">");
        _indent++;
    }

    /// <summary>Closes an element and decreases indentation.</summary>
    public void Close(string tagName)
    {
        _indent--;
        WriteLine("</" + tagName + ">");
    }

    /// <summary>Writes a self-contained element with no children.</summary>
    public void SelfClosing(string tagName, HtmlAttributes? attributes = null)
        => WriteLine("<" + tagName + (attributes?.Build() ?? string.Empty) + " />");

    /// <summary>Writes an element with inline text content on a single line.</summary>
    public void Element(string tagName, HtmlAttributes? attributes, string? content)
    {
        WriteLine("<" + tagName + (attributes?.Build() ?? string.Empty) + ">"
                  + (content ?? string.Empty) + "</" + tagName + ">");
    }

    /// <summary>Writes a complete line of markup, honouring the current indentation.</summary>
    public void WriteLine(string markup)
    {
        if (markup.Length == 0) return;

        WriteIndent();
        _builder.Append(markup);
        NewLine();
    }

    /// <summary>Writes raw user content verbatim, without escaping or re-indenting it.</summary>
    public void WriteRaw(string? markup)
    {
        if (string.IsNullOrEmpty(markup)) return;

        WriteIndent();
        _builder.Append(markup);
        _atLineStart = false;
        NewLine();
    }

    /// <summary>Appends text without any indentation or line break.</summary>
    public void Append(string markup)
    {
        if (markup.Length == 0) return;
        _builder.Append(markup);
        _atLineStart = false;
    }

    /// <summary>Wraps <paramref name="markup"/> in a downlevel-revealed conditional comment for Outlook.</summary>
    public void OutlookConditional(string markup)
        => WriteLine("<!--[if mso | IE]>" + markup + "<![endif]-->");

    private void WriteIndent()
    {
        if (!_beautify || !_atLineStart) return;
        _builder.Append(' ', _indent * 2);
        _atLineStart = false;
    }

    private void NewLine()
    {
        if (_beautify) _builder.Append('\n');
        _atLineStart = true;
    }

    public override string ToString() => _builder.ToString();
}
