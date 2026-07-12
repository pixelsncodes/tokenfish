namespace TokenFish.Infrastructure;

public sealed class ApplicationInstanceStartupRunner
{
    private readonly ApplicationInstanceStartupCoordinator _startupCoordinator;

    public ApplicationInstanceStartupRunner(
        ApplicationInstanceStartupCoordinator startupCoordinator)
    {
        ArgumentNullException.ThrowIfNull(startupCoordinator);

        _startupCoordinator = startupCoordinator;
    }

    public async Task<ApplicationInstanceStartupResult> RunAsync(
        Func<Task> startPrimaryAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startPrimaryAsync);

        var startupResult = await _startupCoordinator
            .DecideStartupAsync(cancellationToken)
            .ConfigureAwait(false);

        if (startupResult.Kind == ApplicationInstanceStartupKind.Primary)
        {
            await startPrimaryAsync().ConfigureAwait(false);
        }

        return startupResult;
    }
}
