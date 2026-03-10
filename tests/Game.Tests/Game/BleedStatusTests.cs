using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Tests.Game;

public sealed class BleedStatusTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void BleedingCut_AppliesBleedStatus()
    {
        var state = BeginCombatWithBleedingCutInHand();

        var (afterPlay, events) = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.Equal(3, afterPlay.Combat!.Enemy.Bleed);
        Assert.Contains(events, e => e is StatusApplied { StatusName: "Bleed", Amount: 3, Target: TurnOwner.Enemy });
    }

    [Fact]
    public void Bleed_TicksAtExpectedTiming()
    {
        var state = BeginCombatWithBleedingCutInHand();
        var (afterPlay, _) = GameReducer.Reduce(state, new PlayCardAction(0));
        var enemyHpAfterPlay = afterPlay.Combat!.Enemy.HP;

        var (afterEndTurn, events) = GameReducer.Reduce(afterPlay, new EndTurnAction());

        Assert.Equal(enemyHpAfterPlay - 3, afterEndTurn.Combat!.Enemy.HP);
        Assert.Equal(2, afterEndTurn.Combat.Enemy.Bleed);
        Assert.Contains(events, e => e is TurnEnded { NextTurnOwner: TurnOwner.Enemy });
        Assert.Contains(events, e => e is StatusTriggered { Target: TurnOwner.Enemy, StatusName: "Bleed", Amount: 3 });
    }

    private static GameState BeginCombatWithBleedingCutInHand()
    {
        var blueprint = new CombatBlueprint(
            Player: new CombatantBlueprint(
                EntityId: "player",
                HP: 30,
                MaxHP: 30,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile:
                [
                    new CardId("blades-bleeding-cut"),
                    new CardId("strike"),
                    new CardId("strike"),
                    new CardId("strike"),
                    new CardId("strike"),
                ]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy",
                HP: 30,
                MaxHP: 30,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: []));

        var (state, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, Content.CardDefinitions));
        return state;
    }
}
