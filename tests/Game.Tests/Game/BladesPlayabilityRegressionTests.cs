using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;
using Game.Data.Content;

namespace Game.Tests.Game;

public sealed class BladesPlayabilityRegressionTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void BladesStarterStrike_IsPlayable_OnPlayerTurn()
    {
        var inCombat = EnterCombatFromBladesRun();
        var hand = inCombat.Combat!.Player.Deck.Hand;
        var strikeIndex = hand.FindIndex(c => c.DefinitionId.Value == "blades-strike");
        Assert.True(strikeIndex >= 0, "Expected a Strike in the opening hand for this deterministic seed.");

        var enemyHpBefore = inCombat.Combat.Enemy.HP;
        var result = GameReducer.Reduce(inCombat, new PlayCardAction(strikeIndex));

        Assert.Equal(TurnOwner.Player, inCombat.Combat.TurnOwner);
        Assert.NotEqual(inCombat, result.NewState);
        Assert.DoesNotContain(result.Events, e => e is PlayCardRejected);
        Assert.Contains(result.Events, e => e is PlayerStrikePlayed);
        Assert.True(result.NewState.Combat!.Enemy.HP < enemyHpBefore);
    }

    [Fact]
    public void ZeroCostBladesCard_CostModel_AllowsPlay()
    {
        var cardId = new CardId("blades-zero");
        var definition = new CardDefinition(cardId, "Zero", 0, [new DamageCardEffect(3, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" });
        var state = BuildSingleCardCombatState(definition, 0);

        var result = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.DoesNotContain(result.Events, e => e is PlayCardRejected);
        Assert.Contains(result.Events, e => e is PlayerStrikePlayed);
    }

    [Fact]
    public void PlayCardAction_RejectionReason_IsSpecific()
    {
        var inCombat = EnterCombatFromBladesRun();

        var result = GameReducer.Reduce(inCombat, new PlayCardAction(999));

        var rejection = Assert.Single(result.Events.OfType<PlayCardRejected>());
        Assert.Equal(PlayCardRejectionReason.InvalidHandIndex, rejection.Reason);
        Assert.Contains("out of range", rejection.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BladesCardDefinitionLookup_ResolvesCorrectlyFromHandInstance()
    {
        var inCombat = EnterCombatFromBladesRun();
        var cardInHand = Assert.Single(inCombat.Combat!.Player.Deck.Hand, c => c.DefinitionId.Value == "blades-strike");

        var found = inCombat.CardDefinitions.TryGetValue(cardInHand.DefinitionId, out var definition);

        Assert.True(found);
        Assert.Equal("Strike", definition!.Name);
        Assert.True(CardEffectResolver.HasResolvableEffects(definition));
    }

    [Fact]
    public void AttackLabel_DoesNotBlockPlayability()
    {
        var cardId = new CardId("attack-only");
        var definition = new CardDefinition(cardId, "AttackOnly", 0, [new DamageCardEffect(1, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" });
        var state = BuildSingleCardCombatState(definition, 0);

        var result = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.DoesNotContain(result.Events, e => e is PlayCardRejected);
        Assert.Equal(1, result.NewState.Combat!.Player.Resources.GetValueOrDefault(ResourceType.Momentum));
        Assert.Contains(result.Events, e => e is ResourceChanged { ResourceType: ResourceType.Momentum, Reason: "Attack label bonus" });
    }

    private static GameState EnterCombatFromBladesRun()
    {
        var initial = GameState.CreateInitial(Content.CardDefinitions, Content.DeckDefinitions, Content.RewardCardPool);
        var selected = GameReducer.Reduce(initial, new SelectDeckAction("deck-blades")).NewState;
        var started = GameReducer.Reduce(selected, new StartRunAction(1337)).NewState;

        var combatNeighbor = started.Map.Graph
            .GetNeighbors(started.Map.CurrentNodeId)
            .First(nodeId => started.Map.Graph.TryGetNode(nodeId, out var node) && node is { Type: NodeType.Combat });

        var inCombat = GameReducer.Reduce(started, new MoveToNodeAction(combatNeighbor)).NewState;
        Assert.Equal(GamePhase.Combat, inCombat.Phase);
        Assert.NotNull(inCombat.Combat);
        return inCombat;
    }

    private static GameState BuildSingleCardCombatState(CardDefinition definition, int momentum)
    {
        var card = new CardInstance(definition.Id);
        var player = new CombatEntity(
            "p",
            40,
            40,
            0,
            new Dictionary<ResourceType, int> { [ResourceType.Momentum] = momentum }.ToImmutableDictionary(),
            new DeckState([card], [card], [], [], 0));
        var enemy = new CombatEntity("e", 30, 30, 0, ImmutableDictionary<ResourceType, int>.Empty, new DeckState([], [], [], [], 0));

        return GameState.Initial with
        {
            Phase = GamePhase.Combat,
            Combat = new CombatState(TurnOwner.Player, player, enemy, false, 0),
            CardDefinitions = new Dictionary<CardId, CardDefinition> { [definition.Id] = definition },
        };
    }
}
