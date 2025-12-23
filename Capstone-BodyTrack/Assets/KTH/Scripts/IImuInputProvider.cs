using UnityEngine;

public interface IImuInputProvider
{
    Vector3 GetLatestAccel();
    Vector3 GetLatestGyro();
}
