using System;
using Bhaptics.SDK2;

public static class HapticsDebugBus
{
    public static event Action<PositionType, int[], int> OnPlayMotors;

    public static void NotifyPlayMotors(PositionType position, int[] motors, int durationMs)
    {
        OnPlayMotors?.Invoke(position, motors, durationMs);
    }
}
