namespace ScriptureSync.Core.Presentations;

public enum PublishChange { Create, Update, Unchanged }
public sealed record PublishResult(string Identity, string PresentationId, PublishChange Change, string Title);

public interface IPresentationPublisher
{
    Task<PublishResult> PublishAsync(ScripturePresentation presentation, string identity, CancellationToken cancellationToken = default);
}
