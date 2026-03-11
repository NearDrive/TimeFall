using Game.Core.Cards;

namespace Game.Cli;

internal static class CardRarityFormatter
{
    private const string Reset = "\u001b[0m";

    public static bool? ForceAnsiColors { get; set; }

    public static string FormatPrefix(CardDefinition definition)
    {
        var marker = GetMarker(definition.Rarity);
        if (!ShouldUseAnsiColors())
        {
            return marker;
        }

        var colorCode = GetAnsiColorCode(definition.Rarity);
        return colorCode is null ? marker : $"{colorCode}{marker}{Reset}";
    }

    private static string GetMarker(string? rarity)
    {
        return NormalizeRarity(rarity) switch
        {
            "common" => "[C]",
            "uncommon" => "[U]",
            "rare" => "[R]",
            "epic" => "[E]",
            "legendary" => "[L]",
            _ => "[?]",
        };
    }

    private static string? GetAnsiColorCode(string? rarity)
    {
        return NormalizeRarity(rarity) switch
        {
            "common" => "\u001b[37m",
            "uncommon" => "\u001b[32m",
            "rare" => "\u001b[33m",
            "epic" => "\u001b[35m",
            "legendary" => "\u001b[93m",
            _ => null,
        };
    }

    private static bool ShouldUseAnsiColors()
    {
        if (ForceAnsiColors.HasValue)
        {
            return ForceAnsiColors.Value;
        }

        if (Console.IsOutputRedirected)
        {
            return false;
        }

        var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
        return string.IsNullOrWhiteSpace(noColor);
    }

    private static string NormalizeRarity(string? rarity)
    {
        return string.IsNullOrWhiteSpace(rarity)
            ? string.Empty
            : rarity.Trim().ToLowerInvariant();
    }
}
