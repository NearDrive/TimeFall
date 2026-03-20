using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Tests.Game;

public class BladesMomentumTests
{
    [Fact]
    public void MomentumAddsToDamage()
    {
        BaseDamage5Momentum3Deals8();
    }

    [Fact]
    public void BaseDamage5Momentum3Deals8()
    {
        var cardId = new CardId("strike-test");
        var defs = new Dictionary<CardId, CardDefinition>
        {
            [cardId] = new(cardId, "Strike", 0, [new DamageCardEffect(5, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" })
        };

        var state = BuildState(cardId, defs, gm: 4, enemyHp: 20, enemyArmor: 0);
        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        var strike = Assert.IsType<PlayerStrikePlayed>(result.Events.First(e => e is PlayerStrikePlayed));

        Assert.Equal(8, strike.Damage);
        Assert.Equal(5, strike.BaseDamage);
        Assert.Equal(3, strike.MomentumBonus);
    }

    [Fact]
    public void MomentumAppliesPerHit()
    {
        TwinSlashMomentumAppliesToBothHits();
    }

    [Fact]
    public void TwinSlashMomentumAppliesToBothHits()
    {
        var cardId = new CardId("twin-slash-test");
        var defs = new Dictionary<CardId, CardDefinition>
        {
            [cardId] = new(cardId, "Twin Slash", 0, [new DamageNTimesCardEffect(5, 2, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" })
        };

        var state = BuildState(cardId, defs, gm: 4, enemyHp: 40, enemyArmor: 0);
        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        var hits = result.Events.OfType<PlayerStrikePlayed>().ToArray();

        Assert.Equal(2, hits.Length);
        Assert.All(hits, hit =>
        {
            Assert.Equal(8, hit.Damage);
            Assert.Equal(5, hit.BaseDamage);
            Assert.Equal(3, hit.MomentumBonus);
        });

        Assert.Equal(6, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
        Assert.Equal(2, result.Events.OfType<ResourceChanged>().Count(e => e.Reason.StartsWith("Attack hit bonus", StringComparison.Ordinal)));
    }

    [Fact]
    public void GeneratedMomentumFollowsExponentRule()
    {
        Assert.Equal(1, MomentumMath.Threshold(1));
        Assert.Equal(2, MomentumMath.Threshold(2));
        Assert.Equal(4, MomentumMath.Threshold(3));
        Assert.Equal(8, MomentumMath.Threshold(4));
    }

    [Fact] public void M1ProducesGM1() => Assert.Equal(1, MomentumMath.Threshold(1));
    [Fact] public void M2ProducesGM2() => Assert.Equal(2, MomentumMath.Threshold(2));
    [Fact] public void M3ProducesGM4() => Assert.Equal(4, MomentumMath.Threshold(3));
    [Fact] public void M4ProducesGM8() => Assert.Equal(8, MomentumMath.Threshold(4));

    [Fact]
    public void Spend1Momentum_FromM1_GoesToM0()
    {
        var card = AttackCard("spend-1", new SpendMomentumCost(1));
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 1), new PlayCardAction(0));

        Assert.Equal(1, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    [Fact]
    public void Spend2Momentum_FromM3_GoesToM1()
    {
        var card = AttackCard("spend-2-m3", new SpendMomentumCost(2));
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 5), new PlayCardAction(0));

        Assert.Equal(2, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
        var spend = Assert.IsType<ResourceChanged>(result.Events.First(e => e is ResourceChanged { Reason: "Spend 2 Momentum" }));
        Assert.Equal(5, spend.Before);
        Assert.Equal(1, spend.After);
    }

    [Fact]
    public void Spend2Momentum_FromM4_GoesToM2()
    {
        var card = AttackCard("spend-2-m4", new SpendMomentumCost(2));
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 9), new PlayCardAction(0));

        var spend = Assert.IsType<ResourceChanged>(result.Events.First(e => e is ResourceChanged { Reason: "Spend 2 Momentum" }));
        Assert.Equal(9, spend.Before);
        Assert.Equal(2, spend.After);
    }

    [Fact]
    public void SpendMomentum_RecomputesMinimumThresholdGm()
    {
        var card = AttackCard("spend-1-threshold", new SpendMomentumCost(1));
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 7), new PlayCardAction(0));

        var spend = Assert.IsType<ResourceChanged>(result.Events.First(e => e is ResourceChanged { Reason: "Spend 1 Momentum" }));
        Assert.Equal(7, spend.Before);
        Assert.Equal(2, spend.After);
    }

    [Fact]
    public void AttackCard_SpendsMomentumBeforeDamageScaling()
    {
        var card = new CardDefinition(new CardId("blade-dance-test"), "Blade Dance", 0, [new DamageNTimesCardEffect(4, 3, CardTarget.Opponent)], new SpendMomentumCost(2), new HashSet<string> { "Attack" });
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 5, enemyHp: 50), new PlayCardAction(0));

        var hits = result.Events.OfType<PlayerStrikePlayed>().ToArray();
        Assert.Equal(3, hits.Length);
        Assert.All(hits, hit =>
        {
            Assert.Equal(4, hit.BaseDamage);
            Assert.Equal(1, hit.MomentumBonus);
            Assert.Equal(5, hit.Damage);
        });
    }

