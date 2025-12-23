using UnityEngine;

public interface ILimbPhysicsData
{
    Vector3 CurrentAngularVelocity { get; }
    Transform RootTransform { get; }
}
