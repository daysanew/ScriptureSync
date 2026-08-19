namespace ScriptureSync.OpenLP;

public class OpenLpException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OpenLpBibleNotInstalledException(string requestedCode)
    : OpenLpException($"Bible translation {requestedCode} is not installed in OpenLP.");
