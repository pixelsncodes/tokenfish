using System.Text;
using TokenFish.Providers.Claude;

namespace TokenFish.Providers.Claude.Tests;

public sealed class ClaudeStatusLineUsageParserTests
{
    private static readonly DateTimeOffset AcceptedAt =
        new(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);

    private readonly ClaudeStatusLineUsageParser _parser = new();

    [Fact]
    public void BothWindowsValid()
    {
        var snapshot = Parse("""
            {
              "rate_limits": {
                "five_hour": { "used_percentage": 24, "resets_at": 1783843200 },
                "seven_day": { "used_percentage": 41, "resets_at": 1784448000 }
              }
            }
            """);

        Assert.Equal(24m, snapshot.FiveHour!.UsedPercentage);
        Assert.Equal(41m, snapshot.SevenDay!.UsedPercentage);
    }

    [Fact]
    public void FiveHourWindowOnly()
    {
        var snapshot = Parse("""{"rate_limits":{"five_hour":{"used_percentage":24}}}""");

        Assert.Equal(24m, snapshot.FiveHour!.UsedPercentage);
        Assert.Null(snapshot.SevenDay);
    }

    [Fact]
    public void SevenDayWindowOnly()
    {
        var snapshot = Parse("""{"rate_limits":{"seven_day":{"used_percentage":41}}}""");

        Assert.Null(snapshot.FiveHour);
        Assert.Equal(41m, snapshot.SevenDay!.UsedPercentage);
    }

    [Fact]
    public void RateLimitsAbsentProducesNoData()
    {
        var snapshot = Parse("""{"session_id":"synthetic-session","cwd":"C:\\Synthetic\\Repo"}""");

        Assert.Null(snapshot.FiveHour);
        Assert.Null(snapshot.SevenDay);
        Assert.False(snapshot.HasUsageData);
    }

    [Fact]
    public void IndividualWindowAbsentIsValid()
    {
        var snapshot = Parse("""{"rate_limits":{"five_hour":{"used_percentage":5}}}""");

        Assert.Equal(5m, snapshot.FiveHour!.UsedPercentage);
        Assert.Null(snapshot.SevenDay);
    }

    [Fact]
    public void NullRateLimitValuesAreUnavailable()
    {
        var snapshot = Parse("""{"rate_limits":{"five_hour":null,"seven_day":null}}""");

        Assert.Null(snapshot.FiveHour);
        Assert.Null(snapshot.SevenDay);
    }

