namespace Game.Core.Common;

/// <summary>
/// Deterministic random number generator wrapper driven by an explicit seed/state.
/// </summary>
public readonly record struct GameRng(int Seed, uint State)
{
    public static GameRng FromSeed(int seed)
    {
        var initialState = (uint)seed;
        if (initialState == 0)
        {
            initialState = 0x6D2B79F5u;
        }

        return new GameRng(seed, initialState);
    }

    public (int Value, GameRng NextRng) NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
        }

        var nextState = unchecked(State * 1664525u + 1013904223u);
        var range = (uint)(maxExclusive - minInclusive);
        var value = (int)(nextState % range) + minInclusive;

        return (value, this with { State = nextState });
    }
}
