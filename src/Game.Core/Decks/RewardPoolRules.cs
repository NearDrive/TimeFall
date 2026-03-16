using System.Collections.Immutable;
using Game.Core.Cards;

namespace Game.Core.Decks;

public static class RewardPoolRules
{
    public const int MinEnabledCards = 20;
    public const int MaxEnabledCards = 30;

    public static ImmutableList<CardId> NormalizeEnabled(IReadOnlyList<CardId> source, IReadOnlyList<CardId> allowedPool)
    {
        var allowed = allowedPool.ToHashSet();
        return source
            .Where(allowed.Contains)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToImmutableList();
    }

    public static bool TryValidate(IReadOnlyList<CardId> enabled, int totalAvailable, out string error)
    {
        if (enabled.Count != enabled.Distinct().Count())
        {
            error = "Enabled reward pool must contain unique cards only.";
            return false;
        }

        if (enabled.Count > MaxEnabledCards)
        {
            error = $"Enabled reward pool cannot exceed {MaxEnabledCards} cards.";
            return false;
        }

        if (totalAvailable >= MinEnabledCards && enabled.Count < MinEnabledCards)
        {
            error = $"Enabled reward pool must contain at least {MinEnabledCards} cards for this deck.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static ImmutableList<CardId> AutofillMin(IReadOnlyList<CardId> currentEnabled, IReadOnlyList<CardId> allowedPool)
    {
        var normalized = NormalizeEnabled(currentEnabled, allowedPool);
        var target = Math.Min(MinEnabledCards, allowedPool.Count);
        if (normalized.Count >= target)
        {
            return normalized;
        }

        var enabledSet = normalized.ToHashSet();
        var additions = allowedPool
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .Where(id => !enabledSet.Contains(id))
            .Take(target - normalized.Count);
        return normalized.AddRange(additions).Distinct().OrderBy(id => id.Value, StringComparer.Ordinal).ToImmutableList();
    }

    public static ImmutableList<CardId> AutofillMax(IReadOnlyList<CardId> allowedPool)
    {
        return allowedPool
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .Take(MaxEnabledCards)
            .ToImmutableList();
    }
}
