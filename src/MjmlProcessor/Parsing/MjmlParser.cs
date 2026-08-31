using System.Text;

namespace MjmlProcessor.Parsing;

/// <summary>
/// A tolerant MJML parser. MJML is XML-like, but the bodies of "ending tags" (mj-text,
/// mj-button, mj-raw, ...) contain arbitrary HTML that is not necessarily well formed,
/// so those bodies are captured verbatim rather than parsed.
/// </summary>
public static class MjmlParser
{
    /// <summary>Tags whose inner markup is captured verbatim instead of being parsed.</summary>
    internal static readonly HashSet<string> EndingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "mj-text",
        "mj-button",
        "mj-raw",
        "mj-style",
        "mj-title",
        "mj-preview",
        "mj-table",
        "mj-social-element",
        "mj-navbar-link",
        "mj-accordion-text",
        "mj-accordion-title",
    };

    /// <summary>Parses <paramref name="source"/> into a document tree.</summary>
    /// <exception cref="MjmlParseException">The source is not valid MJML.</exception>
    public static MjmlNode Parse(string source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var reader = new Reader(source);
        var roots = ParseNodes(reader, null);

        foreach (var node in roots)
        {
            if (node.TagName.Equals("mjml", StringComparison.OrdinalIgnoreCase)) return node;
        }

        // Be forgiving: a bare <mj-body> or a naked fragment is wrapped in a synthetic root.
        var synthetic = new MjmlNode("mjml", 1, 1);
        foreach (var node in roots) synthetic.Children.Add(node);
        return synthetic;
    }

    private static List<MjmlNode> ParseNodes(Reader reader, string? parentTag)
    {
        var nodes = new List<MjmlNode>();

        while (!reader.AtEnd)
        {
            if (reader.Peek() != '<')
            {
                reader.SkipUntil('<');
                continue;
            }

            if (reader.StartsWith("<!--"))
            {
                if (!reader.SkipPast("-->")) reader.SkipToEnd();
                continue;
            }

            if (reader.StartsWith("<!") || reader.StartsWith("<?"))
            {
                if (!reader.SkipPast(">")) reader.SkipToEnd();
                continue;
            }

            if (reader.StartsWith("</"))
            {
                var closeLine = reader.Line;
                var closeColumn = reader.Column;
                reader.Advance(2);
                var closeName = reader.ReadName();
                reader.SkipWhitespace();
                if (reader.Peek() == '>') reader.Advance(1);

                // A stray closing tag at the top level is ignored rather than fatal.
                if (parentTag is null) continue;

                if (!closeName.Equals(parentTag, StringComparison.OrdinalIgnoreCase))
                {
                    throw new MjmlParseException(
                        "Unexpected closing tag </" + closeName + ">, expected </" + parentTag + ">.",
                        closeLine, closeColumn);
                }

                return nodes;
            }

            nodes.Add(ParseElement(reader));
        }

        if (parentTag is not null)
        {
            throw new MjmlParseException("Unclosed tag <" + parentTag + ">.", reader.Line, reader.Column);
        }

        return nodes;
    }

    private static MjmlNode ParseElement(Reader reader)
    {
        var line = reader.Line;
        var column = reader.Column;
        reader.Advance(1); // consume '<'

        var name = reader.ReadName();
        if (name.Length == 0)
        {
            throw new MjmlParseException("Expected a tag name after '<'.", line, column);
        }

        var node = new MjmlNode(name.ToLowerInvariant(), line, column);
        var selfClosing = ReadAttributes(reader, node);

        if (selfClosing) return node;

        if (EndingTags.Contains(node.TagName))
        {
            node.Content = ReadRawContent(reader, node.TagName, line, column);
            return node;
        }

        foreach (var child in ParseNodes(reader, node.TagName))
        {
            node.Children.Add(child);
        }

        return node;
    }

    /// <summary>Reads attributes up to and including the closing angle bracket. Returns true if self-closing.</summary>
    private static bool ReadAttributes(Reader reader, MjmlNode node)
    {
        while (true)
        {
            reader.SkipWhitespace();

            if (reader.AtEnd)
            {
                throw new MjmlParseException("Unclosed tag <" + node.TagName + ">.", node.Line, node.Column);
            }

            var c = reader.Peek();

            if (c == '/')
            {
                reader.Advance(1);
                reader.SkipWhitespace();
                if (reader.Peek() == '>') reader.Advance(1);
                return true;
            }

            if (c == '>')
            {
                reader.Advance(1);
                return false;
            }

            var attributeName = reader.ReadAttributeName();
            if (attributeName.Length == 0)
            {
                // Not a character we understand; skip it so a stray symbol cannot hang the parser.
                reader.Advance(1);
                continue;
            }

            reader.SkipWhitespace();

            var value = string.Empty;
            if (reader.Peek() == '=')
            {
                reader.Advance(1);
                reader.SkipWhitespace();
                value = reader.ReadAttributeValue();
            }

            node.Attributes[attributeName] = HtmlEntities.Decode(value);
        }
    }

    private static string ReadRawContent(Reader reader, string tagName, int line, int column)
    {
        var start = reader.Position;
        var depth = 1;

        while (!reader.AtEnd)
        {
            if (reader.Peek() != '<')
            {
                reader.Advance(1);
                continue;
            }

            if (reader.StartsWith("<!--"))
            {
                var commentStart = reader.Position;
                if (!reader.SkipPast("-->"))
                {
                    reader.Reset(commentStart);
                    reader.Advance(1);
                }

                continue;
            }

            if (reader.StartsWith("</") && reader.MatchesTagNameAt(reader.Position + 2, tagName))
            {
                depth--;
                if (depth == 0)
                {
                    var content = reader.Slice(start, reader.Position);
                    reader.Advance(2);
                    reader.ReadName();
                    reader.SkipWhitespace();
                    if (reader.Peek() == '>') reader.Advance(1);
                    return content;
                }

                reader.Advance(2);
                continue;
            }

            if (reader.MatchesTagNameAt(reader.Position + 1, tagName))
            {
                // A nested element with the same name; track it so the right closer is matched.
                var tagStart = reader.Position;
                reader.Advance(1);
                reader.ReadName();
                if (!SkipToTagEnd(reader)) depth++;
                if (reader.Position == tagStart) reader.Advance(1);
                continue;
            }

            reader.Advance(1);
        }

        throw new MjmlParseException("Unclosed tag <" + tagName + ">.", line, column);
    }

    /// <summary>Skips past the end of the current tag. Returns true when it was self-closing.</summary>
    private static bool SkipToTagEnd(Reader reader)
    {
        var quote = '\0';

        while (!reader.AtEnd)
        {
            var c = reader.Peek();

            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                reader.Advance(1);
                continue;
            }

            if (c == '"' || c == '\'')
            {
                quote = c;
                reader.Advance(1);
                continue;
            }

            if (c == '>')
            {
                var selfClosing = reader.CharAt(reader.Position - 1) == '/';
                reader.Advance(1);
                return selfClosing;
            }

            reader.Advance(1);
        }

        return false;
    }

    private sealed class Reader
    {
        private readonly string _source;

        public Reader(string source)
        {
            _source = source;
            Line = 1;
            Column = 1;
        }

        public int Position { get; private set; }

        public int Line { get; private set; }

        public int Column { get; private set; }

        public bool AtEnd => Position >= _source.Length;

        public char Peek() => Position < _source.Length ? _source[Position] : '\0';

        public char CharAt(int index) => index >= 0 && index < _source.Length ? _source[index] : '\0';

        public string Slice(int start, int end) => _source.Substring(start, end - start);

        /// <summary>Rewinds to a previously captured position. Line and column are recomputed.</summary>
        public void Reset(int position)
        {
            if (position > Position)
            {
                Advance(position - Position);
                return;
            }

            Position = 0;
            Line = 1;
            Column = 1;
            Advance(position);
        }

        public void Advance(int count)
        {
            for (var i = 0; i < count && Position < _source.Length; i++)
            {
                if (_source[Position] == '\n')
                {
                    Line++;
                    Column = 1;
                }
                else
                {
                    Column++;
                }

                Position++;
            }
        }

        public void SkipToEnd() => Advance(_source.Length - Position);

        public bool StartsWith(string value)
            => Position + value.Length <= _source.Length
               && string.CompareOrdinal(_source, Position, value, 0, value.Length) == 0;

        public void SkipUntil(char c)
        {
            while (!AtEnd && Peek() != c) Advance(1);
        }

        public bool SkipPast(string terminator)
        {
            var index = _source.IndexOf(terminator, Position, StringComparison.Ordinal);
            if (index < 0) return false;
            Advance(index + terminator.Length - Position);
            return true;
        }

        public void SkipWhitespace()
        {
            while (!AtEnd && char.IsWhiteSpace(Peek())) Advance(1);
        }

        public string ReadName()
        {
            var start = Position;
            while (!AtEnd)
            {
                var c = Peek();
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':' || c == '.') Advance(1);
                else break;
            }

            return _source.Substring(start, Position - start);
        }

        public string ReadAttributeName()
        {
            var start = Position;
            while (!AtEnd)
            {
                var c = Peek();
                if (char.IsWhiteSpace(c) || c == '=' || c == '>' || c == '/' || c == '<' || c == '"' || c == '\'') break;
                Advance(1);
            }

            return _source.Substring(start, Position - start);
        }

        public string ReadAttributeValue()
        {
            var c = Peek();
            if (c == '"' || c == '\'')
            {
                Advance(1);
                var quotedStart = Position;
                while (!AtEnd && Peek() != c) Advance(1);
                var quoted = _source.Substring(quotedStart, Position - quotedStart);
                if (!AtEnd) Advance(1);
                return quoted;
            }

            var start = Position;
            while (!AtEnd)
            {
                var current = Peek();
                if (char.IsWhiteSpace(current) || current == '>' || current == '/') break;
                Advance(1);
            }

            return _source.Substring(start, Position - start);
        }

        /// <summary>True when <paramref name="tagName"/> starts at <paramref name="index"/> and ends at a delimiter.</summary>
        public bool MatchesTagNameAt(int index, string tagName)
        {
            if (index + tagName.Length > _source.Length) return false;
            if (string.Compare(_source, index, tagName, 0, tagName.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            var next = CharAt(index + tagName.Length);
            return next == '\0' || char.IsWhiteSpace(next) || next == '>' || next == '/';
        }
    }
}

