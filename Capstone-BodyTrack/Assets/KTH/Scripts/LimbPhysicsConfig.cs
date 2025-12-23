using UnityEngine;

[CreateAssetMenu(menuName = "KTH/Limb Physics Config", fileName = "LimbPhysicsConfig")]
public class LimbPhysicsConfig : ScriptableObject
{
    [Header("IMU Feedback Settings")]
    public float forceMagnitude = 100f;
    public float torqueMagnitude = 50f;
    public float damping = 0.5f;
    public Vector3 imuOffset = Vector3.zero;
    public float gravity = 9.81f;

    [Header("Active Muscle Settings")]
    public float muscleForce = 20f;
    public AnimationCurve stiffnessCurve = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0.1f));
    public float dragMultiplier = 0.1f;

    [Header("Organic Movement")]
    public float idleSwayAmount = 5f;
    public float idleSwaySpeed = 1f;
    public float inertiaDelay = 0.1f;

    [Header("Joint Settings")]
    public float jointLimit = 45f;
    public float colliderRadius = 0.08f;
    public float colliderHeight = 0.5f;
    public float boneMass = 0.5f;
}
