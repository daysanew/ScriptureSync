namespace ScriptureSync.Core.Presentations;

/// <summary>Logical content only; the destination template supplies visual formatting.</summary>
public sealed record ScripturePresentation(string Title, IReadOnlyList<ScriptureSlide> Slides);

public sealed record ScriptureSlide(string Reference, string Text, string Translation);
