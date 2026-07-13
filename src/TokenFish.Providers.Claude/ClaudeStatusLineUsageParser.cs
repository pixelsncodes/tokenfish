using System.Text;
using System.Text.Json;

namespace TokenFish.Providers.Claude;

internal sealed class ClaudeStatusLineUsageParser
{
    public ClaudeStatusLineUsageSnapshot Parse(
        string statusLineJson,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(statusLineJson);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return Parse(Encoding.UTF8.GetBytes(statusLineJson), timeProvider.GetUtcNow());
    }

    public ClaudeStatusLineUsageSnapshot Parse(
        ReadOnlySpan<byte> statusLineJsonUtf8,
        DateTimeOffset acceptedAtUtc)
    {
        var reader = new Utf8JsonReader(
            statusLineJsonUtf8,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });

        try
        {
            var snapshot = ParseRoot(ref reader, acceptedAtUtc.ToUniversalTime());
            if (reader.Read())
            {
                throw CreateMalformedJsonException();
            }

            return snapshot;
        }
        catch (JsonException)
        {
            throw CreateMalformedJsonException();
        }
        catch (InvalidOperationException)
        {
            throw CreateMalformedJsonException();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ClaudeStatusLineUsageParseException(
                ClaudeStatusLineUsageParseError.InvalidResetTime);
        }
    }

    private static ClaudeStatusLineUsageSnapshot ParseRoot(
        ref Utf8JsonReader reader,
        DateTimeOffset acceptedAtUtc)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw CreateMalformedJsonException();
        }

        ClaudeStatusLineQuotaWindowObservation? fiveHour = null;
        ClaudeStatusLineQuotaWindowObservation? sevenDay = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new ClaudeStatusLineUsageSnapshot(fiveHour, sevenDay);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw CreateMalformedJsonException();
            }

            if (reader.ValueTextEquals("rate_limits"u8))
            {
                (fiveHour, sevenDay) = ParseRateLimits(ref reader, acceptedAtUtc);
            }
            else
            {
                SkipPropertyValue(ref reader);
            }
        }

        throw CreateMalformedJsonException();
    }

    private static (
        ClaudeStatusLineQuotaWindowObservation? FiveHour,
        ClaudeStatusLineQuotaWindowObservation? SevenDay) ParseRateLimits(
            ref Utf8JsonReader reader,
            DateTimeOffset acceptedAtUtc)
    {
        if (!reader.Read())
        {
            throw CreateMalformedJsonException();
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return (null, null);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new ClaudeStatusLineUsageParseException(
                ClaudeStatusLineUsageParseError.InvalidShape);
        }

        ClaudeStatusLineQuotaWindowObservation? fiveHour = null;
        ClaudeStatusLineQuotaWindowObservation? sevenDay = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return (fiveHour, sevenDay);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw CreateMalformedJsonException();
            }

            if (reader.ValueTextEquals("five_hour"u8))
            {
                fiveHour = ParseWindow(ref reader, acceptedAtUtc);
            }
            else if (reader.ValueTextEquals("seven_day"u8))
            {
                sevenDay = ParseWindow(ref reader, acceptedAtUtc);
            }
            else
            {
                SkipPropertyValue(ref reader);
            }
        }

        throw CreateMalformedJsonException();
    }

    private static ClaudeStatusLineQuotaWindowObservation? ParseWindow(
        ref Utf8JsonReader reader,
        DateTimeOffset acceptedAtUtc)
    {
        if (!reader.Read())
        {
            throw CreateMalformedJsonException();
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new ClaudeStatusLineUsageParseException(
                ClaudeStatusLineUsageParseError.InvalidShape);
        }

        decimal? usedPercentage = null;
        DateTimeOffset? resetAt = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new ClaudeStatusLineQuotaWindowObservation(
                    usedPercentage,
                    resetAt,
                    acceptedAtUtc);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw CreateMalformedJsonException();
            }

            if (reader.ValueTextEquals("used_percentage"u8))
            {
                usedPercentage = ReadOptionalPercentage(ref reader);
            }
            else if (reader.ValueTextEquals("resets_at"u8))
            {
                resetAt = ReadOptionalResetAt(ref reader);
            }
            else
            {
                SkipPropertyValue(ref reader);
            }
        }

        throw CreateMalformedJsonException();
    }

    private static decimal? ReadOptionalPercentage(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            throw CreateMalformedJsonException();
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetDecimal(out var percentage))
        {
            throw new ClaudeStatusLineUsageParseException(
                ClaudeStatusLineUsageParseError.InvalidPercentage);
        }

        if (percentage is < 0m or > 100m)
        {
            throw new ClaudeStatusLineUsageParseException(
                ClaudeStatusLineUsageParseError.InvalidPercentage);
        }

        return percentage;
    }

    private static DateTimeOffset? ReadOptionalResetAt(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            throw CreateMalformedJsonException();
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out var epochSeconds))
        {
            throw new ClaudeStatusLineUsageParseException(
                ClaudeStatusLineUsageParseError.InvalidResetTime);
        }

        return DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToUniversalTime();
    }

    private static void SkipPropertyValue(ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            throw CreateMalformedJsonException();
        }

        reader.Skip();
    }

    private static ClaudeStatusLineUsageParseException CreateMalformedJsonException() =>
        new(ClaudeStatusLineUsageParseError.MalformedJson);
}
