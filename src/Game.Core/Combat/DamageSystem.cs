namespace Game.Core.Combat;

public static class DamageSystem
{
    public static (CombatEntity UpdatedEntity, IReadOnlyList<DamageEvent> Events) ApplyHit(CombatEntity target, int incomingDamage)
    {
        var normalizedIncoming = Math.Max(0, incomingDamage);
        var damageTaken = Math.Max(0, normalizedIncoming - target.Armor);
        var oldArmor = target.Armor;
        var newArmor = oldArmor / 2;
        var newHp = target.HP - damageTaken;

        var updated = target with
        {
            HP = newHp,
            Armor = newArmor
        };

        var events = new List<DamageEvent>
        {
            new DamageDealt(normalizedIncoming, damageTaken),
            new ArmorChanged(oldArmor, newArmor)
        };

        if (newHp <= 0)
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