/// <summary>Minimal HTML entity handling for attribute values.</summary>
internal static class HtmlEntities
{
    public static string Decode(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('&') < 0) return value;

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '&')
            {
                builder.Append(value[i]);
                continue;
            }

            var end = value.IndexOf(';', i + 1);
            if (end < 0 || end - i > 10)
            {
                builder.Append('&');
                continue;
            }

            var replacement = Resolve(value.Substring(i + 1, end - i - 1));
            if (replacement is null)
            {
                builder.Append('&');
                continue;
            }

            builder.Append(replacement);
            i = end;
        }

        return builder.ToString();
    }

    private static string? Resolve(string entity)
    {
        switch (entity)
        {
            case "amp": return "&";
            case "lt": return "<";
            case "gt": return ">";
            case "quot": return "\"";
            case "apos": return "'";
            case "nbsp": return " ";
        }

        if (entity.Length > 1 && entity[0] == '#')
        {
            var digits = entity.Substring(1);
            var isHex = digits.Length > 1 && (digits[0] == 'x' || digits[0] == 'X');
            if (isHex) digits = digits.Substring(1);

            try
            {
                var code = Convert.ToInt32(digits, isHex ? 16 : 10);
                if (code > 0 && code <= 0x10FFFF) return char.ConvertFromUtf32(code);
            }
            catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
            {
                return null;
            }
        }

        return null;
    }

    public static string Encode(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                default: builder.Append(c); break;
            }
        }

        return builder.ToString();
    }
}
