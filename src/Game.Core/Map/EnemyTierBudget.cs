namespace Game.Core.Map;

public static class EnemyTierBudget
{
    public static int GetNormalTierValue(string tier)
    {
        return tier switch
        {
            "TierI" => 1,
            "TierII" => 2,
            _ => 0,
        };
    }
}
