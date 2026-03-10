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
    public void SingleTargetAttack_CanHitSpecifiedEnemy()
    {
        var state = CreateCombatWithEnemies(2);
        var strikeIndex = state.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value == "strike");

        var targetBefore = state.Combat.Enemies[1].HP;
        var otherBefore = state.Combat.Enemies[0].HP;
        var result = GameReducer.Reduce(state, new PlayCardAction(strikeIndex, 1));

        Assert.Equal(targetBefore - 4, result.NewState.Combat!.Enemies[1].HP);
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
    public void SingleTargetAttack_RejectsInvalidTarget()
    {
        var state = CreateCombatWithEnemies(2);
        var strikeIndex = state.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value == "strike");

        var result = GameReducer.Reduce(state, new PlayCardAction(strikeIndex, 9));

        var rejection = Assert.IsType<PlayCardRejected>(Assert.Single(result.Events));
        Assert.Equal(PlayCardRejectionReason.InvalidTarget, rejection.Reason);
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

    private static GameState CreateCombatWithEnemies(int enemyCount)
    {
        var enemy = new CombatantBlueprint(
            EntityId: "enemy-template",
            HP: 10,
            MaxHP: 10,
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
                DrawPile: [PlaytestContent.StrikeCardId, PlaytestContent.GuardCardId, PlaytestContent.QuickDrawCardId, PlaytestContent.StrikeCardId, PlaytestContent.GuardCardId]),
            Enemies: Enumerable.Range(0, enemyCount).Select(i => enemy with { EntityId = $"enemy-{i}" }).ToArray());

        var state = GameState.Initial with { CardDefinitions = PlaytestContent.CardDefinitions };
        return GameReducer.Reduce(state, new BeginCombatAction(blueprint, PlaytestContent.CardDefinitions)).NewState;
    }
}
