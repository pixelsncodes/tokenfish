namespace TokenFish.Infrastructure;

public sealed record ApplicationInstanceStartupResult(
    ApplicationInstanceStartupKind Kind,
    ApplicationInstanceStartupIssue Issue)
{
    public static ApplicationInstanceStartupResult Primary { get; } =
        new(ApplicationInstanceStartupKind.Primary, ApplicationInstanceStartupIssue.None);

    public static ApplicationInstanceStartupResult RedirectedSecondary { get; } =
        new(
            ApplicationInstanceStartupKind.RedirectedSecondary,
            ApplicationInstanceStartupIssue.None);

    public static ApplicationInstanceStartupResult Closed(ApplicationInstanceStartupIssue issue)
    {
        if (issue == ApplicationInstanceStartupIssue.None)
        {
            throw new ArgumentException(
                "Closed startup results require a startup issue.",
                nameof(issue));
        }

        return new ApplicationInstanceStartupResult(
            ApplicationInstanceStartupKind.Closed,
            issue);
    }
}
