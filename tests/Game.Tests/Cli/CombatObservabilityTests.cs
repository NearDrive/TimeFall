using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Game.Cli;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Tests.Cli;

public sealed class CombatObservabilityTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void DamageRendering_ShowsCorrectBlockedAmount()
    {
        var state = BeginCombatWithTargetArmor(armor: 2, enemyHp: 23);
        var (afterPlay, events) = GameReducer.Reduce(state, new PlayCardAction(0));
        var strike = Assert.IsType<PlayerStrikePlayed>(events.First(e => e is PlayerStrikePlayed));

        var hpDelta = strike.EnemyHpBeforeHit - strike.EnemyHpAfterHit;
        Assert.Equal(strike.Damage - hpDelta, strike.DamageBlockedByArmor);

        var rendered = CliRenderer.FormatEvent(strike, Content.CardDefinitions);
        Assert.Contains("5 incoming", rendered);
        Assert.Contains("Enemy HP 23 -> 20", rendered);
        Assert.Contains("Armor 2 -> 1", rendered);
        Assert.Contains("2 blocked", rendered);
        Assert.Equal(20, afterPlay.Combat!.Enemy.HP);
    }

    [Fact]
    public void CombatRenderer_ShowsPlayerAndEnemyStatuses()
    {
        var baseState = CreateCombatState();
        var state = baseState with
        {
            Combat = baseState.Combat! with
            {
                Player = baseState.Combat.Player with { Bleed = 1 },
                Enemy = baseState.Combat.Enemy with { Bleed = 3 },
            },
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));

        Assert.Contains("Player statuses: Bleed 1", output);
        Assert.Contains("Enemy statuses: Bleed 3", output);
    }

    [Fact]
    public void BleedStatus_IsVisibleInCombatState()
    {
        var state = BeginCombatWithBleedingCut();
        var (afterPlay, _) = GameReducer.Reduce(state, new PlayCardAction(0));

        var output = CaptureConsole(() => CliRenderer.RenderState(afterPlay, [], Content.CardDefinitions));

        Assert.Contains("Enemy statuses: Bleed 3", output);
    }

    [Fact]
    public void BleedTick_IsLogged()
    {
        var state = BeginCombatWithBleedingCut();
        var (afterPlay, _) = GameReducer.Reduce(state, new PlayCardAction(0));
        var (_, events) = GameReducer.Reduce(afterPlay, new EndTurnAction());

        var output = CaptureConsole(() => CliRenderer.RenderState(CreateCombatState(), events, Content.CardDefinitions));

        Assert.Contains("Bleed triggers for 3 damage", output);
    }

    [Fact]
    public void StatusList_Rendering_IsDeterministic()
    {
        var combat = CreateCombatState().Combat! with
        {
            Enemy = CreateCombatState().Combat!.Enemy with { Bleed = 2, ReflectNextEnemyAttackDamage = 1 },
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(CreateCombatState() with { Combat = combat }, [], Content.CardDefinitions));

        Assert.Contains("Enemy statuses: Bleed 2, Reflect 1", output);
    }

    [Fact]
    public void CLIShowsReshuffleEvents()
    {
        var fatigue = CliRenderer.FormatEvent(new ReshuffleFatigueApplied(TurnOwner.Player, 2), Content.CardDefinitions);
        var discard = CliRenderer.FormatEvent(new FatigueDiscardResolved(TurnOwner.Player, 2), Content.CardDefinitions);

        Assert.Contains("Reshuffle Fatigue 2", fatigue);
        Assert.Contains("due to fatigue", discard);
    }

    private static GameState BeginCombatWithBleedingCut()
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

        return GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, Content.CardDefinitions)).NewState;
    }

    private static GameState BeginCombatWithTargetArmor(int armor, int enemyHp)
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
                HP: enemyHp,
                MaxHP: enemyHp,
                Armor: armor,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: []));

        return GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, Content.CardDefinitions)).NewState;
    }

    private static GameState CreateCombatState()
    {
        return BeginCombatWithBleedingCut();
    }

    private static string CaptureConsole(Action act)
    {
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            act();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
