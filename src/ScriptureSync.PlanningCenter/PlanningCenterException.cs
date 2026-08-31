namespace ScriptureSync.PlanningCenter;

public sealed class PlanningCenterException(string message, Exception? innerException = null)
    : Exception(message, innerException);
