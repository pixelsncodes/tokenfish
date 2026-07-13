namespace TokenFish.Providers.Claude;

internal interface IClaudeBridgeStateStore
{
    Task<ClaudeBridgeState> LoadAsync(CancellationToken cancellationToken);

    Task<ClaudeBridgeState> MergeAndSaveAsync(
        ClaudeBridgeState incomingState,
        CancellationToken cancellationToken);
}
