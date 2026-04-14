using System.Collections.Immutable;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public sealed record SandboxState(
    int SessionSeed,
    int CombatCount,
    string? SelectedDeckId,
    ImmutableList<CardId> EquippedCardIds,
    string? SelectedEnemyId,
    int? LastCombatSeed,
    bool? LastCombatWon)
{
    public static SandboxState Create(int seed)
    {
        return new SandboxState(
            SessionSeed: seed,
            CombatCount: 0,
            SelectedDeckId: null,
            EquippedCardIds: ImmutableList<CardId>.Empty,
            SelectedEnemyId: null,
            LastCombatSeed: null,
            LastCombatWon: null);
    }
}
