using Game.Core.Cards;
using Game.Core.Game;

namespace Game.Data.Content;

public sealed record GameContentBundle(
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions,
    CombatBlueprint OpeningCombat);
