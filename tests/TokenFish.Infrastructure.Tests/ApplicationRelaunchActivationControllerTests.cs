using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class ApplicationRelaunchActivationControllerTests
{
    [Fact]
    public void AcceptedActivationIsDispatchedToUiBoundaryExactlyOnce()
    {
        var source = new RecordingActivationSource();
        var queue = new RecordingPopupActionQueue();
        var showCallCount = 0;
        using var controller = CreateController(source, queue, _ =>
        {
            showCallCount++;
            return Task.CompletedTask;
        });

        source.RaiseActivated();

        Assert.Equal(1, queue.EnqueueCallCount);
        Assert.Equal(0, showCallCount);

        queue.Flush();

        Assert.Equal(1, showCallCount);
    }

    [Fact]
    public void MultipleRapidActivationsReuseOnePopupShowRequest()
    {
        var source = new RecordingActivationSource();
        var queue = new RecordingPopupActionQueue();
        var showCallCount = 0;
        using var controller = CreateController(source, queue, _ =>
        {
            showCallCount++;
            return Task.CompletedTask;
        });

        source.RaiseActivated();
        source.RaiseActivated();
        source.RaiseActivated();
        queue.Flush();

        Assert.Equal(1, queue.EnqueueCallCount);
        Assert.Equal(1, showCallCount);
    }

    [Fact]
    public void SeparateAcceptedActivationsAreEachDispatchedOnce()
    {
        var source = new RecordingActivationSource();
        var queue = new RecordingPopupActionQueue();
        var showCallCount = 0;
        using var controller = CreateController(source, queue, _ =>
        {
            showCallCount++;
            return Task.CompletedTask;
        });

        source.RaiseActivated();
        queue.Flush();
        source.RaiseActivated();
        queue.Flush();

        Assert.Equal(2, queue.EnqueueCallCount);
        Assert.Equal(2, showCallCount);
    }

    [Fact]
    public void ActivationDuringShutdownIsIgnoredSafely()
    {
        var source = new RecordingActivationSource();
        var queue = new RecordingPopupActionQueue();
        var showCallCount = 0;
        using var controller = CreateController(source, queue, _ =>
        {
            showCallCount++;
            return Task.CompletedTask;
        });

        controller.BeginShutdown();
        source.RaiseActivated();
        queue.Flush();

        Assert.Equal(1, source.UnsubscribeCallCount);
        Assert.Equal(0, queue.EnqueueCallCount);
        Assert.Equal(0, showCallCount);
    }

    [Fact]
    public void ActivationSubscriptionIsRemovedDuringDisposal()
    {
        var source = new RecordingActivationSource();
        var queue = new RecordingPopupActionQueue();
        var controller = CreateController(source, queue, _ => Task.CompletedTask);

        Assert.Equal(1, source.SubscribeCallCount);

        controller.Dispose();
        source.RaiseActivated();

        Assert.Equal(1, source.UnsubscribeCallCount);
        Assert.Equal(0, queue.EnqueueCallCount);
    }

    [Fact]
    public void ActivationFailureReportsGenericApplicationState()
    {
        var source = new RecordingActivationSource();
        var queue = new RecordingPopupActionQueue();
        var faultCallCount = 0;
        using var controller = CreateController(
            source,
            queue,
            _ => Task.FromException(new InvalidOperationException("raw activation payload")),
            () => faultCallCount++);

        source.RaiseActivated();
        queue.Flush();

        Assert.Equal(1, faultCallCount);
    }

    private static ApplicationRelaunchActivationController CreateController(
        RecordingActivationSource source,
        RecordingPopupActionQueue queue,
        Func<CancellationToken, Task> showPopupAsync,
        Action? reportActivationFault = null)
    {
        var controller = new ApplicationRelaunchActivationController(
            source,
            queue,
            showPopupAsync,
            reportActivationFault ?? (() => { }));
        controller.Initialize();
        return controller;
    }

    private sealed class RecordingActivationSource : IApplicationRelaunchActivationSource
    {
        private Action? _activationHandler;

        public int SubscribeCallCount { get; private set; }

        public int UnsubscribeCallCount { get; private set; }

        public IDisposable SubscribeActivated(Action activationHandler)
        {
            SubscribeCallCount++;
            _activationHandler += activationHandler;
            return new CallbackDisposable(() =>
            {
                UnsubscribeCallCount++;
                _activationHandler -= activationHandler;
            });
        }

        public void RaiseActivated() => _activationHandler?.Invoke();
    }

    private sealed class RecordingPopupActionQueue : IPopupActionQueue
    {
        private readonly Queue<Action> _actions = new();

        public int EnqueueCallCount { get; private set; }

        public void Enqueue(Action action)
        {
            EnqueueCallCount++;
            _actions.Enqueue(action);
        }

        public void Flush()
        {
            while (_actions.TryDequeue(out var action))
            {
                action();
            }
        }
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public CallbackDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dispose();
        }
    }
}
