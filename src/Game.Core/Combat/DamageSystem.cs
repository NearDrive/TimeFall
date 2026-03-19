namespace Game.Core.Combat;

public static class DamageSystem
{
    public static (CombatEntity UpdatedEntity, IReadOnlyList<DamageEvent> Events) ApplyHit(CombatEntity target, int incomingDamage)
    {
        var normalizedIncoming = Math.Max(0, incomingDamage);
        var damageTakenBeforeVulnerable = Math.Max(0, normalizedIncoming - target.Armor);
        var vulnerableBonus = damageTakenBeforeVulnerable > 0 ? target.Vulnerable : 0;
        var damageTaken = damageTakenBeforeVulnerable + vulnerableBonus;
        var oldArmor = target.Armor;
        var newArmor = oldArmor / 2;
        var newHp = Math.Max(0, target.HP - damageTaken);

        var updated = target with
        {
            HP = newHp,
            Armor = newArmor,
            Vulnerable = vulnerableBonus > 0 ? 0 : target.Vulnerable,
        };

        var events = new List<DamageEvent>
        {
            new DamageDealt(normalizedIncoming, damageTaken),
            new ArmorChanged(oldArmor, newArmor)
        };

        if (target.HP > 0 && newHp == 0)
        {
            events.Add(new EntityDied(target.EntityId));
        }

        return (updated, events);
    }

    public static (CombatEntity UpdatedEntity, IReadOnlyList<DamageEvent> Events) ApplyArmorIgnoringHit(CombatEntity target, int incomingDamage)
    {
        var normalizedIncoming = Math.Max(0, incomingDamage);
        var vulnerableBonus = normalizedIncoming > 0 ? target.Vulnerable : 0;
        var totalDamage = normalizedIncoming + vulnerableBonus;
        var newHp = Math.Max(0, target.HP - totalDamage);
        var updated = target with
        {
            HP = newHp,
            Vulnerable = vulnerableBonus > 0 ? 0 : target.Vulnerable,
        };
        var events = new List<DamageEvent> { new DamageDealt(normalizedIncoming, totalDamage) };
        if (target.HP > 0 && newHp == 0)
        {
            events.Add(new EntityDied(target.EntityId));
        }

        return (updated, events);
    }
}

public abstract record DamageEvent;

public sealed record DamageDealt(int Incoming, int Taken) : DamageEvent;

public sealed record ArmorChanged(int OldArmor, int NewArmor) : DamageEvent;

public sealed record EntityDied(string EntityId) : DamageEvent;
