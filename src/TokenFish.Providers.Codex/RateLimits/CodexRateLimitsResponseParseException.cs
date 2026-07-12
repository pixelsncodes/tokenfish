namespace TokenFish.Providers.Codex.RateLimits;

public sealed class CodexRateLimitsResponseParseException : Exception
{
    public long? ErrorCode { get; }

    public CodexRateLimitsResponseParseException(
        string message,
        long? errorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
