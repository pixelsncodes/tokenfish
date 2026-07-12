namespace TokenFish.Providers.Codex;

public sealed class CodexUsageSnapshotNormalizationException : Exception
{
    public CodexUsageSnapshotNormalizationException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
