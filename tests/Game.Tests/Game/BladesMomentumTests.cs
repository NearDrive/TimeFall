using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Tests.Game;

public class BladesMomentumTests
{
    [Fact] public void DerivedMomentum_ComputesCorrectlyFromGm() { Assert.Equal(3, MomentumMath.DerivedMomentumFromGm(5)); }

    [Fact] public void AttackLabel_GeneratesOneGm()
    {
        var cardId = new CardId("a");
        var defs = new Dictionary<CardId, CardDefinition> { [cardId] = new(cardId, "A", 0, [new DamageCardEffect(1, CardTarget.Opponent)], new NoCost(), new HashSet<string>{"Attack"}) };
        var state = BuildState(cardId, defs, 0);
        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        Assert.Equal(1, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    [Fact] public void ExplicitGmGain_AndAttackLabel_BothApply()
    {
        var cardId = new CardId("a");
        var defs = new Dictionary<CardId, CardDefinition> { [cardId] = new(cardId, "A", 0, [new GainGeneratedMomentumCardEffect(2, CardTarget.Self)], new NoCost(), new HashSet<string>{"Attack"}) };
        var state = BuildState(cardId, defs, 0);
        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        Assert.Equal(3, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    [Fact] public void SpendAllMomentum_SetsGmToZero()
    {
        var cardId = new CardId("a");
        var defs = new Dictionary<CardId, CardDefinition> { [cardId] = new(cardId, "A", 0, [new DamageCardEffect(1, CardTarget.Opponent)], new SpendAllMomentumCost(), new HashSet<string>{"Attack"}) };
        var state = BuildState(cardId, defs, 8);
        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        Assert.Equal(1, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    [Fact] public void EndTurnWithoutAttack_DecaysGmByHalf()
    {
        var state = BuildState(new CardId("a"), new Dictionary<CardId, CardDefinition>(), 10);
        var result = GameReducer.Reduce(state, new EndTurnAction());
        Assert.Equal(5, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    private static GameState BuildState(CardId cardId, IReadOnlyDictionary<CardId, CardDefinition> defs, int gm)
    {
        var player = new CombatEntity("p", 50, 50, 0, new Dictionary<ResourceType, int> { [ResourceType.Momentum] = gm }.ToImmutableDictionary(), new DeckState([new CardInstance(cardId)], [new CardInstance(cardId)], [], [], 0));
        var enemy = new CombatEntity("e", 50, 50, 0, new Dictionary<ResourceType, int>().ToImmutableDictionary(), new DeckState([], [], [], [], 0));
        return GameState.Initial with { Phase = GamePhase.Combat, Combat = new CombatState(TurnOwner.Player, player, enemy, false, 0), CardDefinitions = defs };
    }
}
