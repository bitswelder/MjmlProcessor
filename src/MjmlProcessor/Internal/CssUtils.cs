using System.Globalization;

namespace MjmlProcessor.Internal;

/// <summary>A CSS length such as <c>600px</c> or <c>50%</c>.</summary>
internal readonly struct CssSize
{
    public CssSize(double value, string unit)
    {
        Value = value;
        Unit = unit;
    }

    public double Value { get; }

    public string Unit { get; }

    public bool IsPercent => Unit == "%";

    public override string ToString() => CssUtils.Number(Value) + Unit;
}

/// <summary>Helpers for parsing and formatting the CSS values MJML works with.</summary>
internal static class CssUtils
{
    /// <summary>Formats a number the way CSS expects: invariant, no trailing zeros.</summary>
    public static string Number(double value)
    {
        var rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) < 0.000001) rounded = 0;
        return rounded.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>Formats a pixel length, for example <c>550px</c>.</summary>
    public static string Px(double value) => Number(value) + "px";

    /// <summary>Parses a CSS length. Unitless values are treated as pixels.</summary>
    public static CssSize ParseSize(string? value, double fallback = 0, string fallbackUnit = "px")
    {
        if (string.IsNullOrWhiteSpace(value)) return new CssSize(fallback, fallbackUnit);

        var text = value!.Trim();
        var end = 0;
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.' || text[end] == '-' || text[end] == '+'))
        {
            end++;
        }

        if (end == 0) return new CssSize(fallback, fallbackUnit);

        if (!double.TryParse(text.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return new CssSize(fallback, fallbackUnit);
        }

        var unit = text.Substring(end).Trim();
        if (unit.Length == 0) unit = "px";

        return new CssSize(number, unit);
    }

    /// <summary>Parses a length and returns its numeric part only.</summary>
    public static double ParseNumber(string? value, double fallback = 0) => ParseSize(value, fallback).Value;

    /// <summary>
    /// Resolves one side of a CSS box shorthand. <paramref name="specific"/> (for example
    /// <c>padding-top</c>) wins over the matching slot of <paramref name="shorthand"/>.
    /// </summary>
    public static string? BoxSide(string? shorthand, string? specific, BoxSide side)
    {
        if (!string.IsNullOrEmpty(specific)) return specific;
        if (string.IsNullOrWhiteSpace(shorthand)) return null;

        var parts = shorthand!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        return side switch
        {
            Internal.BoxSide.Top => parts[0],
            Internal.BoxSide.Right => parts.Length > 1 ? parts[1] : parts[0],
            Internal.BoxSide.Bottom => parts.Length > 2 ? parts[2] : parts[0],
            Internal.BoxSide.Left => parts.Length > 3 ? parts[3] : parts.Length > 1 ? parts[1] : parts[0],
            _ => null,
        };
    }

    /// <summary>Extracts the width component of a CSS border shorthand such as <c>1px solid red</c>.</summary>
    public static double BorderWidth(string? border)
    {
        if (string.IsNullOrWhiteSpace(border)) return 0;
        if (border!.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return 0;

        foreach (var part in border.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length > 0 && (char.IsDigit(part[0]) || part[0] == '.'))
            {
                return ParseNumber(part);
            }
        }

        return 0;
    }

    /// <summary>Splits a whitespace separated class list, tolerating null and extra spaces.</summary>
    public static string[] SplitClasses(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Joins non-empty class names into a single attribute value, or null when there are none.</summary>
    public static string? JoinClasses(params string?[] classes)
    {
        var kept = new List<string>();
        foreach (var value in classes)
        {
            if (!string.IsNullOrWhiteSpace(value)) kept.Add(value!.Trim());
        }

        return kept.Count == 0 ? null : string.Join(" ", kept);
    }

    /// <summary>Suffixes each class in a list, used to build the Outlook-only class names.</summary>
    public static string? SuffixClasses(string? classes, string suffix)
    {
        var parts = SplitClasses(classes);
        if (parts.Length == 0) return null;

        for (var i = 0; i < parts.Length; i++) parts[i] += suffix;
        return string.Join(" ", parts);
    }
}

/// <summary>One side of a CSS box shorthand.</summary>
internal enum BoxSide
{
    Top,
    Right,
    Bottom,
    Left,
}
