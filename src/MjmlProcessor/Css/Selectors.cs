namespace MjmlProcessor.Css;

/// <summary>How a compound selector relates to the one on its left.</summary>
internal enum Combinator
{
    Descendant,
    Child,
    AdjacentSibling,
    GeneralSibling,
}

/// <summary>An attribute test such as <c>[href]</c> or <c>[type="text"]</c>.</summary>
internal sealed class AttributeSelector
{
    public AttributeSelector(string name, string? op, string? value)
    {
        Name = name;
        Operator = op;
        Value = value;
    }

    public string Name { get; }

    /// <summary>One of <c>=</c>, <c>~=</c>, <c>|=</c>, <c>^=</c>, <c>$=</c>, <c>*=</c>, or null for presence.</summary>
    public string? Operator { get; }

    public string? Value { get; }
}

/// <summary>The structural pseudo-classes that can be evaluated without a live document.</summary>
internal enum PseudoKind
{
    FirstChild,
    LastChild,
    OnlyChild,
    FirstOfType,
    LastOfType,
    NthChild,
    NthLastChild,
    Not,
}

/// <summary>A structural pseudo-class, optionally carrying an <c>An+B</c> pattern or a negation.</summary>
internal sealed class PseudoSelector
{
    public PseudoSelector(PseudoKind kind) => Kind = kind;

    public PseudoKind Kind { get; }

    /// <summary>The <c>A</c> of an <c>An+B</c> pattern.</summary>
    public int Step { get; set; }

    /// <summary>The <c>B</c> of an <c>An+B</c> pattern.</summary>
    public int Offset { get; set; }

    /// <summary>The argument list of <c>:not(...)</c>.</summary>
    public List<CompoundSelector>? Negated { get; set; }
}

/// <summary>A run of simple selectors with no combinator between them, such as <c>a.btn[href]</c>.</summary>
internal sealed class CompoundSelector
{
    /// <summary>The element name, or null for <c>*</c> and for compounds with no type.</summary>
    public string? TagName { get; set; }

    public string? Id { get; set; }

    public List<string> Classes { get; } = new();

    public List<AttributeSelector> Attributes { get; } = new();

    public List<PseudoSelector> Pseudos { get; } = new();
}

/// <summary>A full selector: compounds joined by combinators, stored left to right.</summary>
internal sealed class ComplexSelector
{
    public ComplexSelector(string text) => Text = text;

    /// <summary>The original selector text.</summary>
    public string Text { get; }

    public List<CompoundSelector> Compounds { get; } = new();

    /// <summary>Combinators between compounds. Always one shorter than <see cref="Compounds"/>.</summary>
    public List<Combinator> Combinators { get; } = new();

    /// <summary>CSS specificity, packed so that a plain integer comparison orders correctly.</summary>
    public int Specificity { get; set; }
}

/// <summary>
/// Parses the selector syntax that can be resolved statically. Anything else — <c>:hover</c>,
/// <c>::before</c>, and other dynamic or unsupported constructs — is rejected so the caller
/// can preserve the rule in a style block instead of silently dropping it.
/// </summary>
internal static class SelectorParser
{
    public static bool TryParse(string text, out ComplexSelector? selector)
    {
        selector = null;

        var result = new ComplexSelector(text);
        var position = 0;
        var expectCompound = true;
        var ids = 0;
        var classes = 0;
        var types = 0;

        while (position < text.Length)
        {
            SkipWhitespace(text, ref position, out var sawWhitespace);
            if (position >= text.Length) break;

            var c = text[position];

            if (c == '>' || c == '+' || c == '~')
            {
                if (result.Compounds.Count == 0) return false;

                position++;
                result.Combinators.Add(c switch
                {
                    '>' => Combinator.Child,
                    '+' => Combinator.AdjacentSibling,
                    _ => Combinator.GeneralSibling,
                });

                expectCompound = true;
                continue;
            }

            if (sawWhitespace && !expectCompound)
            {
                result.Combinators.Add(Combinator.Descendant);
                expectCompound = true;
            }

            if (!expectCompound) return false;

            if (!TryParseCompound(text, ref position, out var compound, ref ids, ref classes, ref types))
            {
                return false;
            }

            result.Compounds.Add(compound!);
            expectCompound = false;
        }

        if (result.Compounds.Count == 0) return false;
        if (result.Combinators.Count != result.Compounds.Count - 1) return false;

        result.Specificity = (ids * 1_000_000) + (classes * 1_000) + types;
        selector = result;
        return true;
    }

