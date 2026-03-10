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
}
