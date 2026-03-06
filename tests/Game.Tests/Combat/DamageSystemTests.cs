using Game.Core.Combat;
using System.Collections.Immutable;

namespace Game.Tests.Combat;

public class DamageSystemTests
{
    [Fact]
    public void ApplyHit_Armor10Incoming20_Takes10AndHalvesArmor()
    {
        var target = CreateTarget(hp: 50, armor: 10);

        var (updated, events) = DamageSystem.ApplyHit(target, incomingDamage: 20);

        Assert.Equal(40, updated.HP);
        Assert.Equal(5, updated.Armor);

        Assert.Collection(
            events,
            evt => Assert.Equal(new DamageDealt(20, 10), evt),
            evt => Assert.Equal(new ArmorChanged(10, 5), evt));
    }

    [Fact]
    public void ApplyHit_Armor100Incoming20_Takes0AndStillHalvesArmor()
    {
        var target = CreateTarget(hp: 50, armor: 100);

        var (updated, events) = DamageSystem.ApplyHit(target, incomingDamage: 20);

        Assert.Equal(50, updated.HP);
        Assert.Equal(50, updated.Armor);

        Assert.Collection(
            events,
            evt => Assert.Equal(new DamageDealt(20, 0), evt),
            evt => Assert.Equal(new ArmorChanged(100, 50), evt));
    }

    [Fact]
    public void ApplyHit_MultiHitFrom100ArmorWithTenHitsOf10_FollowsHalvingSequence()
    {
        var target = CreateTarget(hp: 100, armor: 100);
        var expectedArmorSequence = new[] { 50, 25, 12, 6, 3, 1, 0, 0, 0, 0 };

        for (var i = 0; i < 10; i++)
        {
            var previousArmor = target.Armor;
            var (updated, events) = DamageSystem.ApplyHit(target, incomingDamage: 10);

            var expectedArmor = expectedArmorSequence[i];
            var expectedDamageTaken = Math.Max(0, 10 - previousArmor);

            Assert.Equal(expectedArmor, updated.Armor);
            Assert.Equal(target.HP - expectedDamageTaken, updated.HP);
            Assert.Equal(new DamageDealt(10, expectedDamageTaken), events[0]);
            Assert.Equal(new ArmorChanged(previousArmor, expectedArmor), events[1]);

            target = updated;
        }
    }

    [Fact]
    public void ApplyHit_EmitsEntityDied_WhenHpDropsToZeroOrBelow()
    {
        var target = CreateTarget(hp: 5, armor: 0);

        var (updated, events) = DamageSystem.ApplyHit(target, incomingDamage: 5);

        Assert.Equal(0, updated.HP);
        Assert.Contains(events, evt => evt is EntityDied("player-1"));
    }

    private static CombatEntity CreateTarget(int hp, int armor)
    {
        return new CombatEntity(
            EntityId: "player-1",
            HP: hp,
            MaxHP: 100,
            Armor: armor,
            Resources: ImmutableDictionary<ResourceType, int>.Empty,
            Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty));
    }
}
