namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerProtocolException : Exception
{
    public CodexAppServerProtocolException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
