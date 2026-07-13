namespace TokenFish.Core.Providers;

public enum ProviderCollectionFailureReason
{
    ClaudeBridgeUnreadable,
    ClaudeBridgeMalformed,
    CodexSession,
    CodexProtocol,
    CodexRateLimitsResponse,
    CodexAccountUsageResponse,
    CodexUsageNormalization
}
