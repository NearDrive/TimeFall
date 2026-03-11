using System.Collections.Immutable;
using System.IO;
using Game.Cli;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Content;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.Rewards;
using Game.Data.Content;

namespace Game.Tests.Cli;

public sealed class CardRenderingReadabilityTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void HandRendering_ShowsRarityMarker()
    {
        var state = CreateCombatStateWithCard(new CardId("blades-strike"));

        var output = CaptureConsole(() => CliRenderer.RenderHand(state, Content.CardDefinitions));

        Assert.Contains("[C] Strike", output);
    }

    [Fact]
    public void RewardRendering_ShowsRarityMarker()
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

        Assert.Contains("[C] Focus", output);
        Assert.Contains("[U] Twin Slash", output);
    }

    [Fact]
    public void DeckRendering_ShowsRarityMarker()
    {
        var state = GameStateTestFactory.CreateStartedRun();

        var output = CaptureConsole(() => CliRenderer.RenderDeck(state, Content.CardDefinitions));

        Assert.Contains("[C] Strike", output);
    }

    [Fact]
    public void RepresentativeCards_ShowExpectedMarkers()
    {
        var cards = new Dictionary<CardId, CardDefinition>
        {
            [new CardId("common-card")] = CreateCard("common-card", "Common", "Common Card"),
            [new CardId("uncommon-card")] = CreateCard("uncommon-card", "Uncommon", "Uncommon Card"),
            [new CardId("rare-card")] = CreateCard("rare-card", "Rare", "Rare Card"),
            [new CardId("epic-card")] = CreateCard("epic-card", "Epic", "Epic Card"),
            [new CardId("legendary-card")] = CreateCard("legendary-card", "Legendary", "Legendary Card"),
        };

        var state = GameStateTestFactory.CreateStartedRun() with
        {
            RunDeck =
            [
                new CardInstance(new CardId("common-card")),
                new CardInstance(new CardId("uncommon-card")),
                new CardInstance(new CardId("rare-card")),
                new CardInstance(new CardId("epic-card")),
                new CardInstance(new CardId("legendary-card")),
            ]
        };

        var output = CaptureConsole(() => CliRenderer.RenderDeck(state, cards));

        Assert.Contains("[C] Common Card", output);
        Assert.Contains("[U] Uncommon Card", output);
        Assert.Contains("[R] Rare Card", output);
        Assert.Contains("[E] Epic Card", output);
        Assert.Contains("[L] Legendary Card", output);
    }

    [Fact]
    public void PlainTextRendering_RemainsReadableWithoutColor()
    {
        CardRarityFormatter.ForceAnsiColors = false;
        var state = CreateCombatStateWithCard(new CardId("blades-strike"));

        try
        {
            var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));

            Assert.DoesNotContain("\u001b[", output);
            Assert.Contains("[C] Strike", output);
            Assert.Contains("Hand preview:", output);
        }
        finally
        {
            CardRarityFormatter.ForceAnsiColors = null;
        }
    }

    [Fact]
    public void ANSIFormatting_CanBeDisabledWithoutLosingRarityInfo()
    {
        var state = CreateCombatStateWithCard(new CardId("blades-twin-slash"));

        try
        {
            CardRarityFormatter.ForceAnsiColors = true;
            var withAnsi = CaptureConsole(() => CliRenderer.RenderHand(state, Content.CardDefinitions));
            Assert.Contains("\u001b[", withAnsi);
            Assert.Contains("[U] Twin Slash", withAnsi);

            CardRarityFormatter.ForceAnsiColors = false;
            var withoutAnsi = CaptureConsole(() => CliRenderer.RenderHand(state, Content.CardDefinitions));
            Assert.DoesNotContain("\u001b[", withoutAnsi);
            Assert.Contains("[U] Twin Slash", withoutAnsi);
        }
        finally
        {
            CardRarityFormatter.ForceAnsiColors = null;
        }
    }

    private static CardDefinition CreateCard(string id, string rarity, string name)
    {
        return new CardDefinition(
            new CardId(id),
            name,
            0,
            [new DamageCardEffect(5, CardTarget.Opponent)],
            Rarity: rarity,
            RulesText: "Deal 5 damage.");
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
