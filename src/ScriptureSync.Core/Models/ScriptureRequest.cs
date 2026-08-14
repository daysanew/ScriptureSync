namespace ScriptureSync.Core.Models;

public sealed record ScriptureRequest(
    string PlanId,
    string PcoItemId,
    int Sequence,
    string RawText,
    string TranslationCode,
    IReadOnlyList<PassageReference> Passages);
