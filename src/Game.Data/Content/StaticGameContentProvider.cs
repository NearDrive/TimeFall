using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Data.Content;

public static class StaticGameContentProvider
{
    private static readonly CardId StrikeCardId = new("strike");
    private static readonly CardId DefendCardId = new("defend");
    private static readonly CardId FocusCardId = new("focus");
    private static readonly CardId AttackCardId = new("attack");

    public static GameContentBundle LoadDefault()
    {
        var cardDefinitions = new Dictionary<CardId, CardDefinition>
        {
            [StrikeCardId] = new(StrikeCardId, "Strike", 1, [new DamageCardEffect(4, CardTarget.Opponent)]),
            [DefendCardId] = new(DefendCardId, "Defend", 1, [new GainArmorCardEffect(3, CardTarget.Self)]),
            [FocusCardId] = new(FocusCardId, "Focus", 1, [new GainArmorCardEffect(1, CardTarget.Self)]),
            [AttackCardId] = new(AttackCardId, "Attack", 1, [new DamageCardEffect(4, CardTarget.Opponent)]),
        };

        var openingCombat = new CombatBlueprint(
            Player: new CombatantBlueprint(
                EntityId: "player",
                HP: 80,
                MaxHP: 80,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int> { [ResourceType.Energy] = 3 },
                DrawPile:
                [
                    StrikeCardId,
                    DefendCardId,
                    StrikeCardId,
                    DefendCardId,
                    FocusCardId,
                    StrikeCardId,
                    DefendCardId,
                    FocusCardId,
                    StrikeCardId,
                    DefendCardId,
                ]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy-1",
                HP: 30,
                MaxHP: 30,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                DrawPile:
                [
                    AttackCardId,
                    AttackCardId,
                    AttackCardId,
                    DefendCardId,
                ]));

        return new GameContentBundle(cardDefinitions, openingCombat);
    }
}
