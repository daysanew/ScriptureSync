namespace ScriptureSync.Core.Logging;

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}
