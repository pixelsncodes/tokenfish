namespace TokenFish.Providers.Codex.Usage;

public sealed class CodexAccountUsageResponseParseException : Exception
{
    public long? ErrorCode { get; }

    public CodexAccountUsageResponseParseException(
        string message,
        long? errorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
