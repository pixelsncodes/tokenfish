using System.Text;

namespace TokenFish.Providers.Claude;

internal sealed class ClaudeBridgeConsoleRunner
{
    private const int MaximumInputBytes = 1024 * 1024;

    private readonly ClaudeStatusLineUsageParser _parser;
    private readonly IClaudeBridgeStateStore _stateStore;
    private readonly ClaudeBridgeStatusLineRenderer _renderer;
    private readonly TimeProvider _timeProvider;

    public ClaudeBridgeConsoleRunner(
        IClaudeBridgeStateStore stateStore,
        TimeProvider? timeProvider = null)
        : this(
            new ClaudeStatusLineUsageParser(),
            stateStore,
            new ClaudeBridgeStatusLineRenderer(),
            timeProvider ?? TimeProvider.System)
    {
    }

    internal ClaudeBridgeConsoleRunner(
        ClaudeStatusLineUsageParser parser,
        IClaudeBridgeStateStore stateStore,
        ClaudeBridgeStatusLineRenderer renderer,
        TimeProvider timeProvider)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<int> RunAsync(
        string[] args,
        Stream input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var arguments = ParseArguments(args);
        if (!arguments.IsSupported)
        {
            await WriteUnavailableAsync(output).ConfigureAwait(false);
            return 2;
        }

        try
        {
            var payload = await ReadBoundedInputAsync(input, cancellationToken)
                .ConfigureAwait(false);
            var usage = _parser.Parse(payload, _timeProvider.GetUtcNow());
            var parsedState = ClaudeBridgeState.FromStatusLineUsage(usage);

            var state = !parsedState.HasUsageData
                ? ClaudeBridgeState.Empty
                : arguments.IsDryRun
                ? parsedState
                : await _stateStore.MergeAndSaveAsync(parsedState, cancellationToken)
                    .ConfigureAwait(false);

            await output.WriteLineAsync(_renderer.Render(state)).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            await WriteUnavailableAsync(output).ConfigureAwait(false);
            return 0;
        }
    }

    private async Task WriteUnavailableAsync(TextWriter output)
    {
        await output.WriteLineAsync(_renderer.Render(ClaudeBridgeState.Empty))
            .ConfigureAwait(false);
    }

    private static ClaudeBridgeConsoleArguments ParseArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return new ClaudeBridgeConsoleArguments(true, false);
        }

        if (args.Length == 1 && StringComparer.Ordinal.Equals(args[0], "--dry-run"))
        {
            return new ClaudeBridgeConsoleArguments(true, true);
        }

        return new ClaudeBridgeConsoleArguments(false, false);
    }

    private static async Task<byte[]> ReadBoundedInputAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var memory = new MemoryStream();

        while (true)
        {
            var bytesRead = await input.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (memory.Length + bytesRead > MaximumInputBytes)
            {
                throw new ClaudeStatusLineUsageParseException(
                    ClaudeStatusLineUsageParseError.InvalidShape);
            }

            memory.Write(buffer, 0, bytesRead);
        }

        return memory.ToArray();
    }

    private sealed record ClaudeBridgeConsoleArguments(
        bool IsSupported,
        bool IsDryRun);
}
