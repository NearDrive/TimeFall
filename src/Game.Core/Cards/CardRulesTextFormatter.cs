namespace Game.Core.Cards;

public static class CardRulesTextFormatter
{
    public static string GetReadableRulesText(CardDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.RulesText))
        {
            return definition.RulesText.Trim();
        }

        if (definition.Effects.Count == 0)
        {
            return "No effect";
        }

        return string.Join(". ", definition.Effects.Select(FormatEffect));
    }

    private static string FormatEffect(CardEffect effect)
    {
        return effect switch
        {
            DamageCardEffect e => $"Deal {e.Amount} damage{FormatTargetSuffix(e.Target)}",
            DamageIgnoringArmorCardEffect e => $"Deal {e.Amount} damage ignoring armor{FormatTargetSuffix(e.Target)}",
            DamageNTimesCardEffect e => $"Deal {e.Amount} damage {FormatTimes(e.Times)}{FormatTargetSuffix(e.Target)}",
            DealDamagePerMomentumSpentCardEffect e => $"Deal {e.DamagePerMomentum} damage per Momentum spent{FormatTargetSuffix(e.Target)}",
            DealDamagePerAllMomentumSpentCardEffect e => $"Deal {e.DamagePerMomentum} damage per Momentum spent{FormatTargetSuffix(e.Target)}",
            DealDamagePerCurrentMomentumCardEffect e => $"Deal {e.DamagePerMomentum} damage per current Momentum{FormatTargetSuffix(e.Target)}",
            DealDamageToAllEnemiesCardEffect e => $"Deal {e.Amount} damage to all enemies",
            DealDamageAndDrawPerCurrentMomentumCardEffect e => $"Deal {e.DamagePerMomentum} damage and draw {e.DrawPerMomentum} card{Plural(e.DrawPerMomentum)} per current Momentum{FormatTargetSuffix(e.Target)}",
            DamageWithAttackCountScalingCardEffect e => $"Deal {e.BaseAmount} damage plus {e.DamagePerAttackPlayedThisTurn} per attack played this turn{FormatTargetSuffix(e.Target)}",
            GainArmorCardEffect e => $"Gain {e.Amount} armor{FormatTargetSuffix(e.Target)}",
            ConditionalGainArmorIfMomentumAtLeastCardEffect e => $"If Momentum >= {e.MinimumMomentum}, gain {e.Amount} armor{FormatTargetSuffix(e.Target)}",
            DrawCardsCardEffect e => $"Draw {e.Amount} card{Plural(e.Amount)}{FormatTargetSuffix(e.Target)}",
            HealCardEffect e => $"Heal {e.Amount} HP{FormatTargetSuffix(e.Target)}",
            ApplyStatusCardEffect e => $"Apply {e.Status} {e.Amount}{FormatTargetSuffix(e.Target)}",
            ApplyBleedCardEffect e => $"Apply Bleed {e.Amount}{FormatTargetSuffix(e.Target)}",
            ApplyStatusPerCurrentMomentumCardEffect e => $"Apply {e.Status} {e.BaseAmount} + {e.AmountPerCurrentMomentum} per current Momentum{FormatTargetSuffix(e.Target)}",
            GainGeneratedMomentumCardEffect e => $"Gain {e.Amount} gm{FormatTargetSuffix(e.Target)}",
            ReflectNextEnemyAttackDamageCardEffect e => $"Reflect {e.Amount} damage from the next enemy attack{FormatTargetSuffix(e.Target)}",
            AttackCountThisTurnToGmCardEffect e => $"Gain gm equal to attacks played this turn{FormatTargetSuffix(e.Target)}",
            RemoveEnemyArmorCardEffect e => $"Remove all armor{FormatTargetSuffix(e.Target)}",
            RemoveAllArmorCardEffect e => $"Remove all armor from all enemies{FormatTargetSuffix(e.Target)}",
            NextAttackBonusDamageThisTurnCardEffect e => $"Next attack gains +{e.Amount} damage{FormatTargetSuffix(e.Target)}",
            NextAttackDoubleThisTurnCardEffect e => $"Next attack deals double damage{FormatTargetSuffix(e.Target)}",
            NextAttackDoubleDamageThisTurnCardEffect e => $"Next attack deals double damage{FormatTargetSuffix(e.Target)}",
            TemporaryBuffAllAttacksPlusDamageThisTurnCardEffect e => $"All attacks this turn gain +{e.Amount} damage{FormatTargetSuffix(e.Target)}",
            AllAttacksBonusDamageThisTurnCardEffect e => $"All attacks this turn gain +{e.Amount} damage{FormatTargetSuffix(e.Target)}",
            TemporaryBuffAllAttacksDoubleDamageThisTurnCardEffect e => $"All attacks this turn deal double damage{FormatTargetSuffix(e.Target)}",
            AllAttacksDoubleDamageThisTurnCardEffect e => $"All attacks this turn deal double damage{FormatTargetSuffix(e.Target)}",
            LifestealPercentOfDamageDealtCardEffect e => $"Heal for {e.Percent}% of damage dealt{FormatTargetSuffix(e.Target)}",
            RepeatEffectsPerCurrentMomentumCardEffect e => $"Repeat effects per current Momentum{FormatTargetSuffix(e.Target)}",
            _ => "Apply a unique effect",
        };
    }

    private static string FormatTimes(int times)
    {
        return times switch
        {
            1 => "once",
            2 => "twice",
            3 => "three times",
            _ => $"{times} times",
        };
    }

    private static string Plural(int amount) => amount == 1 ? string.Empty : "s";

    private static string FormatTargetSuffix(CardTarget target)
    {
        return target switch
        {
            CardTarget.Self => " (self)",
            _ => string.Empty,
        };
    }
}