    private static bool TryParseCompound(
        string text, ref int position, out CompoundSelector? compound,
        ref int ids, ref int classes, ref int types)
    {
        compound = null;
        var result = new CompoundSelector();
        var consumed = false;

        while (position < text.Length)
        {
            var c = text[position];

            if (c == '*')
            {
                position++;
                consumed = true;
                continue;
            }

            if (c == '#')
            {
                position++;
                var id = ReadIdentifier(text, ref position);
                if (id.Length == 0) return false;
                result.Id = id;
                ids++;
                consumed = true;
                continue;
            }

            if (c == '.')
            {
                position++;
                var name = ReadIdentifier(text, ref position);
                if (name.Length == 0) return false;
                result.Classes.Add(name);
                classes++;
                consumed = true;
                continue;
            }

            if (c == '[')
            {
                if (!TryParseAttribute(text, ref position, out var attribute)) return false;
                result.Attributes.Add(attribute!);
                classes++;
                consumed = true;
                continue;
            }

            if (c == ':')
            {
                if (!TryParsePseudo(text, ref position, out var pseudo, ref classes)) return false;
                result.Pseudos.Add(pseudo!);
                consumed = true;
                continue;
            }

            if (IsIdentifierStart(c))
            {
                if (result.TagName is not null) return false;
                result.TagName = ReadIdentifier(text, ref position).ToLowerInvariant();
                types++;
                consumed = true;
                continue;
            }

            break;
        }

        if (!consumed) return false;

        compound = result;
        return true;
    }

    private static bool TryParseAttribute(string text, ref int position, out AttributeSelector? attribute)
    {
        attribute = null;
        position++; // consume '['

        SkipWhitespace(text, ref position, out _);
        var name = ReadIdentifier(text, ref position);
        if (name.Length == 0) return false;

        SkipWhitespace(text, ref position, out _);
        if (position >= text.Length) return false;

        if (text[position] == ']')
        {
            position++;
            attribute = new AttributeSelector(name.ToLowerInvariant(), null, null);
            return true;
        }

        string op;
        var c = text[position];
        if (c == '=')
        {
            op = "=";
            position++;
        }
        else if ((c == '~' || c == '|' || c == '^' || c == '$' || c == '*') &&
                 position + 1 < text.Length && text[position + 1] == '=')
        {
            op = c + "=";
            position += 2;
        }
        else
        {
            return false;
        }

        SkipWhitespace(text, ref position, out _);
        if (position >= text.Length) return false;

        string value;
        var quote = text[position];
        if (quote == '"' || quote == '\'')
        {
            position++;
            var start = position;
            while (position < text.Length && text[position] != quote) position++;
            if (position >= text.Length) return false;
            value = text.Substring(start, position - start);
            position++;
        }
        else
        {
            var start = position;
            while (position < text.Length && text[position] != ']' && !char.IsWhiteSpace(text[position])) position++;
            value = text.Substring(start, position - start);
        }

        SkipWhitespace(text, ref position, out _);

        // An optional case-insensitivity flag is accepted but not honoured.
        if (position < text.Length && (text[position] == 'i' || text[position] == 'I'))
        {
            position++;
            SkipWhitespace(text, ref position, out _);
        }

        if (position >= text.Length || text[position] != ']') return false;
        position++;

        attribute = new AttributeSelector(name.ToLowerInvariant(), op, value);
        return true;
    }

