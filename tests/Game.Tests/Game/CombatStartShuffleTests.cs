using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Content;
using Game.Data.Content;

namespace Game.Tests.Game;

public class CombatStartShuffleTests
{
    [Fact]
    public void CombatStart_PlayerDeck_IsShuffledDeterministically()
    {
        var first = BeginOpeningCombat(seed: 1337);
        var second = BeginOpeningCombat(seed: 1337);

        var firstOrder = GetPlayerDeckOrder(first.Combat!);
        var secondOrder = GetPlayerDeckOrder(second.Combat!);
        var starterOrder = first.RunDeck.Select(card => card.DefinitionId).ToArray();

        Assert.Equal(firstOrder, secondOrder);
        Assert.NotEqual(starterOrder, firstOrder);
    }

    [Fact]
    public void CombatStart_DifferentSeeds_CanProduceDifferentOpeningOrder()
    {
        var seeds = Enumerable.Range(1, 12).ToArray();
        var orders = seeds
            .Select(seed => GetPlayerDeckOrder(BeginOpeningCombat(seed).Combat!))
            .ToArray();

        var hasDifferentOrder = false;
        for (var i = 0; i < orders.Length && !hasDifferentOrder; i++)
        {
            for (var j = i + 1; j < orders.Length; j++)
            {
                if (!orders[i].SequenceEqual(orders[j]))
                {
                    hasDifferentOrder = true;
                    break;
                }
            }
        }

        Assert.True(hasDifferentOrder);
    }

    private static GameState BeginOpeningCombat(int seed)
    {
        var started = GameStateTestFactory.CreateStartedRun(seed);
        var content = StaticGameContentProvider.LoadDefault();
        var action = new BeginCombatAction(PlaytestContent.OpeningCombat, content.CardDefinitions, content.RewardCardPool);
        return GameReducer.Reduce(started, action).NewState;
    }

    private static IReadOnlyList<CardId> GetPlayerDeckOrder(global::Game.Core.Combat.CombatState combat)
    {
        return combat.Player.Deck.Hand
            .Concat(combat.Player.Deck.DrawPile)
            .Select(card => card.DefinitionId)
            .ToArray();
    }
}
