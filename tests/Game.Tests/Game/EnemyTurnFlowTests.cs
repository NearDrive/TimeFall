using Game.Core.Cards;
using CardsCardId = Game.Core.Cards.CardId;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Tests.Game;

public sealed class EnemyTurnFlowTests
{
    [Fact]
    public void EnemyTurn_DrawsOnceAtTurnStart()
    {
        var start = CreateCombatGameState(
            enemyDrawPile: ["enemy-attack", "enemy-attack", "enemy-attack"],
            enemyHand: [],
            enemyDiscard: []);

        var (_, events) = GameReducer.Reduce(start, new EndTurnAction());

        var enemyDraws = events.OfType<CardDrawn>()
            .Select(e => e.Card.DefinitionId.Value)
            .Count(id => id.StartsWith("enemy-", StringComparison.Ordinal));

        Assert.Equal(1, enemyDraws);
        Assert.Single(events.OfType<EnemyAttackPlayed>());
    }

    [Fact]
    public void EnemyTurn_DoesNotChainUnlimitedDrawPlayCycles()
    {
        var start = CreateCombatGameState(
            enemyDrawPile: ["enemy-attack"],
            enemyHand: [],
            enemyDiscard: ["enemy-attack", "enemy-attack", "enemy-attack"]);

        var (nextState, events) = GameReducer.Reduce(start, new EndTurnAction());

        Assert.Single(events.OfType<EnemyAttackPlayed>());
        Assert.Empty(events.OfType<DeckReshuffled>());
        Assert.Empty(events.OfType<CardBurned>());
        Assert.Equal(75, nextState.Combat!.Player.HP);
    }

    [Fact]
    public void EnemyTurn_CanStillPlayMultipleCardsFromCurrentHand()
    {
        var combat = CreateCombatState(
            enemyDrawPile: [],
            enemyHand: ["enemy-attack", "enemy-attack"],
            enemyDiscard: []);

        var result = EnemyController.ExecuteTurn(combat, GameRng.FromSeed(321), PlaytestContent.CardDefinitions);

        Assert.Equal(2, result.ActionCount);
        Assert.Equal(2, result.Events.OfType<EnemyAttackPlayed>().Count());
        Assert.Equal(70, result.CombatState.Player.HP);
        Assert.Empty(result.Events.OfType<CardDrawn>());
    }

    [Fact]
    public void EnemyBurnRule_RemainsActive()
    {
        var start = CreateCombatGameState(
            enemyDrawPile: [],
            enemyHand: [],
            enemyDiscard: ["enemy-attack", "enemy-attack", "enemy-attack"]);

        var (_, events) = GameReducer.Reduce(start, new EndTurnAction());

        Assert.Contains(events, e => e is DeckReshuffled);
        Assert.Contains(events, e => e is CardBurned);
        Assert.Single(events.OfType<EnemyAttackPlayed>());
    }

    private static GameState CreateCombatGameState(
        IReadOnlyList<string> enemyDrawPile,
        IReadOnlyList<string> enemyHand,
        IReadOnlyList<string> enemyDiscard)
    {
        return GameState.Initial with
        {
            Phase = GamePhase.Combat,
            Rng = GameRng.FromSeed(1234),
            CardDefinitions = PlaytestContent.CardDefinitions,
            Combat = CreateCombatState(enemyDrawPile, enemyHand, enemyDiscard),
        };
    }

    private static CombatState CreateCombatState(
        IReadOnlyList<string> enemyDrawPile,
        IReadOnlyList<string> enemyHand,
        IReadOnlyList<string> enemyDiscard)
    {
        return new CombatState(
            TurnOwner: TurnOwner.Player,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 80,
                MaxHP: 80,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(
                    DrawPile: ImmutableList<CardInstance>.Empty,
                    Hand: ImmutableList<CardInstance>.Empty,
                    DiscardPile: ImmutableList<CardInstance>.Empty,
                    BurnPile: ImmutableList<CardInstance>.Empty,
                    ReshuffleCount: 0)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 40,
                MaxHP: 40,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(
                    DrawPile: enemyDrawPile.Select(ToCard).ToImmutableList(),
                    Hand: enemyHand.Select(ToCard).ToImmutableList(),
                    DiscardPile: enemyDiscard.Select(ToCard).ToImmutableList(),
                    BurnPile: ImmutableList<CardInstance>.Empty,
                    ReshuffleCount: 0)),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);
    }

    private static CardInstance ToCard(string id) => new(new CardsCardId(id));
}
