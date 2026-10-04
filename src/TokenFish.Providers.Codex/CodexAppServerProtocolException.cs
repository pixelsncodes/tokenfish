namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerProtocolException : Exception
{
    public CodexProtocolErrorKind Kind { get; }

    public CodexAppServerProtocolException(
        string message,
        Exception? innerException = null,
        CodexProtocolErrorKind kind = CodexProtocolErrorKind.TransportFailure)
        : base(message, innerException)
    {
        Kind = kind;
    }
}

public enum CodexProtocolErrorKind
{
    TransportFailure,
    UnsupportedMethod,
    RequestRejected
}
