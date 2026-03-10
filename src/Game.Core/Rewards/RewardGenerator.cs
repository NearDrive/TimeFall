using Game.Core.Cards;
using Game.Core.Common;
using CardId = Game.Core.Cards.CardId;
using NodeId = Game.Core.Map.NodeId;
using System.Collections.Immutable;

namespace Game.Core.Rewards;

public static class RewardGenerator
{
    private static bool IsRewardEligible(CardDefinition definition)
    {
        return !string.IsNullOrWhiteSpace(definition.DeckAffinity);
    }

    public static (RewardState RewardState, GameRng Rng) CreateCardChoiceReward(
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions,
        IReadOnlyList<CardId> rewardCardPool,
        GameRng rng,
        NodeId? sourceNodeId)
    {
        var cardIds = rewardCardPool
            .Where(cardDefinitions.ContainsKey)
            .Where(cardId => IsRewardEligible(cardDefinitions[cardId]))
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        if (cardIds.Length == 0)
        {
            throw new InvalidOperationException("Cannot generate card reward without a configured reward pool.");
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
