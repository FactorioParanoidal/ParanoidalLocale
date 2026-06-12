using Xunit;

namespace FactorioLocaleSync.Library.Tests;

public class LocalizationPlaceholdersTests
{
    [Theory]
    // Single built-in with a name, nothing else but a separator / whitespace.
    [InlineData("__ITEM__iron-plate__")]
    [InlineData("__ITEM__atomic-artillery-shell__")]
    [InlineData("__ENTITY__angels-cab__")]
    [InlineData("  __FLUID__water__  ")]
    // Several placeholders glued together with punctuation only.
    [InlineData("__ITEM__angels-wire-gold__")]
    [InlineData("__ENTITY__x__ - __ENTITY__y__")]
    [InlineData("__ITEM__a__, __ITEM__b__, __ITEM__c__")]
    // Positional parameters with no surrounding text.
    [InlineData("__1__")]
    [InlineData("__1__ __2__")]
    // Standalone control built-ins.
    [InlineData("__CONTROL_MOVE__")]
    [InlineData("__CONTROL__open-technology-gui__")]
    // Rich text only.
    [InlineData("[img=item/iron-plate]")]
    [InlineData("[item=iron-plate] __ITEM__iron-plate__")]
    public void IsPlaceholderOnly_DetectsPlaceholderOnlyStrings(string value)
    {
        Assert.True(LocalizationPlaceholders.IsPlaceholderOnly(value), $"expected placeholder-only: '{value}'");
    }

    [Theory]
    // Real text, no placeholder at all.
    [InlineData("Iron plate")]
    [InlineData("A plate made of iron.")]
    [InlineData("")]
    [InlineData("   ")]
    // Placeholder PLUS real translatable words.
    [InlineData("__ENTITY__angels-cab__ deploy")]
    [InlineData("Fast __ITEM__motor__ crafting")]
    [InlineData("Starting resource __ENTITY__angels-ore1__")]
    [InlineData("__ITEM__angels-solid-sodium-hypochlorite__ decomposition")]
    [InlineData("Use __CONTROL__open-technology-gui__ to open the technology screen.")]
    // Bare number / separators with no placeholder are NOT considered placeholder-only.
    [InlineData("60")]
    [InlineData(" - ")]
    public void IsPlaceholderOnly_RejectsStringsWithRealText(string value)
    {
        Assert.False(LocalizationPlaceholders.IsPlaceholderOnly(value), $"expected NOT placeholder-only: '{value}'");
    }

    [Fact]
    public void IsPlaceholderOnly_NullIsFalse()
    {
        Assert.False(LocalizationPlaceholders.IsPlaceholderOnly(null));
    }

    [Theory]
    [InlineData("Fast __ITEM__motor__ crafting", "Fast  crafting")]
    [InlineData("[img=item/x] __ENTITY__y__ done", "  done")]
    [InlineData("__1__ minutes", " minutes")]
    public void StripNonTranslatable_RemovesPlaceholdersAndRichText(string input, string expected)
    {
        Assert.Equal(expected, LocalizationPlaceholders.StripNonTranslatable(input));
    }
}