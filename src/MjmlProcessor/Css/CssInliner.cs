using System.Text;
using MjmlProcessor.Parsing;

namespace MjmlProcessor.Css;

/// <summary>Evaluates a parsed selector against an element in a rendered document.</summary>
internal static class SelectorMatcher
{
    public static bool Matches(HtmlElement element, ComplexSelector selector)
        => MatchesFrom(element, selector, selector.Compounds.Count - 1);

    /// <summary>Matches right to left, which lets the common cases fail fast.</summary>
    private static bool MatchesFrom(HtmlElement element, ComplexSelector selector, int index)
    {
        if (!MatchesCompound(element, selector.Compounds[index])) return false;
        if (index == 0) return true;

        var combinator = selector.Combinators[index - 1];

        switch (combinator)
        {
            case Combinator.Child:
                return element.Parent is not null && MatchesFrom(element.Parent, selector, index - 1);

            case Combinator.Descendant:
            {
                for (var ancestor = element.Parent; ancestor is not null; ancestor = ancestor.Parent)
                {
                    if (MatchesFrom(ancestor, selector, index - 1)) return true;
                }

                return false;
            }

            case Combinator.AdjacentSibling:
            {
                var previous = PreviousSibling(element);
                return previous is not null && MatchesFrom(previous, selector, index - 1);
            }

            case Combinator.GeneralSibling:
            {
                for (var sibling = PreviousSibling(element); sibling is not null; sibling = PreviousSibling(sibling))
                {
                    if (MatchesFrom(sibling, selector, index - 1)) return true;
                }

                return false;
            }

            default:
                return false;
        }
    }

