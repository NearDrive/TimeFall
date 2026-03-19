namespace Game.Core.Combat;

public static class MomentumMath
{
    public static int DerivedMomentumFromGm(int gm)
    {
        if (gm <= 0)
        {
            return 0;
        }

        return (int)Math.Floor(Math.Log2(gm)) + 1;
    }

    public static int Threshold(int momentum)
    {
        if (momentum <= 0)
        {
            return 0;
        }

        return 1 << (momentum - 1);
    }

    public static int BaseGmForMomentum(int momentum)
        => Threshold(momentum);

    public static int SpendVisibleMomentum(int gm, int momentumToSpend)
    {
        var visibleMomentum = DerivedMomentumFromGm(gm);
        var targetMomentum = Math.Max(0, visibleMomentum - Math.Max(0, momentumToSpend));
        return BaseGmForMomentum(targetMomentum);
    }

    public static int DecayVisibleMomentum(int gm)
    {
        var visibleMomentum = DerivedMomentumFromGm(gm);
        var decayedMomentum = visibleMomentum / 2;
        return BaseGmForMomentum(decayedMomentum);
    }
}
