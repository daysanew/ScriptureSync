namespace ScriptureSync.Core.Sync;

public class ScriptureSyncException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ScriptureDestinationUnavailableException(string message, Exception? innerException = null)
    : ScriptureSyncException(message, innerException);

public sealed class ScriptureTranslationNotInstalledException(string translationCode, Exception? innerException = null)
    : ScriptureSyncException($"Bible translation {translationCode} is not installed.", innerException)
{
    public string TranslationCode { get; } = translationCode;
}
