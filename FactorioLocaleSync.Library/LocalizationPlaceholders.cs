using System.Linq;
using System.Text.RegularExpressions;

namespace FactorioLocaleSync.Library;

/// <summary>
///     Detects Factorio localisation strings that carry no human-translatable text, i.e. strings built
///     entirely out of localisation placeholders (e.g. <c>__ITEM__iron-plate__</c>) and/or rich-text
///     tags (e.g. <c>[img=item/iron-plate]</c>).
///     <para />
///     Such strings resolve to an already-localised name at runtime, so there is nothing for a translator
///     to do — the result is identical in every language.
///     <para />
///     See https://wiki.factorio.com/Tutorial:Localisation for the placeholder grammar.
/// </summary>
public static class LocalizationPlaceholders
{
    // Localisation placeholders, all delimited by double underscores.
    // Order matters: parameterised built-ins must be tried before the generic __UPPER__ fallback so
    // that e.g. __ITEM__iron-plate__ is consumed whole instead of leaving "iron-plate__" behind.
    private const string PlaceholderPattern =
        @"__\d+__" + // positional parameter: __1__
        @"|__ALT_CONTROL__\d+__(?:(?!__).)+__" + // __ALT_CONTROL__n__name__
        @"|__ALT_CONTROL_(?:LEFT|RIGHT)_CLICK__\d+__" + // __ALT_CONTROL_LEFT_CLICK__n__
        @"|__(?:CONTROL_MODIFIER|CONTROL|ENTITY|FLUID|ITEM|PLANET|TILE)__(?:(?!__).)+__" + // parameterised built-ins with a name
        @"|__(?:CONTROL_STYLE_BEGIN|CONTROL_STYLE_END" +
        @"|CONTROL_LEFT_CLICK|CONTROL_RIGHT_CLICK" +
        @"|CONTROL_KEY_SHIFT|CONTROL_KEY_CTRL|CONTROL_MOVE" +
        @"|REMARK_COLOR_BEGIN|REMARK_COLOR_END)__" + // standalone built-ins
        @"|__[A-Z][A-Z0-9_]*__"; // any other standalone __UPPER__ token

    // Rich-text tags: [img=item/x], [item=x], [color=255,0,0], [font=default-bold], [/color], [gps=1,2,nauvis] ...
    private const string RichTextPattern = @"\[/?[a-zA-Z][a-zA-Z0-9-]*(?:=[^\]]*)?\]";

    private static readonly Regex PlaceholderRegex = new(PlaceholderPattern, RegexOptions.Compiled);
    private static readonly Regex RichTextRegex = new(RichTextPattern, RegexOptions.Compiled);

    /// <summary>
    ///     Returns <paramref name="value" /> with every localization placeholder and rich-text tag removed,
    ///     leaving only the literal text a translator would actually translate.
    /// </summary>
    public static string StripNonTranslatable(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        value = PlaceholderRegex.Replace(value, string.Empty);
        value = RichTextRegex.Replace(value, string.Empty);
        return value;
    }

    /// <summary>
    ///     <c>true</c> when <paramref name="value" /> contains at least one placeholder or rich-text tag and
    ///     has no real (letter-bearing) text left once those are removed. Whitespace, digits, and punctuation
    ///     are not considered translatable text, so e.g. <c>"__ITEM__x__ - __ITEM__y__"</c> is placeholder-only,
    ///     while <c>"__ENTITY__x__ deploy"</c> is not.
    ///     <para />
    ///     Strings that contain no placeholder at all (plain text, a bare number, a lone separator) return
    ///     <c>false</c> — there is nothing placeholder-like about them.
    /// </summary>
    public static bool IsPlaceholderOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var containsPlaceholder = PlaceholderRegex.IsMatch(value) || RichTextRegex.IsMatch(value);
        if (!containsPlaceholder) return false;

        var residual = StripNonTranslatable(value);
        return !residual.Any(char.IsLetter);
    }
}