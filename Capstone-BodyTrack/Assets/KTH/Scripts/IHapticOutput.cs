using Bhaptics.SDK2;

public interface IHapticOutput
{
    void PlayMotors(PositionType position, int[] motors, int durationMs);
    void StopAll();
}