    private static bool TryParsePseudo(string text, ref int position, out PseudoSelector? pseudo, ref int classes)
    {
        pseudo = null;
        position++; // consume ':'

        // A pseudo-element can never be represented by an inline style attribute.
        if (position < text.Length && text[position] == ':') return false;

        var name = ReadIdentifier(text, ref position).ToLowerInvariant();
        if (name.Length == 0) return false;

        string? argument = null;
        if (position < text.Length && text[position] == '(')
        {
            var depth = 0;
            var start = position + 1;

            while (position < text.Length)
            {
                if (text[position] == '(') depth++;
                else if (text[position] == ')')
                {
                    depth--;
                    if (depth == 0) break;
                }

                position++;
            }

            if (position >= text.Length) return false;
            argument = text.Substring(start, position - start);
            position++; // consume ')'
        }

        classes++;

        switch (name)
        {
            case "first-child":
                pseudo = new PseudoSelector(PseudoKind.FirstChild);
                return argument is null;
            case "last-child":
                pseudo = new PseudoSelector(PseudoKind.LastChild);
                return argument is null;
            case "only-child":
                pseudo = new PseudoSelector(PseudoKind.OnlyChild);
                return argument is null;
            case "first-of-type":
                pseudo = new PseudoSelector(PseudoKind.FirstOfType);
                return argument is null;
            case "last-of-type":
                pseudo = new PseudoSelector(PseudoKind.LastOfType);
                return argument is null;

            case "nth-child":
            case "nth-last-child":
            {
                if (argument is null) return false;
                if (!TryParseNth(argument, out var step, out var offset)) return false;

                pseudo = new PseudoSelector(name == "nth-child" ? PseudoKind.NthChild : PseudoKind.NthLastChild)
                {
                    Step = step,
                    Offset = offset,
                };

                return true;
            }

            case "not":
            {
                if (argument is null) return false;

                var negated = new List<CompoundSelector>();
                foreach (var part in CssParser.SplitTopLevel(argument, ','))
                {
                    var trimmed = part.Trim();
                    if (trimmed.Length == 0) return false;

                    var innerPosition = 0;
                    int innerIds = 0, innerClasses = 0, innerTypes = 0;
                    if (!TryParseCompound(trimmed, ref innerPosition, out var inner,
                            ref innerIds, ref innerClasses, ref innerTypes))
                    {
                        return false;
                    }

                    // Only a simple compound is supported inside :not(); a combinator is not.
                    if (innerPosition != trimmed.Length) return false;
                    negated.Add(inner!);
                }

                pseudo = new PseudoSelector(PseudoKind.Not) { Negated = negated };
                return true;
            }

            default:
                // :hover, :focus, :checked and friends depend on state we cannot resolve.
                return false;
        }
    }

    /// <summary>Parses the <c>An+B</c> microsyntax, including the <c>odd</c> and <c>even</c> keywords.</summary>
    private static bool TryParseNth(string argument, out int step, out int offset)
    {
        step = 0;
        offset = 0;

        var text = argument.Replace(" ", string.Empty).ToLowerInvariant();
        if (text.Length == 0) return false;

        if (text == "odd")
        {
            step = 2;
            offset = 1;
            return true;
        }

        if (text == "even")
        {
            step = 2;
            offset = 0;
            return true;
        }

        var nIndex = text.IndexOf('n');
        if (nIndex < 0) return int.TryParse(text, out offset);

        var stepText = text.Substring(0, nIndex);
        if (stepText.Length == 0 || stepText == "+") step = 1;
        else if (stepText == "-") step = -1;
        else if (!int.TryParse(stepText, out step)) return false;

        var offsetText = text.Substring(nIndex + 1);
        if (offsetText.Length == 0) return true;

        return int.TryParse(offsetText, out offset);
    }

    private static void SkipWhitespace(string text, ref int position, out bool sawWhitespace)
    {
        sawWhitespace = false;
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            sawWhitespace = true;
            position++;
        }
    }

    private static string ReadIdentifier(string text, ref int position)
    {
        var start = position;

        while (position < text.Length)
        {
            var c = text[position];
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '\\' || c >= 0x80) position++;
            else break;
        }

        return text.Substring(start, position - start);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_' || c >= 0x80;
}
