using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexAccountUsageResponseParserTests
{
    private const string RequestId = "usage-1";

    private readonly CodexAccountUsageResponseParser _parser = new();

    [Fact]
    public void OneValidDailyBucketParsesCorrectly()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse("""
                [
                  {"startDate": "2026-07-12", "tokens": 123}
                ]
                """),
            RequestId);

        var bucket = Assert.Single(GetAvailableBuckets(snapshot));
        Assert.True(snapshot.HasDailyUsageBuckets);
        Assert.Equal(new DateOnly(2026, 7, 12), bucket.StartDate);
        Assert.Equal(123, bucket.Tokens);
    }

    [Fact]
    public void MultipleDailyBucketsParseInProviderOrder()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse("""
                [
                  {"startDate": "2026-07-13", "tokens": 200},
                  {"startDate": "2026-07-12", "tokens": 100}
                ]
                """),
            RequestId);

        Assert.Equal(
            new[] { new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 12) },
            GetAvailableBuckets(snapshot).Select(bucket => bucket.StartDate));
    }

    [Fact]
    public void ZeroTokenCountIsAccepted()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse("""
                [
                  {"startDate": "2026-07-12", "tokens": 0}
                ]
                """),
            RequestId);

        Assert.Equal(0, Assert.Single(GetAvailableBuckets(snapshot)).Tokens);
    }

    [Fact]
    public void PositiveTokenCountIsAccepted()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse("""
                [
                  {"startDate": "2026-07-12", "tokens": 987654}
                ]
                """),
            RequestId);

        Assert.Equal(987654, Assert.Single(GetAvailableBuckets(snapshot)).Tokens);
    }

    [Fact]
    public void NegativeTokenCountIsRejected()
    {
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse(
                CreateSuccessResponse("""
                    [
                      {"startDate": "2026-07-12", "tokens": -1}
                    ]
                    """),
                RequestId));

        Assert.DoesNotContain("-1", exception.Message);
    }

    [Fact]
    public void NullDailyUsageBucketsRemainUnavailable()
    {
        var snapshot = _parser.Parse(CreateSuccessResponse("null"), RequestId);

        Assert.False(snapshot.HasDailyUsageBuckets);
        Assert.Null(snapshot.DailyUsageBuckets);
    }

    [Fact]
    public void EmptyDailyUsageBucketsRemainAvailableEmptyCollection()
    {
        var snapshot = _parser.Parse(CreateSuccessResponse("[]"), RequestId);

        Assert.True(snapshot.HasDailyUsageBuckets);
        Assert.Empty(GetAvailableBuckets(snapshot));
    }

    [Fact]
    public void IsoLeapDayDateParsesCorrectly()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse("""
                [
                  {"startDate": "2024-02-29", "tokens": 42}
                ]
                """),
            RequestId);

        Assert.Equal(new DateOnly(2024, 2, 29), Assert.Single(GetAvailableBuckets(snapshot)).StartDate);
    }

    [Theory]
    [InlineData("2026/07/12")]
    [InlineData("07-12-2026")]
    public void MalformedDateIsRejected(string startDate)
    {
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse(CreateSuccessResponse(CreateBucketsJson(startDate, 42)), RequestId));

        Assert.DoesNotContain(startDate, exception.Message);
    }

    [Fact]
    public void ImpossibleCalendarDateIsRejected()
    {
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse(CreateSuccessResponse(CreateBucketsJson("2025-02-29", 42)), RequestId));

        Assert.DoesNotContain("2025-02-29", exception.Message);
    }

    [Fact]
    public void DuplicateDatesAreRejected()
    {
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse(
                CreateSuccessResponse("""
                    [
                      {"startDate": "2026-07-12", "tokens": 100},
                      {"startDate": "2026-07-12", "tokens": 200}
                    ]
                    """),
                RequestId));

        Assert.Contains("duplicate", exception.Message);
        Assert.DoesNotContain("2026-07-12", exception.Message);
    }

    [Fact]
    public void MismatchedResponseIdIsRejected()
    {
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse(CreateSuccessResponse("[]", idJson: "\"other-id\""), RequestId));

        Assert.Contains("ID", exception.Message);
        Assert.DoesNotContain("other-id", exception.Message);
    }

    [Fact]
    public void JsonRpcErrorResponseIsRejected()
    {
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse(
                """
                {"id":"usage-1","error":{"code":-32000,"message":"synthetic details should not appear"}}
                """,
                RequestId));

        Assert.Equal(-32000, exception.ErrorCode);
        Assert.DoesNotContain("synthetic details", exception.Message);
    }

    [Fact]
    public void MalformedJsonIsRejected()
    {
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse("{not valid json", RequestId));

        Assert.DoesNotContain("not valid json", exception.Message);
    }

    [Fact]
    public void UnknownFieldsAreIgnored()
    {
        var snapshot = _parser.Parse(
            CreateSuccessResponse(
                """
                [
                  {"startDate": "2026-07-12", "tokens": 123, "futureMetric": "ignored"}
                ]
                """,
                extraResultFields: """
                    ,
                    "summaryExtension": {"futureValue": "ignored"}
                    """),
            RequestId);

        Assert.Equal(123, Assert.Single(GetAvailableBuckets(snapshot)).Tokens);
    }

    [Fact]
    public void ExceptionMessagesDoNotContainFixtureContent()
    {
        const string fixtureMarker = "FIXTURE_MARKER";
        var exception = Assert.Throws<CodexAccountUsageResponseParseException>(() =>
            _parser.Parse(
                CreateSuccessResponse($$"""
                    [
                      {"startDate": "2026-07-12", "tokens": -1, "fixtureMarker": "{{fixtureMarker}}"}
                    ]
                    """),
                RequestId));

        Assert.DoesNotContain(fixtureMarker, exception.Message);
    }

    private static string CreateSuccessResponse(
        string dailyUsageBucketsJson,
        string idJson = "\"usage-1\"",
        string extraResultFields = "") =>
        $$"""
        {
          "id": {{idJson}},
          "result": {
            "dailyUsageBuckets": {{dailyUsageBucketsJson}},
            "summary": {
              "lifetimeTokens": 999,
              "peakDailyTokens": 123
            }{{extraResultFields}},
            "ignoredMetadata": {
              "displayLabel": "ignored",
              "futureValue": "ignored"
            }
          },
          "jsonrpc": "2.0"
        }
        """;

    private static IReadOnlyList<CodexDailyTokenUsage> GetAvailableBuckets(
        CodexAccountUsageSnapshot snapshot)
    {
        Assert.True(snapshot.HasDailyUsageBuckets);
        return Assert.IsAssignableFrom<IReadOnlyList<CodexDailyTokenUsage>>(snapshot.DailyUsageBuckets);
    }

    private static string CreateBucketsJson(string startDate, long tokens) =>
        $$"""
        [
          {"startDate": "{{startDate}}", "tokens": {{tokens}}}
        ]
        """;
}
