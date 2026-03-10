using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Tests.Game;

public sealed class MultiEnemyCombatTests
{
    [Fact]
    public void CombatState_CanContainMultipleEnemies()
    {
        var state = CreateCombatWithEnemies(3);

        Assert.Equal(3, state.Combat!.Enemies.Count);
    }

    [Fact]
    public void PlayCardAction_TargetIndex_ResolvesToCorrectEnemy()
    {
        var state = CreateCombatWithEnemies(enemyIds: ["zone1-raider", "zone1-bastion-guard", "zone1-raider"]);
        var strikeIndex = state.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value == "strike");

        var targetBefore = state.Combat.Enemies[2].HP;
        var otherBefore = state.Combat.Enemies[0].HP;
        var result = GameReducer.Reduce(state, new PlayCardAction(strikeIndex, 2));

        Assert.Equal(targetBefore - 4, result.NewState.Combat!.Enemies[2].HP);
        Assert.Equal(otherBefore, result.NewState.Combat.Enemies[0].HP);
    }

    [Fact]
    public void SingleTargetAttack_RejectsMissingTarget_WhenMultipleEnemiesExist()
    {
        var state = CreateCombatWithEnemies(2);
        var strikeIndex = state.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value == "strike");

        var result = GameReducer.Reduce(state, new PlayCardAction(strikeIndex));

        var rejection = Assert.IsType<PlayCardRejected>(Assert.Single(result.Events));
        Assert.Equal(PlayCardRejectionReason.MissingTarget, rejection.Reason);
    }

    [Fact]
    public void InvalidTargetIndex_IsRejectedClearly()
    {
        var state = CreateCombatWithEnemies(2);
        var strikeIndex = state.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value == "strike");

        var result = GameReducer.Reduce(state, new PlayCardAction(strikeIndex, 9));

        var rejection = Assert.IsType<PlayCardRejected>(Assert.Single(result.Events));
        Assert.Equal(PlayCardRejectionReason.InvalidTarget, rejection.Reason);
        Assert.Equal("Target index 9 is out of range.", rejection.Message);
    }

    [Fact]
    public void SingleTargetMultiHit_UsesChosenTargetForAllHits()
    {
        var twinSlashId = new CardId("test-twin-slash");
        var cardDefinitions = new Dictionary<CardId, CardDefinition>(PlaytestContent.CardDefinitions)
        {
            [twinSlashId] = new(twinSlashId, "Twin Slash", 1, [new DamageNTimesCardEffect(5, 2, CardTarget.Opponent)]),
        }.ToImmutableDictionary();

        var state = CreateCombatWithEnemies(3, playerDrawPile: [twinSlashId], cardDefinitions: cardDefinitions);

        var result = GameReducer.Reduce(state, new PlayCardAction(0, 2));

        Assert.Equal(0, result.NewState.Combat!.Enemies[2].HP);
        Assert.Equal(10, result.NewState.Combat.Enemies[0].HP);
        Assert.Equal(10, result.NewState.Combat.Enemies[1].HP);

        var strikeEvents = result.Events.OfType<PlayerStrikePlayed>().ToArray();
        Assert.Equal(2, strikeEvents.Length);
        Assert.All(strikeEvents, e =>
        {
            Assert.Equal(10, e.EnemyHpBeforeHit);
            Assert.Equal(5, e.Damage);
        });
        Assert.Equal(5, strikeEvents[0].EnemyHpAfterHit);
        Assert.Equal(0, strikeEvents[1].EnemyHpAfterHit);
    }

    [Fact]
    public void SingleTargetMultiHit_DoesNotSilentlyRetarget()
    {
        var twinSlashId = new CardId("test-twin-slash");
        var cardDefinitions = new Dictionary<CardId, CardDefinition>(PlaytestContent.CardDefinitions)
        {
            [twinSlashId] = new(twinSlashId, "Twin Slash", 1, [new DamageNTimesCardEffect(6, 2, CardTarget.Opponent)]),
        }.ToImmutableDictionary();

        var state = CreateCombatWithEnemies(3, playerDrawPile: [twinSlashId], cardDefinitions: cardDefinitions, enemyHp: 6);

        var result = GameReducer.Reduce(state, new PlayCardAction(0, 2));

        var strikeEvents = result.Events.OfType<PlayerStrikePlayed>().ToArray();
        Assert.Equal(2, strikeEvents.Length);
        Assert.Equal(6, strikeEvents[0].EnemyHpBeforeHit);
        Assert.Equal(0, strikeEvents[0].EnemyHpAfterHit);
        Assert.Equal(0, strikeEvents[1].EnemyHpBeforeHit);
        Assert.Equal(0, strikeEvents[1].EnemyHpAfterHit);

        Assert.Equal(6, result.NewState.Combat!.Enemies[0].HP);
        Assert.Equal(6, result.NewState.Combat.Enemies[1].HP);
    }

    [Fact]
    public void MultiEnemyCombat_VictoryOccursWhenAllEnemiesDead()
    {
        var state = CreateCombatWithEnemies(2);
        var combat = state.Combat! with
        {
            Enemies = state.Combat!.Enemies.Select(e => e with { HP = 0 }).ToImmutableList(),
        };

        var result = GameReducer.Reduce(state with { Combat = combat }, new EndTurnAction());

        Assert.Equal(GamePhase.RewardSelection, result.NewState.Phase);
        Assert.Contains(result.Events, e => e is CombatVictory);
    }

    [Fact]
    public void EnemyTurn_SkipsDeadEnemies()
    {
        var state = CreateCombatWithEnemies(2);
        var deadFirst = state.Combat! with
        {
            Enemies = state.Combat!.Enemies.SetItem(0, state.Combat.Enemies[0] with { HP = 0 }),
        };

        var result = GameReducer.Reduce(state with { Combat = deadFirst }, new EndTurnAction());

        Assert.Single(result.Events.OfType<EnemyAttackPlayed>());
    }

    private static GameState CreateCombatWithEnemies(
        int enemyCount = 0,
        IReadOnlyList<string>? enemyIds = null,
        IReadOnlyList<CardId>? playerDrawPile = null,
        IReadOnlyDictionary<CardId, CardDefinition>? cardDefinitions = null,
        int enemyHp = 10)
    {
        var ids = enemyIds ?? Enumerable.Range(0, enemyCount).Select(i => $"enemy-{i}").ToArray();
        var enemy = new CombatantBlueprint(
            EntityId: "enemy-template",
            HP: enemyHp,
            MaxHP: enemyHp,
            Armor: 0,
            Resources: ImmutableDictionary<ResourceType, int>.Empty,
            DrawPile: [PlaytestContent.EnemyAttackCardId]);

        var blueprint = new CombatBlueprint(
            Player: new CombatantBlueprint(
                EntityId: "player",
                HP: 30,
                MaxHP: 30,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: playerDrawPile ?? [PlaytestContent.StrikeCardId, PlaytestContent.GuardCardId, PlaytestContent.QuickDrawCardId, PlaytestContent.StrikeCardId, PlaytestContent.GuardCardId]),
            Enemies: ids.Select(id => enemy with { EntityId = id }).ToArray());

        var definitions = cardDefinitions ?? PlaytestContent.CardDefinitions;
        var state = GameState.Initial with { CardDefinitions = definitions };
        return GameReducer.Reduce(state, new BeginCombatAction(blueprint, definitions)).NewState;
    }
}
