namespace TokenFish.Providers.Claude;

internal sealed class ClaudeStatusLineUsageParseException : Exception
{
    public ClaudeStatusLineUsageParseException(ClaudeStatusLineUsageParseError error)
        : base(CreateMessage(error))
    {
        Error = error;
    }

    public ClaudeStatusLineUsageParseError Error { get; }

    private static string CreateMessage(ClaudeStatusLineUsageParseError error) =>
        error switch
        {
            ClaudeStatusLineUsageParseError.MalformedJson => "Claude status-line JSON was malformed.",
            ClaudeStatusLineUsageParseError.InvalidShape => "Claude status-line usage data had an unsupported shape.",
            ClaudeStatusLineUsageParseError.InvalidPercentage => "Claude status-line usage data contained an invalid percentage.",
            ClaudeStatusLineUsageParseError.InvalidResetTime => "Claude status-line usage data contained an invalid reset time.",
            _ => "Claude status-line usage data could not be parsed."
        };
}
