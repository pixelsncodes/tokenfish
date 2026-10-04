using TokenFish.Core.Models;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexRecoveryAndSummaryTests
{
    [Fact]
    public async Task FailedSessionIsDisposedAndRecreatedOnlyOnNextRefresh()
    {
        var first=new Session {Failure=new IOException("pipe closed")};var next=new Session();var factory=new Factory(first,next);
        await using var collector=new CodexSessionUsageCollector(factory,CodexAppServerLaunchCommand.CreateNative("codex.exe"),"1.0.0");
        await Assert.ThrowsAsync<IOException>(()=>collector.CollectAsync(CancellationToken.None));
        Assert.Equal(1,first.Disposals);Assert.Equal(1,factory.Starts);
        var snapshot=await collector.CollectAsync(CancellationToken.None);
        Assert.Equal(2,factory.Starts);Assert.Equal(ProviderKind.Codex,snapshot.Provider);
    }

    [Fact]
    public async Task UnhealthySessionIsReplacedAfterReturningItsQuota()
    {
        var first=new Session();var factory=new Factory(first,new Session());
        await using var collector=new CodexSessionUsageCollector(factory,CodexAppServerLaunchCommand.CreateNative("codex.exe"),"1.0.0");
        await collector.CollectAsync(CancellationToken.None);first.IsHealthy=false;
        await collector.CollectAsync(CancellationToken.None);
        Assert.Equal(1,first.Disposals);Assert.Equal(2,factory.Starts);
    }

    [Fact]
    public async Task OptionalActivityTimeoutPreservesRealQuotaAndRequestsRecovery()
    {
        var client=new Protocol {Stall=true};
        var collector=new CodexProviderUsageCollector(client,new(),activityTimeout:TimeSpan.FromMilliseconds(25));
        var snapshot=await collector.CollectAsync(CancellationToken.None);
        Assert.Equal(36m,snapshot.UsageWindow.PercentageConsumed);
        Assert.False(snapshot.WeeklyTokens.IsAvailable);Assert.True(collector.RequiresSessionRecovery);
    }

    [Fact]
    public async Task UnsupportedActivityPreservesQuotaWithoutReconnectLoop()
    {
        var client=new Protocol {Failure=new CodexAppServerProtocolException("unsupported",kind:CodexProtocolErrorKind.UnsupportedMethod)};
        var collector=new CodexProviderUsageCollector(client,new());
        var snapshot=await collector.CollectAsync(CancellationToken.None);
        Assert.True(snapshot.UsageWindow.IsAvailable);Assert.False(snapshot.WeeklyTokens.IsAvailable);
        Assert.False(collector.RequiresSessionRecovery);
    }

    [Fact]
    public async Task CallerCancellationPropagatesInsteadOfReturningPartialData()
    {
        using var cancellation=new CancellationTokenSource(TimeSpan.FromMilliseconds(25));
        var collector=new CodexProviderUsageCollector(new Protocol {Stall=true},new());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>collector.CollectAsync(cancellation.Token));
    }

    [Fact]
    public void OptionalSummaryHasCorrectUnitsAndPreservesCompatibilityTotals()
    {
        var parsed=new CodexAccountUsageResponseParser().Parse("""
            {"id":1,"result":{"summary":{"lifetimeTokens":24800000,"peakDailyTokens":310000,"longestRunningTurnSec":2280,"currentStreakDays":8,"longestStreakDays":14},"dailyUsageBuckets":[{"startDate":"2026-10-03","tokens":132000}]}}
            ""","1");
        var snapshot=new CodexUsageSnapshotFactory().Create(Limits(),parsed,new(2026,10,3,12,0,0,TimeSpan.Zero));
        Assert.Equal(132000,snapshot.WeeklyTokens.TokenCount);Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.Equal(5,snapshot.ActivityMetrics.Count(metric=>metric.MetricId.StartsWith("codex:summary:")));
        Assert.Equal(UsageActivityUnit.Seconds,snapshot.ActivityMetrics.Single(metric=>metric.DisplayLabel=="Longest turn").Unit);
        Assert.Equal(UsageActivityUnit.Days,snapshot.ActivityMetrics.Single(metric=>metric.DisplayLabel=="Current streak").Unit);
        Assert.Equal(132000,Assert.Single(snapshot.DailyActivity).Tokens);
    }

    [Fact]
    public void MissingNullInvalidOptionalFieldsRemainUnavailableAndZeroIsReal()
    {
        var parsed=new CodexAccountUsageResponseParser().Parse("""
            {"id":1,"result":{"summary":{"lifetimeTokens":null,"peakDailyTokens":-1,"longestRunningTurnSec":"secret","currentStreakDays":0}}}
            ""","1");
        Assert.Null(parsed.LifetimeTokens);Assert.Null(parsed.PeakDailyTokens);Assert.Null(parsed.LongestRunningTurnSec);
        Assert.Equal(0,parsed.CurrentStreakDays);Assert.Null(parsed.LongestStreakDays);
        var snapshot=new CodexUsageSnapshotFactory().Create(Limits(),parsed,DateTimeOffset.UtcNow);
        Assert.False(snapshot.WeeklyTokens.IsAvailable);Assert.Empty(snapshot.DailyActivity);
        Assert.Equal(0,snapshot.ActivityMetrics.Single(metric=>metric.IsAvailable).Value);
    }

    private static CodexRateLimitsSnapshot Limits()=>new(new(36,300,null),new(79,10080,null),null);
    private sealed class Protocol:ICodexAppServerProtocolClient
    {
        public bool Stall {get;init;}
        public Exception? Failure {get;init;}
        public Task InitializeAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
        public Task<CodexRateLimitsSnapshot> ReadRateLimitsAsync(CancellationToken cancellationToken)=>Task.FromResult(Limits());
        public async Task<CodexAccountUsageSnapshot> ReadAccountUsageAsync(CancellationToken cancellationToken)
        {if(Stall)await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);if(Failure is not null)throw Failure;return new(null);}
    }
    private sealed class Factory(params Session[] sessions):ICodexAppServerSessionFactory
    {
        public int Starts {get;private set;}
        public Task<ICodexAppServerSession> StartAsync(CodexAppServerLaunchCommand launchCommand,string clientVersion,CancellationToken cancellationToken)
            =>Task.FromResult<ICodexAppServerSession>(sessions[Starts++]);
    }
    private sealed class Session:ICodexAppServerSession
    {
        public bool IsHealthy {get;set;}=true;public Exception? Failure {get;init;} public int Disposals {get;private set;}
        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
            =>Failure is {} failure ? Task.FromException<ProviderUsageSnapshot>(failure) : Task.FromResult(new CodexUsageSnapshotFactory().Create(Limits(),new(null),DateTimeOffset.UtcNow));
        public ValueTask DisposeAsync(){Disposals++;return ValueTask.CompletedTask;}
    }
}
