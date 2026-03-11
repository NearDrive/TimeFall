using System.Collections.Immutable;
using System.IO;
using Game.Cli;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Game;
using Game.Core.Rewards;
using Game.Core.Map;
using Game.Data.Content;

namespace Game.Tests.Cli;

public sealed class CardRenderingReadabilityTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void HandRendering_DoesNotShowSpecialEffectPlaceholder()
    {
        var state = CreateCombatStateWithCard(new CardId("blades-blade-tempo"));

        var output = CaptureConsole(() => CliRenderer.RenderHand(state, Content.CardDefinitions));

        Assert.Contains("Blade Tempo — Gain 3 gm. Next attack deals +3 damage.", output);
        Assert.DoesNotContain("Special effect", output);
    }

    [Fact]
    public void RewardRendering_UsesReadableRulesText()
    {
        var state = GameStateTestFactory.CreateStartedRun() with
        {
            Reward = new RewardState(
                RewardType.CardChoice,
                ImmutableList.Create(new CardId("blades-focus"), new CardId("blades-twin-slash")),
                false,
                new NodeId("combat-1"))
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));

        Assert.Contains("Focus — Draw 1 card. Gain 2 gm.", output);
        Assert.Contains("Twin Slash — Requires Momentum 2. Deal 5 damage twice.", output);
    }

    [Fact]
    public void DeckRendering_UsesReadableRulesText()
    {
        var state = GameStateTestFactory.CreateStartedRun();

        var output = CaptureConsole(() => CliRenderer.RenderDeck(state, Content.CardDefinitions));

        Assert.Contains("Strike — Deal 5 damage.", output);
        Assert.DoesNotContain("Special effect", output);
    }

    [Fact]
    public void FallbackText_DoesNotExposeRawEffectTypeNames()
    {
        var card = new CardDefinition(
            new CardId("test-fallback"),
            "Fallback Card",
            0,
            [new DamageNTimesCardEffect(4, 3, CardTarget.Opponent), new NextAttackBonusDamageThisTurnCardEffect(3, CardTarget.Self)],
            RulesText: "");

        var rendered = CardRulesTextFormatter.GetReadableRulesText(card);

        Assert.Equal("Deal 4 damage three times. Next attack gains +3 damage (self)", rendered);
        Assert.DoesNotContain("DamageNTimesCardEffect", rendered);
        Assert.DoesNotContain("NextAttackBonusDamageThisTurn", rendered);
        Assert.DoesNotContain("Special effect", rendered);
    }

    [Fact]
    public void RepresentativeCompositeCard_RendersClearly()
    {
        var card = Content.CardDefinitions[new CardId("blades-bleeding-cut")];

        var rendered = CardRulesTextFormatter.GetReadableRulesText(card);

        Assert.Equal("Deal 5 damage. Apply Bleed 3.", rendered);
    }

    private static GameState CreateCombatStateWithCard(CardId cardId)
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
                    cardId,
                    new CardId("blades-strike"),
                    new CardId("blades-guard"),
                    new CardId("blades-focus"),
                    new CardId("blades-quick-slash"),
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
