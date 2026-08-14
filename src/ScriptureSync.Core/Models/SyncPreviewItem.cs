namespace ScriptureSync.Core.Models;

public enum SyncDisposition
{
    Ready,
    AlreadySynced,
    NeedsAttention
}

public sealed record SyncPreviewItem(
    ScriptureRequest Request,
    SyncDisposition Disposition,
    string Message);
