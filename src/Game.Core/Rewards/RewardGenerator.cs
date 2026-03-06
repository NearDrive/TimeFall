using Game.Core.Cards;
using Game.Core.Common;
using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.Rewards;

public static class RewardGenerator
{
    public static (RewardState RewardState, GameRng Rng) CreateCardChoiceReward(
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions,
        GameRng rng,
        NodeId? sourceNodeId)
    {
        var cardIds = cardDefinitions.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
        if (cardIds.Length == 0)
        {
            throw new InvalidOperationException("Cannot generate card reward without card definitions.");
        }

        var options = ImmutableList.CreateBuilder<CardId>();
        var seen = new HashSet<CardId>();

        while (options.Count < 3)
        {
            var (index, nextRng) = rng.NextInt(0, cardIds.Length);
            rng = nextRng;
            var candidate = cardIds[index];

            if (seen.Add(candidate) || seen.Count == cardIds.Length)
            {
                options.Add(candidate);
            }
        }

        return (new RewardState(RewardType.CardChoice, options.ToImmutable(), false, sourceNodeId), rng);
    }
}
