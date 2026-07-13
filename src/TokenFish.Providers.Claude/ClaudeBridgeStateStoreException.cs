namespace TokenFish.Providers.Claude;

internal sealed class ClaudeBridgeStateStoreException : Exception
{
    public ClaudeBridgeStateStoreException()
        : this(ClaudeBridgeStateStoreFailureKind.Unreadable)
    {
    }

    public ClaudeBridgeStateStoreException(ClaudeBridgeStateStoreFailureKind failureKind)
        : base("Claude bridge state could not be read or updated.")
    {
        FailureKind = failureKind;
    }

    public ClaudeBridgeStateStoreFailureKind FailureKind { get; }
}

internal enum ClaudeBridgeStateStoreFailureKind
{
    Unreadable,
    Malformed
}
