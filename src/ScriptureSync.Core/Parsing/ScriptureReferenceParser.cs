using System.Text.RegularExpressions;
using ScriptureSync.Core.Models;

namespace ScriptureSync.Core.Parsing;

public sealed partial class ScriptureReferenceParser
{
    public ScriptureReferenceParser(string defaultBibleTranslation = "KJV")
    {
        DefaultBibleTranslation = defaultBibleTranslation;
    }

    public string DefaultBibleTranslation { get; set; }

    public ScriptureParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ScriptureParseResult.Invalid("Enter a scripture reference and Bible translation.");
        }

        var text = NormalizePunctuation(input.Trim());
        var suffixMatch = TranslationSuffixRegex().Match(text);
        IReadOnlyList<string> translations;
        string referenceText;
        if (suffixMatch.Success)
        {
            translations = ParseTranslations(TranslationValue(suffixMatch));
            referenceText = suffixMatch.Groups["reference"].Value.Trim();
        }
        else
        {
            translations = ParseTranslations(DefaultBibleTranslation);
            referenceText = text;
        }
        if (translations.Count == 0)
        {
            return ScriptureParseResult.Invalid(
                "Add one or more Bible translations, such as (KJV) or (KJV & NLT).");
        }
        var groups = SplitBookGroups(referenceText);
        if (groups.Count == 0)
        {
            return ScriptureParseResult.Invalid("Use a reference such as John 1:1-5 or Psalm 23.");
        }

        var passages = new List<PassageReference>();
        foreach (var group in groups)
        {
            if (!BibleBookNames.TryRead(group, out var book, out var body))
            {
                return ScriptureParseResult.Invalid($"The Bible book in '{group}' was not recognized.");
            }

            var groupResult = ParseBookGroup(book, body);
            if (!groupResult.IsValid)
            {
                return ScriptureParseResult.Invalid(groupResult.ErrorMessage);
            }

            passages.AddRange(groupResult.Passages);
        }

        return ScriptureParseResult.Valid(translations, passages);
    }

    private static ScriptureParseResult ParseBookGroup(string book, string body)
    {
        body = body.Replace('&', ',').Replace(';', ',').Trim().TrimEnd(',').Trim();

        var crossChapterMatch = CrossChapterRangeRegex().Match(body);
        if (crossChapterMatch.Success)
        {
            var startChapter = int.Parse(crossChapterMatch.Groups["startChapter"].Value);
            var startVerse = int.Parse(crossChapterMatch.Groups["startVerse"].Value);
            var endChapter = int.Parse(crossChapterMatch.Groups["endChapter"].Value);
            var endVerse = int.Parse(crossChapterMatch.Groups["endVerse"].Value);

            if (startChapter < 1 || startVerse < 1 || endChapter < startChapter || endVerse < 1)
            {
                return ScriptureParseResult.Invalid($"'{body}' contains a range that is out of order.");
            }

            return ScriptureParseResult.Valid(
                string.Empty,
                [new PassageReference(book, startChapter, startVerse.ToString(), endChapter, endVerse)]);
        }

        if (!body.Contains(':'))
        {
            return ParseWholeChapters(book, body);
        }

        return ParseVerseSelections(book, body);
    }

    private static ScriptureParseResult ParseWholeChapters(string book, string body)
    {
        var chapterTokens = body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var passages = new List<PassageReference>();

        foreach (var token in chapterTokens)
        {
            if (!int.TryParse(token, out var chapter) || chapter < 1)
            {
                return ScriptureParseResult.Invalid($"'{token}' is not a valid chapter number.");
            }

            passages.Add(new PassageReference(book, chapter));
        }

        return passages.Count == 0
            ? ScriptureParseResult.Invalid("No chapter was found in the scripture reference.")
            : ScriptureParseResult.Valid(string.Empty, passages);
    }

    private static ScriptureParseResult ParseVerseSelections(string book, string body)
    {
        var chapterMatches = ChapterStartRegex().Matches(body);
        if (chapterMatches.Count == 0 || chapterMatches[0].Index != 0)
        {
            return ScriptureParseResult.Invalid("Use a chapter and verse, such as John 1:1-5.");
        }

        var passages = new List<PassageReference>();
        for (var index = 0; index < chapterMatches.Count; index++)
        {
            var match = chapterMatches[index];
            var selectionStart = match.Index + match.Length;
            var selectionEnd = index + 1 < chapterMatches.Count
                ? chapterMatches[index + 1].Index
                : body.Length;
            var rawSelection = body[selectionStart..selectionEnd].Trim().TrimEnd(',').Trim();

            if (!int.TryParse(match.Groups["chapter"].Value, out var chapter) || chapter < 1)
            {
                return ScriptureParseResult.Invalid("Chapter numbers must be greater than zero.");
            }

            if (!VerseSelectionRegex().IsMatch(rawSelection))
            {
                return ScriptureParseResult.Invalid(
                    $"'{rawSelection}' is not a valid verse list for {book} {chapter}.");
            }

            try
            {
                passages.Add(new PassageReference(book, chapter, NormalizeVerseSelection(rawSelection)));
            }
            catch (FormatException)
            {
                return ScriptureParseResult.Invalid(
                    $"'{rawSelection}' contains a verse range that is out of order.");
            }
        }

        return ScriptureParseResult.Valid(string.Empty, passages);
    }

    private static IReadOnlyList<string> SplitBookGroups(string referenceText)
    {
        var groups = new List<string>();
        var start = 0;

        for (var index = 0; index < referenceText.Length; index++)
        {
            if (referenceText[index] is not (',' or ';' or '&'))
            {
                continue;
            }

            var possibleNextGroup = referenceText[(index + 1)..].TrimStart();
            if (!BibleBookNames.TryRead(possibleNextGroup, out _, out _))
            {
                continue;
            }

            groups.Add(referenceText[start..index].Trim());
            start = index + 1;
        }

        groups.Add(referenceText[start..].Trim());
        return groups.Where(group => group.Length > 0).ToArray();
    }

    private static string NormalizeVerseSelection(string selection)
    {
        var ranges = selection
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseRange)
            .OrderBy(range => range.Start)
            .ThenBy(range => range.End)
            .ToList();

        var merged = new List<VerseRange>();
        foreach (var range in ranges)
        {
            if (range.Start < 1 || range.End < range.Start)
            {
                throw new FormatException();
            }

            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, range.End) };
            }
            else
            {
                merged.Add(range);
            }
        }

        return string.Join(',', merged.Select(range =>
            range.Start == range.End ? range.Start.ToString() : $"{range.Start}-{range.End}"));
    }

    private static VerseRange ParseRange(string token)
    {
        var values = token.Split('-', StringSplitOptions.TrimEntries);
        var start = int.Parse(values[0]);
        var end = values.Length == 1 ? start : int.Parse(values[1]);
        return new VerseRange(start, end);
    }

    private static string NormalizePunctuation(string value) =>
        value.Replace('–', '-').Replace('—', '-').Replace('−', '-');

    private static string TranslationValue(Match match) =>
        new[] { "parenthesized", "bracketed", "dashed", "bare" }
            .Select(group => match.Groups[group].Value.Trim())
            .First(value => value.Length > 0);

    private static IReadOnlyList<string> ParseTranslations(string value) =>
        Regex.Split(value, @"\s*(?:&|,|/|\band\b)\s*", RegexOptions.IgnoreCase)
            .Select(code => code.Trim().ToUpperInvariant())
            .Where(code => TranslationCodeRegex().IsMatch(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record VerseRange(int Start, int End);

    [GeneratedRegex(@"^(?<reference>.*\d)\s*(?:(?:\(\s*(?<parenthesized>[A-Za-z][A-Za-z0-9 .,&/-]{0,100})\s*\))|(?:\[\s*(?<bracketed>[A-Za-z][A-Za-z0-9 .,&/-]{0,100})\s*\])|(?:-\s*(?<dashed>[A-Za-z][A-Za-z0-9.-]{1,15}))|(?<bare>[A-Za-z][A-Za-z0-9.-]{1,15}))\s*[.,;]?$")]
    private static partial Regex TranslationSuffixRegex();

    [GeneratedRegex(@"^[A-Z][A-Z0-9.-]{0,14}$")]
    private static partial Regex TranslationCodeRegex();

    [GeneratedRegex(@"^(?<startChapter>\d+)\s*:\s*(?<startVerse>\d+)\s*-\s*(?<endChapter>\d+)\s*:\s*(?<endVerse>\d+)$")]
    private static partial Regex CrossChapterRangeRegex();

    [GeneratedRegex(@"(?<![\d-])(?<chapter>\d+)\s*:\s*")]
    private static partial Regex ChapterStartRegex();

    [GeneratedRegex(@"^\d+(?:\s*-\s*\d+)?(?:\s*,\s*\d+(?:\s*-\s*\d+)?)*$")]
    private static partial Regex VerseSelectionRegex();
}
