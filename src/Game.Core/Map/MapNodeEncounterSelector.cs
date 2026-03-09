using Game.Core.Cards;
using Game.Core.Content;
using Game.Core.Game;

namespace Game.Core.Map;

public readonly record struct SelectedEncounter(
    CombatBlueprint Blueprint,
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions,
    IReadOnlyList<CardId> RewardCardPool);

public static class MapNodeEncounterSelector
{
    public static bool TrySelect(NodeType nodeType, out SelectedEncounter selectedEncounter)
    {
        if (!PlaytestContent.TryCreateEncounter(nodeType, out var blueprint))
        {
            selectedEncounter = default;
            return false;
        }

        selectedEncounter = new SelectedEncounter(
            Blueprint: blueprint,
            CardDefinitions: PlaytestContent.CardDefinitions,
            RewardCardPool: PlaytestContent.RewardCardPool);

        return true;
    }

    public static bool IsCombatNode(NodeType nodeType)
    {
        return nodeType is NodeType.Combat or NodeType.Elite or NodeType.Boss;
    }
}