    [Fact]
    public void DecimalPercentagesAreAccepted()
    {
        var snapshot = Parse("""{"rate_limits":{"five_hour":{"used_percentage":24.5}}}""");

        Assert.Equal(24.5m, snapshot.FiveHour!.UsedPercentage);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    public void BoundaryPercentagesAreAccepted(string percentage, decimal expected)
    {
        var snapshot = Parse($"{{\"rate_limits\":{{\"five_hour\":{{\"used_percentage\":{percentage}}}}}}}");

        Assert.Equal(expected, snapshot.FiveHour!.UsedPercentage);
    }

    [Fact]
    public void NegativePercentageIsRejected()
    {
        var exception = Assert.Throws<ClaudeStatusLineUsageParseException>(() =>
            Parse("""{"rate_limits":{"five_hour":{"used_percentage":-0.1}}}"""));

        Assert.Equal(ClaudeStatusLineUsageParseError.InvalidPercentage, exception.Error);
    }

    [Fact]
    public void PercentageAboveOneHundredIsRejected()
    {
        var exception = Assert.Throws<ClaudeStatusLineUsageParseException>(() =>
            Parse("""{"rate_limits":{"five_hour":{"used_percentage":100.1}}}"""));

        Assert.Equal(ClaudeStatusLineUsageParseError.InvalidPercentage, exception.Error);
    }

    [Theory]
    [InlineData("\"24\"")]
    [InlineData("true")]
    [InlineData("{}")]
    public void WrongPercentageValueTypesAreRejected(string value)
    {
        var exception = Assert.Throws<ClaudeStatusLineUsageParseException>(() =>
            Parse($"{{\"rate_limits\":{{\"five_hour\":{{\"used_percentage\":{value}}}}}}}"));

        Assert.Equal(ClaudeStatusLineUsageParseError.InvalidPercentage, exception.Error);
    }

    [Fact]
    public void ValidUnixEpochIsConverted()
    {
        var snapshot = Parse("""{"rate_limits":{"five_hour":{"used_percentage":24,"resets_at":0}}}""");

        Assert.Equal(DateTimeOffset.UnixEpoch, snapshot.FiveHour!.ResetAt);
    }

    [Theory]
    [InlineData("253402300800")]
    [InlineData("9223372036854775808")]
    [InlineData("123.4")]
    [InlineData("\"0\"")]
    public void InvalidAndOverflowingEpochsAreRejected(string value)
    {
        var exception = Assert.Throws<ClaudeStatusLineUsageParseException>(() =>
            Parse($"{{\"rate_limits\":{{\"five_hour\":{{\"used_percentage\":24,\"resets_at\":{value}}}}}}}"));

        Assert.Equal(ClaudeStatusLineUsageParseError.InvalidResetTime, exception.Error);
    }

    [Fact]
    public void MalformedJsonIsRejectedSafely()
    {
        var exception = Assert.Throws<ClaudeStatusLineUsageParseException>(() =>
            Parse("""{"rate_limits":{"five_hour":"""));

        Assert.Equal(ClaudeStatusLineUsageParseError.MalformedJson, exception.Error);
        Assert.DoesNotContain("five_hour", exception.Message);
    }

    [Fact]
    public void UnknownNestedFieldsAreIgnored()
    {
        var snapshot = Parse("""
            {
              "rate_limits": {
                "five_hour": {
                  "used_percentage": 24,
                  "nested": { "cwd": "C:\\Synthetic\\Repo", "transcript_path": "C:\\Synthetic\\Transcript.jsonl" }
                }
              }
            }
            """);

        Assert.Equal(24m, snapshot.FiveHour!.UsedPercentage);
    }

    [Fact]
    public void SensitiveFieldsAreAbsentFromParsedState()
    {
        var payload = """
            {
              "cwd": "C:\\Synthetic\\Repo",
              "workspace": "/synthetic/workspace",
              "session_id": "synthetic-session-id",
              "session_name": "synthetic-session-name",
              "prompt_id": "synthetic-prompt-id",
              "transcript_path": "/synthetic/transcript.jsonl",
              "model": "synthetic-model",
              "cost": { "usd": 12.34 },
              "agent": "synthetic-agent",
              "pr": "synthetic-pr",
              "worktree": "synthetic-worktree",
              "rate_limits": { "five_hour": { "used_percentage": 24 } }
            }
            """;

        var snapshot = Parse(payload);
        var stateText = snapshot.ToString();

        Assert.Equal(24m, snapshot.FiveHour!.UsedPercentage);
        Assert.DoesNotContain("synthetic-session-id", stateText);
        Assert.DoesNotContain("synthetic-prompt-id", stateText);
        Assert.DoesNotContain("Synthetic", stateText);
        Assert.DoesNotContain("synthetic-model", stateText);
    }

    [Fact]
    public void ParserExceptionsDoNotContainRawPayloadFragments()
    {
        var exception = Assert.Throws<ClaudeStatusLineUsageParseException>(() =>
            Parse("""{"rate_limits":{"five_hour":{"used_percentage":"synthetic-secret-fragment"}}}"""));

        Assert.DoesNotContain("synthetic-secret-fragment", exception.ToString());
        Assert.DoesNotContain("used_percentage", exception.ToString());
    }

    [Fact]
    public void TimeProviderSuppliesObservationTime()
    {
        var timeProvider = new ManualTimeProvider(AcceptedAt);

        var snapshot = _parser.Parse(
            """{"rate_limits":{"five_hour":{"used_percentage":24}}}""",
            timeProvider);

        Assert.Equal(AcceptedAt, snapshot.FiveHour!.ObservedAt);
    }

    [Fact]
    public void ExplicitObservationTimeIsUsed()
    {
        var snapshot = Parse("""{"rate_limits":{"five_hour":{"used_percentage":24}}}""");

        Assert.Equal(AcceptedAt, snapshot.FiveHour!.ObservedAt);
    }

    [Fact]
    public void PercentageAndResetTimeAreIndependentlyOptionalInParserModel()
    {
        var snapshot = Parse("""
            {
              "rate_limits": {
                "five_hour": { "resets_at": 0 },
                "seven_day": { "used_percentage": 41, "resets_at": null }
              }
            }
            """);

        Assert.Null(snapshot.FiveHour!.UsedPercentage);
        Assert.Equal(DateTimeOffset.UnixEpoch, snapshot.FiveHour.ResetAt);
        Assert.Equal(41m, snapshot.SevenDay!.UsedPercentage);
        Assert.Null(snapshot.SevenDay.ResetAt);
    }

    private ClaudeStatusLineUsageSnapshot Parse(string json) =>
        _parser.Parse(Encoding.UTF8.GetBytes(json), AcceptedAt);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
