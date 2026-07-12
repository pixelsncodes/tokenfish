namespace TokenFish.Core.Models;

public enum ProviderConnectionState
{
    Unknown,
    NotConfigured,
    Disconnected,
    Connecting,
    Connected,
    Degraded
}
