using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Core.Map;

public readonly record struct SelectedEncounter(CombatBlueprint Blueprint, IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions);

public static class MapNodeEncounterSelector
{
    private static readonly CardId StrikeCardId = new("strike");
    private static readonly CardId DefendCardId = new("defend");
    private static readonly CardId FocusCardId = new("focus");
    private static readonly CardId AttackCardId = new("attack");

    private static readonly IReadOnlyDictionary<CardId, CardDefinition> DefaultCardDefinitions = new Dictionary<CardId, CardDefinition>
    {
        [StrikeCardId] = new(StrikeCardId, "Strike", 1, [new DamageCardEffect(4, CardTarget.Opponent)]),
        [DefendCardId] = new(DefendCardId, "Defend", 1, [new GainArmorCardEffect(3, CardTarget.Self)]),
        [FocusCardId] = new(FocusCardId, "Focus", 1, [new GainArmorCardEffect(1, CardTarget.Self)]),
        [AttackCardId] = new(AttackCardId, "Attack", 1, [new DamageCardEffect(4, CardTarget.Opponent)]),
    };

    public static bool TrySelect(NodeType nodeType, out SelectedEncounter selectedEncounter)
    {
        if (!IsCombatNode(nodeType))
        {
            selectedEncounter = default;
            return false;
        }

        var enemyHp = nodeType switch
        {
            NodeType.Combat => 30,
            NodeType.Elite => 45,
            NodeType.Boss => 70,
            _ => throw new InvalidOperationException($"Unsupported combat-capable node type: {nodeType}"),
        };

        var enemyId = nodeType switch
        {
            NodeType.Combat => "enemy-standard",
            NodeType.Elite => "enemy-elite",
            NodeType.Boss => "enemy-boss",
            _ => throw new InvalidOperationException($"Unsupported combat-capable node type: {nodeType}"),
        };

        selectedEncounter = new SelectedEncounter(
            Blueprint: new CombatBlueprint(
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
                    EntityId: enemyId,
                    HP: enemyHp,
                    MaxHP: enemyHp,
                    Armor: 0,
                    Resources: ImmutableDictionary<ResourceType, int>.Empty,
                    DrawPile:
                    [
                        AttackCardId,
                        AttackCardId,
                        AttackCardId,
                        DefendCardId,
                    ])),
            CardDefinitions: DefaultCardDefinitions);

        return true;
    }

    public static bool IsCombatNode(NodeType nodeType)
    {
        return nodeType is NodeType.Combat or NodeType.Elite or NodeType.Boss;
    }
}
