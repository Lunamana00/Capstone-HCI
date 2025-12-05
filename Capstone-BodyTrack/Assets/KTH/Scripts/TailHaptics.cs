using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

public class TailHaptics : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TailControllerPhysics tailPhysics;
    [SerializeField] private Transform tailRoot;

    [Header("Sway Feedback (Inertia)")]
    public bool enableSwayFeedback = true;
    public float swayVelocityThreshold = 10f; // Minimum angular velocity
    public float swayIntensityMultiplier = 0.5f;

    [Header("Collision Feedback")]
    public bool enableCollisionFeedback = true;
    public float collisionIntensityMultiplier = 0.5f;
    public float maxImpactDistance = 1.0f; // Distance from root to be considered "base"

    [Header("Tension Feedback (Grabbing)")]
    public bool enableTensionFeedback = true;
    [Range(0f, 1f)] public float currentTension = 0f; // Updated by external script

    private float lastSwayTime;

    void Start()
    {
        if (tailPhysics == null) tailPhysics = GetComponent<TailControllerPhysics>();
        if (tailRoot == null && tailPhysics != null) tailRoot = tailPhysics.tailRoot;

        // Find all TailCollision components in tail bones and subscribe to their events
        TailCollision[] tailCollisions = GetComponentsInChildren<TailCollision>();
        if (tailCollisions.Length > 0)
        {
            foreach (TailCollision collision in tailCollisions)
            {
                collision.OnTailCollision += TriggerCollisionFeedback;
            }
            Debug.Log($"TailHaptics: Subscribed to {tailCollisions.Length} TailCollision components");
        }
        else
        {
            Debug.LogWarning("TailHaptics: No TailCollision components found in children. Collision feedback will not work.");
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from all TailCollision components
        TailCollision[] tailCollisions = GetComponentsInChildren<TailCollision>();
        foreach (TailCollision collision in tailCollisions)
        {
            if (collision != null)
            {
                collision.OnTailCollision -= TriggerCollisionFeedback;
            }
        }
    }

    void Update()
    {
        if (enableSwayFeedback && Time.time - lastSwayTime > 0.1f) // Throttle sway updates
        {
            UpdateSwayFeedback();
            lastSwayTime = Time.time;
        }

        if (enableTensionFeedback && currentTension > 0.01f)
        {
            UpdateTensionFeedback();
        }
    }

    // 2. Inertia & Sway (Motor Mode, Centrifugal)
    private void UpdateSwayFeedback()
    {
        if (tailPhysics == null) return;

        float angularVelY = tailPhysics.CurrentAngularVelocity.y;

        if (Mathf.Abs(angularVelY) > swayVelocityThreshold)
        {
            float intensityVal = Mathf.Clamp01(Mathf.Abs(angularVelY) * swayIntensityMultiplier / 100f);
            int intensity = (int)(intensityVal * 100); 

            // Centrifugal Force Logic:
            // Turning Left (Vel < 0) -> Force pushes to Right.
            // Turning Right (Vel > 0) -> Force pushes to Left.
            
            // Use Mid-Back Side Columns (Rows 2, 3, 4) to avoid overlapping with Idle (Row 5)
            int[] motors = new int[40];

            if (angularVelY > 0) // Turning Right -> Force to Left
            {
                // Activate Left Side Column (Upper/Mid Back)
                // Row 2 Left: 24, Row 3 Left: 28, Row 4 Left: 32
                motors[24] = intensity;
                motors[28] = intensity;
                motors[32] = intensity;
            }
            else // Turning Left -> Force to Right
            {
                // Activate Right Side Column (Upper/Mid Back)
                // Row 2 Right: 27, Row 3 Right: 31, Row 4 Right: 35
                motors[27] = intensity;
                motors[31] = intensity;
                motors[35] = intensity;
            }

            BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);
        }
    }

    // 3. Collision (Dot Mode, Impact + Reverberation)
    public void TriggerCollisionFeedback(float impactForce, Vector3 contactPoint)
    {
        if (!enableCollisionFeedback) return;

        float intensityVal = Mathf.Clamp01(impactForce * collisionIntensityMultiplier);
        int intensity = (int)(intensityVal * 100);

        // Calculate distance from root
        if (tailRoot == null)
        {
            if (tailPhysics != null) tailRoot = tailPhysics.tailRoot;
            if (tailRoot == null) tailRoot = transform; // Fallback to self if still null
        }
        
        float distance = Vector3.Distance(tailRoot.position, contactPoint);
        bool isNearRoot = distance < (maxImpactDistance * 0.3f); // Top 30% is "Near"

        if (isNearRoot)
        {
            // Strong Impact at Base (Lumbar Center)
            // X40 Back Mapping: 20-39.
            // Bottom Center: 37, 38 (Row 5 middle) or 33, 34 (Row 4 middle).
            // Let's hit a cluster at the bottom center.
            int[] motors = new int[40];
            motors[33] = intensity; motors[34] = intensity; // Row 4
            motors[37] = intensity; motors[38] = intensity; // Row 5 (Bottom)
            
            BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100); // Short & Sharp
            
            // Reverberation (Echo up the spine)
            StartCoroutine(PlayReverberation(intensity));
        }
        else
        {
            // Weak, Diffuse Vibration (Impact at Tip)
            // Spread across bottom area but weaker
            int weakIntensity = intensity / 2;
            int[] motors = new int[40];
            // Activate wider range on bottom rows
            motors[32] = weakIntensity; motors[35] = weakIntensity;
            motors[36] = weakIntensity; motors[39] = weakIntensity;
            
            BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 200); // Longer duration
        }
    }

    private IEnumerator PlayReverberation(int startIntensity)
    {
        yield return new WaitForSeconds(0.1f);
        
        // Move up the spine: Row 3 -> Row 2 -> Row 1
        int[] motors = new int[40];
        int intensity = startIntensity / 2;

        // Row 3 (Middle Back)
        motors[29] = intensity; motors[30] = intensity;
        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);
        
        yield return new WaitForSeconds(0.1f);

        // Row 2 (Upper Back)
        motors = new int[40];
        intensity /= 2;
        motors[25] = intensity; motors[26] = intensity;
        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);
    }

    // 4. Tension (Rumble Mode)
    private void UpdateTensionFeedback()
    {
        // Tension spreads from base upwards
        int intensity = (int)(currentTension * 100);
        
        int[] motors = new int[40];
        
        // Always active at base
        motors[37] = intensity; motors[38] = intensity;

        // Spread up based on tension level
        if (currentTension > 0.3f) { motors[33] = intensity; motors[34] = intensity; }
        if (currentTension > 0.6f) { motors[29] = intensity; motors[30] = intensity; }
        if (currentTension > 0.9f) { motors[25] = intensity; motors[26] = intensity; }

        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);
    }
}
