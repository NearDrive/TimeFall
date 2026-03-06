namespace Game.Core.Decks;

public sealed record DeckEditState(int RemainingRemovals)
{
    public static DeckEditState RemoveOneCard() => new(1);
}
