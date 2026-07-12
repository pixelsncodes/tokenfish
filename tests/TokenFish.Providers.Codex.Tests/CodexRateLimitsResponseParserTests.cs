using TokenFish.Providers.Codex.RateLimits;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexRateLimitsResponseParserTests
{
    private const string RequestId = "rate-limits-1";

    private readonly CodexRateLimitsResponseParser _parser = new();

    [Fact]
    public void ParsesPrimaryRateLimitWindow()
    {
        var snapshot = _parser.Parse(CreateSuccessResponse(primaryUsedPercent: 42), RequestId);

        Assert.True(snapshot.Primary.IsAvailable);
        Assert.Equal(42, snapshot.Primary.UsedPercent);
        Assert.Equal(300, snapshot.Primary.WindowDurationMins);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_785_000_000), snapshot.Primary.ResetsAt);
        Assert.Equal("rate_limit_reached", snapshot.RateLimitReachedType);
    }

    [Fact]
    public void ParsesSecondaryRateLimitWindow()
    {
        var snapshot = _parser.Parse(CreateSuccessResponse(secondaryUsedPercent: 84), RequestId);

        Assert.True(snapshot.Secondary.IsAvailable);
        Assert.Equal(84, snapshot.Secondary.UsedPercent);
        Assert.Equal(10_080, snapshot.Secondary.WindowDurationMins);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_785_604_800), snapshot.Secondary.ResetsAt);
    }

    [Fact]
    public void NullSecondaryWindowIsUnavailable()
    {
        var snapshot = _parser.Parse(CreateSuccessResponse(secondaryJson: "null"), RequestId);

        Assert.False(snapshot.Secondary.IsAvailable);
        Assert.Null(snapshot.Secondary.UsedPercent);
        Assert.Null(snapshot.Secondary.WindowDurationMins);
        Assert.Null(snapshot.Secondary.ResetsAt);
    }

    [Fact]
    public void MissingOptionalWindowFieldsRemainUnavailable()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse(primaryJson: """
                {
                  "usedPercent": 55
                }
                """),
            RequestId);

        Assert.True(snapshot.Primary.IsAvailable);
        Assert.Equal(55, snapshot.Primary.UsedPercent);
        Assert.Null(snapshot.Primary.WindowDurationMins);
        Assert.Null(snapshot.Primary.ResetsAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void PercentageBoundariesAreAccepted(int usedPercent)
    {
        var snapshot = _parser.Parse(CreateSuccessResponse(primaryUsedPercent: usedPercent), RequestId);

        Assert.Equal(usedPercent, snapshot.Primary.UsedPercent);
    }

    [Fact]
    public void PercentageBelowZeroIsRejected()
    {
        var exception = Assert.Throws<CodexRateLimitsResponseParseException>(() =>
            _parser.Parse(CreateSuccessResponse(primaryUsedPercent: -1), RequestId));

        Assert.DoesNotContain("usedPercent", exception.Message);
    }

    [Fact]
    public void PercentageAboveOneHundredIsRejected()
    {
        var exception = Assert.Throws<CodexRateLimitsResponseParseException>(() =>
            _parser.Parse(CreateSuccessResponse(primaryUsedPercent: 101), RequestId));

        Assert.DoesNotContain("usedPercent", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ZeroOrNegativeWindowDurationIsRejected(long windowDurationMins)
    {
        var exception = Assert.Throws<CodexRateLimitsResponseParseException>(() =>
            _parser.Parse(CreateSuccessResponse(primaryWindowDurationMins: windowDurationMins), RequestId));

        Assert.DoesNotContain(windowDurationMins.ToString(), exception.Message);
    }

    [Fact]
    public void UnixResetTimestampConvertsToUtc()
    {
        var snapshot = _parser.Parse(CreateSuccessResponse(primaryResetsAt: 1_785_000_000), RequestId);

        Assert.Equal(TimeSpan.Zero, snapshot.Primary.ResetsAt?.Offset);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_785_000_000), snapshot.Primary.ResetsAt);
    }

    [Fact]
    public void NullResetTimestampRemainsUnavailable()
    {
        var snapshot = _parser.Parse(CreateSuccessResponse(primaryResetsAtJson: "null"), RequestId);

        Assert.Null(snapshot.Primary.ResetsAt);
    }

    [Fact]
    public void MismatchedResponseIdIsRejected()
    {
        var exception = Assert.Throws<CodexRateLimitsResponseParseException>(() =>
            _parser.Parse(CreateSuccessResponse(idJson: "\"other-id\""), RequestId));

        Assert.Contains("ID", exception.Message);
        Assert.DoesNotContain("other-id", exception.Message);
    }

    [Fact]
    public void JsonRpcErrorResponseIsRejected()
    {
        var exception = Assert.Throws<CodexRateLimitsResponseParseException>(() =>
            _parser.Parse(
                """
                {"id":"rate-limits-1","error":{"code":-32000,"message":"synthetic sensitive details should not appear"}}
                """,
                RequestId));

        Assert.Equal(-32000, exception.ErrorCode);
        Assert.DoesNotContain("synthetic sensitive details", exception.Message);
    }

    [Fact]
    public void MalformedJsonIsRejected()
    {
        var exception = Assert.Throws<CodexRateLimitsResponseParseException>(() =>
            _parser.Parse("{not valid json", RequestId));

        Assert.DoesNotContain("not valid json", exception.Message);
    }

    [Fact]
    public void UnknownFieldsAreIgnored()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse(extraRateLimitFields: """
                ,
                "displayName": "Synthetic Account",
                "unknownFutureObject": {"futureMetric": "ignored"}
                """),
            RequestId);

        Assert.Equal(42, snapshot.Primary.UsedPercent);
        Assert.Equal(21, snapshot.Secondary.UsedPercent);
    }

    [Fact]
    public void ExceptionMessagesDoNotContainFixtureContent()
    {
        const string fixtureMarker = "FIXTURE_MARKER";
        var exception = Assert.Throws<CodexRateLimitsResponseParseException>(() =>
            _parser.Parse(
                $$"""
                {
                  "id": "rate-limits-1",
                  "result": {
                    "rateLimits": {
                      "primary": {
                        "usedPercent": 101,
                        "windowDurationMins": 300,
                        "resetsAt": 1785000000,
                        "fixtureMarker": "{{fixtureMarker}}"
                      },
                      "secondary": null
                    }
                  }
                }
                """,
                RequestId));

        Assert.DoesNotContain(fixtureMarker, exception.Message);
    }

    private static string CreateSuccessResponse(
        string idJson = "\"rate-limits-1\"",
        int primaryUsedPercent = 42,
        int secondaryUsedPercent = 21,
        long primaryWindowDurationMins = 300,
        long secondaryWindowDurationMins = 10_080,
        long primaryResetsAt = 1_785_000_000,
        string? primaryResetsAtJson = null,
        string? primaryJson = null,
        string? secondaryJson = null,
        string extraRateLimitFields = "") =>
        $$"""
        {
          "id": {{idJson}},
          "result": {
            "rateLimits": {
              "primary": {{primaryJson ?? CreateWindowJson(primaryUsedPercent, primaryWindowDurationMins, primaryResetsAt, primaryResetsAtJson)}},
              "secondary": {{secondaryJson ?? CreateWindowJson(secondaryUsedPercent, secondaryWindowDurationMins, 1_785_604_800, null)}},
              "rateLimitReachedType": "rate_limit_reached"{{extraRateLimitFields}}
            },
            "ignoredMetadata": {
              "displayLabel": "ignored",
              "futureValue": "ignored"
            }
          },
          "jsonrpc": "2.0"
        }
        """;

    private static string CreateWindowJson(
        int usedPercent,
        long windowDurationMins,
        long resetsAt,
        string? resetsAtJson) =>
        $$"""
        {
          "usedPercent": {{usedPercent}},
          "windowDurationMins": {{windowDurationMins}},
          "resetsAt": {{resetsAtJson ?? resetsAt.ToString()}}
        }
        """;
}
