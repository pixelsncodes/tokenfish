namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerSessionException : Exception
{
    public CodexAppServerSessionException(string message)
        : base(message)
    {
    }
}
