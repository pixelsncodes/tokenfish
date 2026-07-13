namespace TokenFish.Providers.Claude;

internal sealed class ClaudeBridgeStateStoreException : Exception
{
    public ClaudeBridgeStateStoreException()
        : base("Claude bridge state could not be updated.")
    {
    }
}
