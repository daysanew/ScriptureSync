using ScriptureSync.Core.Parsing;

namespace ScriptureSync.Tests;

public class ScriptureReferenceParserTests
{
    private readonly ScriptureReferenceParser _parser = new();

    [Fact]
    public void Uses_default_translation_when_input_has_none()
    {
        var result = new ScriptureReferenceParser("NLT").Parse("John 3:16");

        Assert.True(result.IsValid);
        Assert.Equal(["NLT"], result.TranslationCodes);
        Assert.Equal("John 3:16", Assert.Single(result.Passages).ToString());
    }

    [Theory]
    [InlineData("John 1:1,5 (KJV)", "John 1:1,5")]
    [InlineData("John 1:1&5 (kjv)", "John 1:1,5")]
    [InlineData("John 1:1-5,7,9-17 (KJV)", "John 1:1-5,7,9-17")]
    [InlineData("1 Corinthians 13:1-7 (KJV)", "1 Corinthians 13:1-7")]
    public void Parses_single_chapter_verse_lists(string input, string expected)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("KJV", result.TranslationCode);
        Assert.Equal(expected, Assert.Single(result.Passages).ToString());
    }

    [Theory]
    [InlineData("John 1:1-5, 2:4-5 (KJV)")]
    [InlineData("John 1:1-5 & 2:4-5 (KJV)")]
    [InlineData("John 1:1-5; John 2:4-5 (KJV)")]
    public void Parses_multiple_chapters(string input)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(["John 1:1-5", "John 2:4-5"], result.Passages.Select(x => x.ToString()));
    }

    [Fact]
    public void Parses_a_whole_chapter()
    {
        var result = _parser.Parse("Psalm 23 (KJV)");

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("Psalm 23", Assert.Single(result.Passages).ToString());
    }

    [Theory]
    [InlineData("John 3:16–18 (KJV)", "John 3:16-18")]
    [InlineData("John 3 : 16 — 18 ( KJV )", "John 3:16-18")]
    [InlineData("Jn 3:16 [KJV]", "John 3:16")]
    [InlineData("Rom 8:28 KJV", "Romans 8:28")]
    [InlineData("I Corinthians 13:1-7 - KJV", "1 Corinthians 13:1-7")]
    [InlineData("1Corinthians 13:1-7 (KJV).", "1 Corinthians 13:1-7")]
    public void Normalizes_common_human_variations(string input, string expected)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(expected, Assert.Single(result.Passages).ToString());
    }

    [Theory]
    [InlineData("John 3:16; Romans 8:28 (KJV)")]
    [InlineData("John 3:16, Rom 8:28 (KJV)")]
    [InlineData("Jn 3:16 & Romans 8:28 (KJV)")]
    public void Parses_multiple_books(string input)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(["John 3:16", "Romans 8:28"], result.Passages.Select(x => x.ToString()));
    }

    [Fact]
    public void Parses_a_cross_chapter_range()
    {
        var result = _parser.Parse("Acts 7:54-8:3 (KJV)");

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("Acts 7:54-8:3", Assert.Single(result.Passages).ToString());
    }

    [Theory]
    [InlineData("1 Peter 1:3 (NKJV & NLT)", "NKJV", "NLT")]
    [InlineData("1 Peter 1:3 (NKJV & NLT & KJV & AMP)", "NKJV", "NLT", "KJV", "AMP")]
    [InlineData("1 Peter 1:3 [NKJV, NLT, KJV]", "NKJV", "NLT", "KJV")]
    [InlineData("1 Peter 1:3 (NKJV / NLT and KJV)", "NKJV", "NLT", "KJV")]
    public void Parses_any_number_of_translations(string input, params string[] expected)
    {
        var result = _parser.Parse(input);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(expected, result.TranslationCodes);
        Assert.Equal("1 Peter 1:3", Assert.Single(result.Passages).ToString());
    }

    [Fact]
    public void Removes_duplicate_translation_codes_while_preserving_order()
    {
        var result = _parser.Parse("Romans 5:5 (AMP & KJV & amp)");

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(["AMP", "KJV"], result.TranslationCodes);
    }

    [Fact]
    public void Removes_overlapping_verses_within_one_pco_item()
    {
        var result = _parser.Parse("John 1:1-5,3,5-7 (KJV)");

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("John 1:1-7", Assert.Single(result.Passages).ToString());
    }

    [Theory]
    [InlineData("not a scripture (KJV)")]
    [InlineData("John 1:9-3 (KJV)")]
    public void Returns_a_friendly_error_for_invalid_input(string input)
    {
        var result = _parser.Parse(input);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ErrorMessage);
    }
}
