using System.Collections.Immutable;
using Game.Core.Cards;

namespace Game.Core.Decks;

public sealed record RewardPoolEditState(ImmutableList<CardId> WorkingEnabledCardIds);
