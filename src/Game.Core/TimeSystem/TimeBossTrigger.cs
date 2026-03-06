namespace Game.Core.TimeSystem;

public static class TimeBossTrigger
{
    public static TimeState MarkPlayerCaught(TimeState timeState)
    {
        return timeState with
        {
            PlayerCaughtByTime = true,
            TimeBossTriggerPending = true,
        };
    }
}
