using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Map;

namespace Game.Data.Content;

public sealed record GameContentBundle(
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions,
    IReadOnlyDictionary<string, RunDeckDefinition> DeckDefinitions,
    IReadOnlyList<CardId> RewardCardPool,
    CombatBlueprint OpeningCombat,
    IReadOnlyDictionary<string, EnemyDefinition> EnemyDefinitions,
    ZoneSpawnTable? Zone1SpawnTable);
