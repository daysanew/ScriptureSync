using ScriptureSync.Core.Models;

namespace ScriptureSync.Core.Parsing;

public sealed record ScriptureParseResult(
    bool IsValid,
    IReadOnlyList<string> TranslationCodes,
    IReadOnlyList<PassageReference> Passages,
    string ErrorMessage)
{
    // Kept for callers that only need the first (or only) requested translation.
    public string TranslationCode => TranslationCodes.FirstOrDefault() ?? string.Empty;

    public static ScriptureParseResult Valid(
        string translationCode,
        IReadOnlyList<PassageReference> passages) =>
        Valid([translationCode], passages);

    public static ScriptureParseResult Valid(
        IReadOnlyList<string> translationCodes,
        IReadOnlyList<PassageReference> passages) =>
        new(true, translationCodes, passages, string.Empty);

    public static ScriptureParseResult Invalid(string message) =>
        new(false, [], [], message);
}
