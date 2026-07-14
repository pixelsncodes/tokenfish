using TokenFish.App;

namespace TokenFish.Infrastructure.Tests;

public sealed class OnboardingWindowLifetimeTests
{
    [Fact]
    public void RecheckReceivesAFreshCancellableLifetimeToken()
    {
        using var lifetime = new OnboardingWindowLifetime();

        Assert.True(lifetime.TryBeginRecheck(out var token));
        Assert.True(token.CanBeCanceled);
        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public void ClosingCancelsActiveRecheckAndClearsBusyOwnership()
    {
        using var lifetime = new OnboardingWindowLifetime();
        lifetime.TryBeginRecheck(out var token);

        lifetime.BeginClosing();

        Assert.True(token.IsCancellationRequested);
        Assert.False(lifetime.IsActive);
        Assert.False(lifetime.IsRechecking);
        Assert.False(lifetime.TryBeginRecheck(out _));
    }

    [Fact]
    public void ClosingIsIdempotent()
    {
        using var lifetime = new OnboardingWindowLifetime();
        lifetime.TryBeginRecheck(out var token);

        lifetime.BeginClosing();
        lifetime.BeginClosing();

        Assert.True(token.IsCancellationRequested);
        Assert.False(lifetime.IsActive);
    }

    [Fact]
    public void DisposalIsSafeAfterClosing()
    {
        var lifetime = new OnboardingWindowLifetime();
        lifetime.BeginClosing();

        lifetime.Dispose();
        lifetime.Dispose();

        Assert.False(lifetime.TryBeginRecheck(out _));
    }

    [Fact]
    public void SecondWindowLifetimeHasANewNonCancelledToken()
    {
        using var first = new OnboardingWindowLifetime();
        first.TryBeginRecheck(out var firstToken);
        first.BeginClosing();
        using var second = new OnboardingWindowLifetime();

        Assert.True(second.TryBeginRecheck(out var secondToken));
        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(secondToken.IsCancellationRequested);
    }

    [Fact]
    public void DuplicateRecheckIsBlockedUntilTheCurrentRecheckEnds()
    {
        using var lifetime = new OnboardingWindowLifetime();

        Assert.True(lifetime.TryBeginRecheck(out _));
        Assert.False(lifetime.TryBeginRecheck(out _));
        lifetime.EndRecheck();
        Assert.True(lifetime.TryBeginRecheck(out _));
    }
}
