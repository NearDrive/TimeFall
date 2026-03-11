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
    public void MomentumDecayStillWorks()
    {
        var state = BuildState(new CardId("a"), new Dictionary<CardId, CardDefinition>(), gm: 10);
        var result = GameReducer.Reduce(state, new EndTurnAction());
        Assert.Equal(5, result.NewState.Combat!.Player.Resources[ResourceType.Momentum]);
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

    private static GameState BuildState(CardId cardId, IReadOnlyDictionary<CardId, CardDefinition> defs, int gm, int enemyHp = 50, int enemyArmor = 0)
    {
        var player = new CombatEntity("p", 50, 50, 0, new Dictionary<ResourceType, int> { [ResourceType.Momentum] = gm }.ToImmutableDictionary(), new DeckState([new CardInstance(cardId)], [new CardInstance(cardId)], [], [], 0));
        var enemy = new CombatEntity("e", enemyHp, enemyHp, enemyArmor, new Dictionary<ResourceType, int>().ToImmutableDictionary(), new DeckState([], [], [], [], 0));
        return GameState.Initial with { Phase = GamePhase.Combat, Combat = new CombatState(TurnOwner.Player, player, enemy, false, 0), CardDefinitions = defs };
    }
}
