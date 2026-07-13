using System.Globalization;

namespace TokenFish.Providers.Claude;

internal sealed class ClaudeBridgeStatusLineRenderer
{
    public string Render(ClaudeBridgeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var parts = new List<string>(3)
        {
            "TokenFish"
        };

        if (state.FiveHour is not null)
        {
            parts.Add($"Claude 5h {FormatPercentage(state.FiveHour.UsedPercentage)}%");
        }

        if (state.SevenDay is not null)
        {
            parts.Add($"7d {FormatPercentage(state.SevenDay.UsedPercentage)}%");
        }

        if (parts.Count == 1)
        {
            parts.Add("Claude usage unavailable");
        }

        return string.Join(" | ", parts);
    }

    private static string FormatPercentage(decimal value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);
}