    private static bool MatchesCompound(HtmlElement element, CompoundSelector compound)
    {
        if (compound.TagName is not null &&
            !element.TagName.Equals(compound.TagName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (compound.Id is not null && !string.Equals(element.GetAttribute("id"), compound.Id, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var className in compound.Classes)
        {
            if (!HasClass(element, className)) return false;
        }

        foreach (var attribute in compound.Attributes)
        {
            if (!MatchesAttribute(element, attribute)) return false;
        }

        foreach (var pseudo in compound.Pseudos)
        {
            if (!MatchesPseudo(element, pseudo)) return false;
        }

        return true;
    }

    private static bool HasClass(HtmlElement element, string className)
    {
        foreach (var candidate in element.Classes)
        {
            if (string.Equals(candidate, className, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static bool MatchesAttribute(HtmlElement element, AttributeSelector attribute)
    {
        var value = element.GetAttribute(attribute.Name);
        if (value is null) return false;
        if (attribute.Operator is null) return true;

        var expected = attribute.Value ?? string.Empty;

        switch (attribute.Operator)
        {
            case "=":
                return string.Equals(value, expected, StringComparison.Ordinal);

            case "~=":
                if (expected.Length == 0) return false;
                foreach (var part in value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (string.Equals(part, expected, StringComparison.Ordinal)) return true;
                }

                return false;

            case "|=":
                return string.Equals(value, expected, StringComparison.Ordinal) ||
                       value.StartsWith(expected + "-", StringComparison.Ordinal);

            case "^=":
                return expected.Length > 0 && value.StartsWith(expected, StringComparison.Ordinal);

            case "$=":
                return expected.Length > 0 && value.EndsWith(expected, StringComparison.Ordinal);

            case "*=":
                return expected.Length > 0 && value.IndexOf(expected, StringComparison.Ordinal) >= 0;

            default:
                return false;
        }
    }

    private static bool MatchesPseudo(HtmlElement element, PseudoSelector pseudo)
    {
        switch (pseudo.Kind)
        {
            case PseudoKind.FirstChild:
                return element.ChildIndex == 1;

            case PseudoKind.LastChild:
                return element.ChildIndex == SiblingCount(element);

            case PseudoKind.OnlyChild:
                return SiblingCount(element) == 1;

            case PseudoKind.FirstOfType:
                return TypeIndex(element, fromEnd: false) == 1;

            case PseudoKind.LastOfType:
                return TypeIndex(element, fromEnd: true) == 1;

            case PseudoKind.NthChild:
                return MatchesNth(element.ChildIndex, pseudo.Step, pseudo.Offset);

            case PseudoKind.NthLastChild:
                return MatchesNth(SiblingCount(element) - element.ChildIndex + 1, pseudo.Step, pseudo.Offset);

            case PseudoKind.Not:
            {
                if (pseudo.Negated is null) return false;

                foreach (var negated in pseudo.Negated)
                {
                    if (MatchesCompound(element, negated)) return false;
                }

                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Evaluates the <c>An+B</c> pattern for a 1-based position.</summary>
    private static bool MatchesNth(int position, int step, int offset)
    {
        if (position < 1) return false;
        if (step == 0) return position == offset;

        var delta = position - offset;
        if (delta == 0) return true;

        return Math.Sign(delta) == Math.Sign(step) && delta % step == 0;
    }

    private static int SiblingCount(HtmlElement element)
        => element.Parent?.Children.Count ?? 1;

    private static int TypeIndex(HtmlElement element, bool fromEnd)
    {
        var siblings = element.Parent?.Children;
        if (siblings is null) return 1;

        var index = 0;
        if (fromEnd)
        {
            for (var i = siblings.Count - 1; i >= 0; i--)
            {
                if (!siblings[i].TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase)) continue;
                index++;
                if (ReferenceEquals(siblings[i], element)) return index;
            }
        }
        else
        {
            foreach (var sibling in siblings)
            {
                if (!sibling.TagName.Equals(element.TagName, StringComparison.OrdinalIgnoreCase)) continue;
                index++;
                if (ReferenceEquals(sibling, element)) return index;
            }
        }

        return index;
    }

    private static HtmlElement? PreviousSibling(HtmlElement element)
    {
        var siblings = element.Parent?.Children;
        if (siblings is null) return null;

        var index = element.ChildIndex - 2;
        return index >= 0 && index < siblings.Count ? siblings[index] : null;
    }
}

/// <summary>
/// Merges stylesheet rules into the <c>style</c> attribute of each matching element, which is
/// what makes CSS survive clients such as Gmail that strip style blocks.
/// </summary>
internal static class CssInliner
{
    /// <summary>Where a declaration came from, ordered by how strongly it wins the cascade.</summary>
    private enum Origin
    {
        Stylesheet = 0,
        InlineAttribute = 1,
        ImportantStylesheet = 2,
        ImportantInlineAttribute = 3,
    }

    private readonly struct Candidate
    {
        public Candidate(string value, Origin origin, int specificity, int order)
        {
            Value = value;
            Origin = origin;
            Specificity = specificity;
            Order = order;
        }

        public string Value { get; }

        public Origin Origin { get; }

        public int Specificity { get; }

        public int Order { get; }

        /// <summary>True when this declaration should replace <paramref name="other"/>.</summary>
        public bool Beats(Candidate other)
        {
            if (Origin != other.Origin) return Origin > other.Origin;
            if (Specificity != other.Specificity) return Specificity > other.Specificity;
            return Order >= other.Order;
        }
    }

    /// <summary>
    /// Merges <paramref name="sheet"/> into the style attributes of <paramref name="html"/>.
    /// Everything the rules do not touch is returned byte for byte.
    /// </summary>
    public static string Apply(string html, CssStylesheet sheet)
    {
        if (sheet.Rules.Count == 0 || string.IsNullOrEmpty(html)) return html;

        var elements = HtmlDocument.Parse(html);
        if (elements.Count == 0) return html;

        var edits = new List<Edit>();

        foreach (var element in elements)
        {
            var merged = BuildStyle(element, sheet.Rules);
            if (merged is null) continue;

            edits.Add(element.HasStyle
                ? new Edit(element.StyleValueStart, element.StyleValueEnd, HtmlEntities.Encode(merged))
                : new Edit(element.AttributeInsertPosition, element.AttributeInsertPosition,
                    " style=\"" + HtmlEntities.Encode(merged) + "\""));
        }

        return ApplyEdits(html, edits);
    }

    /// <summary>
    /// Resolves the cascade for one element and returns its new style attribute, or null when
    /// nothing matched and the element should be left exactly as it was.
    /// </summary>
    private static string? BuildStyle(HtmlElement element, List<CssRule> rules)
    {
        var winners = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
        var propertyOrder = new List<string>();
        var matched = false;

        foreach (var rule in rules)
        {
            if (!SelectorMatcher.Matches(element, rule.Selector)) continue;

            matched = true;

            foreach (var declaration in rule.Declarations)
            {
                Offer(winners, propertyOrder, declaration.Property,
                    new Candidate(
                        declaration.Value,
                        declaration.IsImportant ? Origin.ImportantStylesheet : Origin.Stylesheet,
                        rule.Selector.Specificity,
                        rule.Order));
            }
        }

        if (!matched) return null;

        // An existing style attribute outranks the stylesheet unless the rule is !important.
        if (element.HasStyle && element.Attributes.TryGetValue("style", out var existing))
        {
            var inline = CssParser.ParseDeclarations(existing);
            for (var i = 0; i < inline.Count; i++)
            {
                Offer(winners, propertyOrder, inline[i].Property,
                    new Candidate(
                        inline[i].Value,
                        inline[i].IsImportant ? Origin.ImportantInlineAttribute : Origin.InlineAttribute,
                        int.MaxValue,
                        i));
            }
        }

        var builder = new StringBuilder();
        foreach (var property in propertyOrder)
        {
            var candidate = winners[property];
            builder.Append(property).Append(':').Append(candidate.Value);

            if (candidate.Origin is Origin.ImportantStylesheet or Origin.ImportantInlineAttribute)
            {
                builder.Append(" !important");
            }

            builder.Append(';');
        }

        return builder.ToString();
    }

    private static void Offer(
        Dictionary<string, Candidate> winners, List<string> propertyOrder, string property, Candidate candidate)
    {
        if (!winners.TryGetValue(property, out var current))
        {
            winners[property] = candidate;
            propertyOrder.Add(property);
            return;
        }

        if (candidate.Beats(current)) winners[property] = candidate;
    }

    private readonly struct Edit
    {
        public Edit(int start, int end, string replacement)
        {
            Start = start;
            End = end;
            Replacement = replacement;
        }

        public int Start { get; }

        public int End { get; }

        public string Replacement { get; }
    }

    /// <summary>Splices the recorded edits into the source, leaving everything else untouched.</summary>
    private static string ApplyEdits(string html, List<Edit> edits)
    {
        if (edits.Count == 0) return html;

        edits.Sort((a, b) => a.Start.CompareTo(b.Start));

        var builder = new StringBuilder(html.Length + (edits.Count * 32));
        var position = 0;

        foreach (var edit in edits)
        {
            if (edit.Start < position) continue;

            builder.Append(html, position, edit.Start - position);
            builder.Append(edit.Replacement);
            position = edit.End;
        }

        builder.Append(html, position, html.Length - position);
        return builder.ToString();
    }
}
