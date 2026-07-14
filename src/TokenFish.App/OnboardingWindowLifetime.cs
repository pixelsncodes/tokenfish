namespace TokenFish.App;

internal sealed class OnboardingWindowLifetime : IDisposable
{
    private CancellationTokenSource? _cancellation = new();
    private bool _isClosing;
    private bool _isRechecking;

    public bool IsActive => !_isClosing;

    public bool IsRechecking => _isRechecking;

    public bool TryBeginRecheck(out CancellationToken cancellationToken)
    {
        if (_isClosing || _isRechecking || _cancellation is null)
        {
            cancellationToken = default;
            return false;
        }

        _isRechecking = true;
        cancellationToken = _cancellation.Token;
        return true;
    }

    public void EndRecheck() => _isRechecking = false;

    public void BeginClosing()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _isRechecking = false;
        _cancellation?.Cancel();
    }

    public void Dispose()
    {
        BeginClosing();
        _cancellation?.Dispose();
        _cancellation = null;
    }
}