    [Fact]
    public void AttackLabelBonus_IsAppliedPerHitAfterResolution()
    {
        var card = new CardDefinition(new CardId("attack-bonus-order"), "Twin Slash", 0, [new DamageNTimesCardEffect(5, 2, CardTarget.Opponent)], new SpendMomentumCost(2), new HashSet<string> { "Attack" });
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 5, enemyHp: 30), new PlayCardAction(0));

        Assert.Equal(3, result.Events.OfType<ResourceChanged>().Count(e => e.Reason == "Spend 2 Momentum" || e.Reason.StartsWith("Attack hit bonus", StringComparison.Ordinal)));
        Assert.Equal(3, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    [Fact]
    public void BladeDance_UsesRemainingMomentumAfterPayment()
    {
        var card = new CardDefinition(new CardId("blade-dance-behavior"), "Blade Dance", 0, [new DamageNTimesCardEffect(4, 3, CardTarget.Opponent)], new SpendMomentumCost(2), new HashSet<string> { "Attack" });
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 5, enemyHp: 50), new PlayCardAction(0));

        var spend = Assert.IsType<ResourceChanged>(result.Events.First(e => e is ResourceChanged { Reason: "Spend 2 Momentum" }));
        Assert.Equal(5, spend.Before);
        Assert.Equal(1, spend.After);

        Assert.Equal(4, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
        Assert.Equal(35, result.NewState.Combat.Enemy.HP);
    }

    [Fact]
    public void MomentumPoolUpdatesCorrectly()
    {
        var cardId = new CardId("gm-test");
        var defs = new Dictionary<CardId, CardDefinition>
        {
            [cardId] = new(cardId, "Gain", 0, [new GainGeneratedMomentumCardEffect(3, CardTarget.Self)], new NoCost(), new HashSet<string>())
        };

        var state = BuildState(cardId, defs, gm: 2, enemyHp: 20, enemyArmor: 0);
        var result = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.Equal(6, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    [Fact]
    public void MomentumDecayHalvesVisibleMomentumAndResetsToBaseThreshold()
    {
        var state = BuildState(new CardId("a"), new Dictionary<CardId, CardDefinition>(), gm: 10);
        var result = GameReducer.Reduce(state, new EndTurnAction());
        Assert.Equal(2, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }

    [Fact]
    public void MultipleCostsValidateBeforeApplying()
    {
        var card = new CardDefinition(new CardId("multi-cost-fail"), "Costly", 0, [new DamageCardEffect(5, CardTarget.Opponent)], [new SpendMomentumCost(1), new RequireMomentumCost(3)], new HashSet<string> { "Attack" });
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 2), new PlayCardAction(0));

        Assert.Equal(2, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
        Assert.Contains(result.Events, e => e is PlayCardRejected { Reason: PlayCardRejectionReason.CostNotPayable });
    }

    [Fact]
    public void MultipleCostsApplyInDeterministicOrder()
    {
        var card = new CardDefinition(new CardId("multi-cost-pass"), "Costly", 0, [new DamageCardEffect(5, CardTarget.Opponent)], [new RequireMomentumCost(3), new SpendMomentumCost(1), new SpendUpToMomentumCost(2)], new HashSet<string> { "Attack" });
        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 9), new PlayCardAction(0));

        var changes = result.Events.OfType<ResourceChanged>().ToArray();
        Assert.Collection(changes,
            first => Assert.Equal("Spend 1 Momentum", first.Reason),
            second => Assert.Equal("Spend up to 2 Momentum (2)", second.Reason),
            third => Assert.StartsWith("Attack hit bonus", third.Reason));
        Assert.Equal(2, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
    }



    [Fact]
    public void WeakReducesSingleHitDamage()
    {
        var card = new CardDefinition(new CardId("weak-single"), "Strike", 0, [new DamageCardEffect(5, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" });
        var state = BuildState(card.Id, Defs(card), gm: 0, enemyHp: 30);
        state = state with { Combat = state.Combat! with { Player = state.Combat!.Player with { Weak = 2 } } };

        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        var hit = Assert.Single(result.Events.OfType<PlayerStrikePlayed>());

        Assert.Equal(3, hit.Damage);
    }

    [Fact]
    public void WeakReducesDirectDamagePerHit()
    {
        var card = new CardDefinition(new CardId("weak-hit"), "Twin Slash", 0, [new DamageNTimesCardEffect(5, 2, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" });
        var state = BuildState(card.Id, Defs(card), gm: 0, enemyHp: 30);
        state = state with { Combat = state.Combat! with { Player = state.Combat!.Player with { Weak = 2 } } };

        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        var hits = result.Events.OfType<PlayerStrikePlayed>().ToArray();

        Assert.All(hits, hit => Assert.Equal(3, hit.Damage));
    }

    [Fact]
    public void NextAttackMultiplierOnlyAffectsFirstHitOfNextAttackCard()
    {
        var card = new CardDefinition(new CardId("buffed-twin"), "Buffed Twin", 0,
            [new NextAttackDoubleThisTurnCardEffect(CardTarget.Self), new DamageNTimesCardEffect(4, 2, CardTarget.Opponent)],
            PlayCosts: [new NoCost()], Labels: new HashSet<string> { "Attack" });

        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 0, enemyHp: 30), new PlayCardAction(0));
        var hits = result.Events.OfType<PlayerStrikePlayed>().ToArray();

        Assert.Equal([8, 4], hits.Select(h => h.Damage).ToArray());
    }

    [Fact]
    public void AttackMultipliersStackAdditively()
    {
        var card = new CardDefinition(new CardId("stacking-mults"), "Stacking", 0,
            [new TemporaryBuffAllAttacksDoubleDamageThisTurnCardEffect(CardTarget.Self), new NextAttackDoubleThisTurnCardEffect(CardTarget.Self), new DamageCardEffect(4, CardTarget.Opponent)],
            PlayCosts: [new NoCost()], Labels: new HashSet<string> { "Attack" });

        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 0, enemyHp: 30), new PlayCardAction(0));
        var hit = Assert.Single(result.Events.OfType<PlayerStrikePlayed>());

        Assert.Equal(12, hit.Damage);
    }

    [Fact]
    public void MomentumSystemDeterministic()
    {
        var cardId = new CardId("strike-test");
        var defs = new Dictionary<CardId, CardDefinition>
        {
            [cardId] = new(cardId, "Strike", 0, [new DamageCardEffect(5, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" })
        };

        var state = BuildState(cardId, defs, gm: 4, enemyHp: 20, enemyArmor: 0);
        var resultA = GameReducer.Reduce(state, new PlayCardAction(0));
        var resultB = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.Equal(resultA.NewState, resultB.NewState);
        Assert.Equal(resultA.Events, resultB.Events);
    }

    private static CardDefinition AttackCard(string id, CardCost cost)
        => new(new CardId(id), "Attack", 0, [new DamageCardEffect(5, CardTarget.Opponent)], cost, new HashSet<string> { "Attack" });

    private static IReadOnlyDictionary<CardId, CardDefinition> Defs(CardDefinition card)
        => new Dictionary<CardId, CardDefinition> { [card.Id] = card };

    private static GameState BuildState(CardId cardId, IReadOnlyDictionary<CardId, CardDefinition> defs, int gm, int enemyHp = 50, int enemyArmor = 0, int playerHp = 50, int playerMaxHp = 50, IReadOnlyList<CombatEntity>? extraEnemies = null)
        => BuildState([cardId], defs, gm, enemyHp, enemyArmor, playerHp, playerMaxHp, extraEnemies);

    private static GameState BuildState(IReadOnlyList<CardId> handCardIds, IReadOnlyDictionary<CardId, CardDefinition> defs, int gm, int enemyHp = 50, int enemyArmor = 0, int playerHp = 50, int playerMaxHp = 50, IReadOnlyList<CombatEntity>? extraEnemies = null)
    {
        var hand = handCardIds.Select(id => new CardInstance(id)).ToImmutableList();
        var player = new CombatEntity("p", playerHp, playerMaxHp, 0, new Dictionary<ResourceType, int> { [ResourceType.Momentum] = gm }.ToImmutableDictionary(), new DeckState(hand, hand, [], [], 0));
        var enemies = ImmutableList.Create(CreateEnemy("e", enemyHp, enemyArmor)).AddRange(extraEnemies ?? []);
        return GameState.Initial with { Phase = GamePhase.Combat, Combat = new CombatState(TurnOwner.Player, player, enemies, false, 0), CardDefinitions = defs };
    }

    private static CombatEntity CreateEnemy(string id, int hp, int armor)
        => new(id, hp, hp, armor, new Dictionary<ResourceType, int>().ToImmutableDictionary(), new DeckState([], [], [], [], 0));

    [Fact]
    public void CurrentMomentumEffectsUseSnapshotAtCardStart()
    {
        var card = new CardDefinition(new CardId("momentum-snapshot"), "Snapshot", 0,
            [new GainGeneratedMomentumCardEffect(2, CardTarget.Self), new DealDamagePerCurrentMomentumCardEffect(3, CardTarget.Opponent)],
            new NoCost(), new HashSet<string> { "Attack" });

        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 2, enemyHp: 40), new PlayCardAction(0));
        var hit = Assert.Single(result.Events.OfType<PlayerStrikePlayed>());

        Assert.Equal(6, hit.BaseDamage);
        Assert.Equal(1, hit.MomentumBonus);
        Assert.Equal(7, hit.Damage);
    }

    [Fact]
    public void AllAttacksBuffsApplyToAllHitsOfMultiHitAttack()
    {
        var card = new CardDefinition(new CardId("all-attacks-multi"), "Whirl", 0,
            [new AllAttacksBonusDamageThisTurnCardEffect(2, CardTarget.Self), new AllAttacksDoubleDamageThisTurnCardEffect(CardTarget.Self), new DamageNTimesCardEffect(3, 2, CardTarget.Opponent)],
            new NoCost(), new HashSet<string> { "Attack" });

        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 0, enemyHp: 50), new PlayCardAction(0));
        var hits = result.Events.OfType<PlayerStrikePlayed>().ToArray();

        Assert.Equal([10, 10], hits.Select(h => h.Damage).ToArray());
    }

    [Fact]
    public void NextAttackDoubleAppliesOnlyToFirstHitOfNextAttackCard()
    {
        var buffId = new CardId("prep-next");
        var attackId = new CardId("twin-next");
        var defs = new Dictionary<CardId, CardDefinition>
        {
            [buffId] = new(buffId, "Prep", 0, [new NextAttackDoubleDamageThisTurnCardEffect(CardTarget.Self)], new NoCost(), new HashSet<string>()),
            [attackId] = new(attackId, "Twin", 0, [new DamageNTimesCardEffect(4, 2, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" })
        };

        var state = BuildState([buffId, attackId], defs, gm: 0, enemyHp: 30);
        var afterBuff = GameReducer.Reduce(state, new PlayCardAction(0)).NewState;
        var afterAttack = GameReducer.Reduce(afterBuff, new PlayCardAction(0));
        var hits = afterAttack.Events.OfType<PlayerStrikePlayed>().ToArray();

        Assert.Equal([8, 4], hits.Select(h => h.Damage).ToArray());
    }

    [Fact]
    public void LifestealUsesRealDamageDealtAfterArmor()
    {
        var card = new CardDefinition(new CardId("lifesteal-real"), "Drain", 0,
            [new DamageCardEffect(10, CardTarget.Opponent), new LifestealPercentOfDamageDealtCardEffect(50, CardTarget.Self)],
            new NoCost(), new HashSet<string> { "Attack" });
        var state = BuildState(card.Id, Defs(card), gm: 0, enemyHp: 40, enemyArmor: 8, playerHp: 20, playerMaxHp: 40);

        var result = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.Equal(21, result.NewState.Combat!.Player.HP);
        Assert.Equal(38, result.NewState.Combat.Enemy.HP);
    }

    [Fact]
    public void DamageToAllEnemiesHitsEachEnemyDeterministically()
    {
        var card = new CardDefinition(new CardId("aoe"), "AoE", 0, [new DealDamageToAllEnemiesCardEffect(5)], new NoCost(), new HashSet<string> { "Attack" });
        var state = BuildState(card.Id, Defs(card), gm: 0, enemyHp: 20, extraEnemies: [CreateEnemy("e2", 18, 2), CreateEnemy("e3", 15, 0)]);

        var result = GameReducer.Reduce(state, new PlayCardAction(0));
        var hits = result.Events.OfType<PlayerStrikePlayed>().ToArray();

        Assert.Equal(3, hits.Length);
        Assert.Equal([15, 15, 10], result.NewState.Combat!.Enemies.Select(e => e.HP).ToArray());
    }

    [Fact]
    public void FlashCutStyleAttackScalesWithAttacksPlayedThisTurn()
    {
        var buffId = new CardId("open-attack");
        var flashCutId = new CardId("flash-cut");
        var defs = new Dictionary<CardId, CardDefinition>
        {
            [buffId] = new(buffId, "Open", 0, [new DamageCardEffect(2, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" }),
            [flashCutId] = new(flashCutId, "Flash Cut", 0, [new DamageWithAttackCountScalingCardEffect(4, 2, CardTarget.Opponent)], new NoCost(), new HashSet<string> { "Attack" })
        };

        var state = BuildState([buffId, flashCutId], defs, gm: 0, enemyHp: 40);
        var afterFirst = GameReducer.Reduce(state, new PlayCardAction(0)).NewState;
        var afterSecond = GameReducer.Reduce(afterFirst, new PlayCardAction(0));
        var hit = Assert.Single(afterSecond.Events.OfType<PlayerStrikePlayed>());

        Assert.Equal(6, hit.BaseDamage);
        Assert.Equal(6, hit.Damage);
    }

    [Fact]
    public void BleedingCutStyleEffectUsesCurrentMomentumSnapshot()
    {
        var card = new CardDefinition(new CardId("bleeding-cut"), "Bleeding Cut", 0,
            [new ApplyStatusPerCurrentMomentumCardEffect(StatusKind.Bleed, 1, 1, CardTarget.Opponent)],
            new NoCost(), new HashSet<string> { "Attack" });

        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 5, enemyHp: 30), new PlayCardAction(0));

        Assert.Equal(3, result.NewState.Combat!.Enemy.Bleed);
        var applied = Assert.Single(result.Events.OfType<StatusApplied>());
        Assert.Equal(3, applied.Amount);
    }

    [Fact]
    public void InfiniteDanceStyleEffectRepeatsSequencePerInitialMomentum()
    {
        var card = new CardDefinition(new CardId("infinite-dance"), "Infinite Dance", 0,
            [new RepeatEffectsPerCurrentMomentumCardEffect([
                new DamageCardEffect(2, CardTarget.Opponent),
                new ApplyStatusCardEffect(StatusKind.Bleed, 1, CardTarget.Opponent)
            ], CardTarget.Self)],
            new NoCost(), new HashSet<string> { "Attack" });

        var result = GameReducer.Reduce(BuildState(card.Id, Defs(card), gm: 5, enemyHp: 40), new PlayCardAction(0));

        Assert.Equal(31, result.NewState.Combat!.Enemy.HP);
        Assert.Equal(3, result.NewState.Combat.Enemy.Bleed);
        Assert.Equal(3, result.Events.OfType<StatusApplied>().Count());
    }

}
