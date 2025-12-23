using Bhaptics.SDK2;
using UnityEngine;

public class BhapticsVestOutput : MonoBehaviour, IHapticOutput
{
    public void PlayMotors(PositionType position, int[] motors, int durationMs)
    {
        BhapticsLibrary.PlayMotors((int)position, motors, durationMs);
        HapticsDebugBus.NotifyPlayMotors(position, motors, durationMs);
    }

    public void StopAll()
    {
        BhapticsLibrary.StopAll();
    }
}
