using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

public class TailHaptics : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TailControllerPhysics tailPhysics;
    [SerializeField] private TailCollision tailCollision;
    [SerializeField] private Transform tailRoot;

    [Header("Idle Feedback (Weight)")]
    [SerializeField] private bool enableIdleFeedback = true;
    [SerializeField] private float idleIntensity = 0.1f; // 10%
    [SerializeField] private float breathingInterval = 4.0f; // 4 seconds per breath
    [SerializeField] private float breathingIntensity = 0.15f; // Slightly stronger during breath

    [Header("Inertia Feedback (Sway)")]
    [SerializeField] private bool enableSwayFeedback = true;
    [SerializeField] private float swayVelocityThreshold = 5.0f;
    [SerializeField] private float swayIntensityMultiplier = 0.5f;

    [Header("Collision Feedback")]
    [SerializeField] private bool enableCollisionFeedback = true;
    [SerializeField] private float collisionIntensityMultiplier = 1.0f;
    [SerializeField] private float maxImpactDistance = 1.0f; // Distance from root to consider "Tip"

    [Header("Tension Feedback (Grab)")]
    [SerializeField] private bool enableTensionFeedback = true;
    [Range(0, 1)] [SerializeField] private float currentTension = 0f;

    private float lastSwayTime;
    private const float SWAY_HAPTIC_INTERVAL = 0.1f;
    private float breathingTimer;

    void Start()
    {
        if (tailPhysics == null) tailPhysics = GetComponent<TailControllerPhysics>();
        if (tailCollision == null) tailCollision = GetComponent<TailCollision>();
        if (tailRoot == null && tailPhysics != null) tailRoot = tailPhysics.tailRoot;

        if (tailCollision != null)
        {
            tailCollision.OnTailCollision += TriggerCollisionFeedback;
        }
    }

    void OnDestroy()
    {
        if (tailCollision != null)
        {
            tailCollision.OnTailCollision -= TriggerCollisionFeedback;
        }
    }

    void Update()
    {
        if (enableIdleFeedback)
        {
            UpdateIdleFeedback();
        }

        if (enableSwayFeedback && Time.time - lastSwayTime > SWAY_HAPTIC_INTERVAL)
        {
            UpdateSwayFeedback();
            lastSwayTime = Time.time;
        }

        if (enableTensionFeedback && currentTension > 0.01f)
        {
            UpdateTensionFeedback();
        }
    }

    // 1. Idle & Weight (Continuous, Lumbar)
    private void UpdateIdleFeedback()
    {
        breathingTimer += Time.deltaTime;
        float currentIntensity = idleIntensity;

        // Simple Breathing Effect (Sine Wave)
        float breath = (Mathf.Sin(breathingTimer * (2f * Mathf.PI / breathingInterval)) + 1f) * 0.5f; // 0 to 1
        currentIntensity += breath * (breathingIntensity - idleIntensity);

        int intensity = (int)(currentIntensity * 100);

        // PlayPath on Bottom Row (Lumbar)
        // Vest Y: 0.0 is bottom, 1.0 is top.
        // We want bottom 1-2 rows. Y = 0.1 - 0.2.
        // X: 0.0 to 1.0 (Full width)
        
        // We use 4 points to define a line across the bottom
        float[] xValues = { 0.2f, 0.8f };
        float[] yValues = { 0.1f, 0.1f };
        int[] intensities = { intensity, intensity };

        // Duration small to allow continuous update
        BhapticsLibrary.PlayPath((int)PositionType.Vest, xValues, yValues, intensities, 100);
    }

    // 2. Inertia & Sway (Path Mode, Centrifugal)
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
            
            // We want a path that moves from Center to Side.
            // Center X = 0.5.
            // Right X = 0.8, Left X = 0.2.

            float[] xValues = new float[2];
            float[] yValues = new float[2];
            int[] intensities = new int[2];

            float yPos = 0.3f; // Slightly above lumbar

            if (angularVelY > 0) // Turning Right -> Force to Left
            {
                // Path: Center -> Left
                xValues[0] = 0.5f; yValues[0] = yPos; intensities[0] = intensity / 2;
                xValues[1] = 0.2f; yValues[1] = yPos; intensities[1] = intensity;
            }
            else // Turning Left -> Force to Right
            {
                // Path: Center -> Right
                xValues[0] = 0.5f; yValues[0] = yPos; intensities[0] = intensity / 2;
                xValues[1] = 0.8f; yValues[1] = yPos; intensities[1] = intensity;
            }

            BhapticsLibrary.PlayPath((int)PositionType.Vest, xValues, yValues, intensities, 150);
        }
    }

    // 3. Collision (Dot Mode, Impact + Reverberation)
    public void TriggerCollisionFeedback(float impactForce, Vector3 contactPoint)
    {
        if (!enableCollisionFeedback) return;

        float intensityVal = Mathf.Clamp01(impactForce * collisionIntensityMultiplier);
        int intensity = (int)(intensityVal * 100);

        // Calculate distance from root
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
        System.Array.Clear(motors, 0, 40);
        intensity /= 2;
        motors[25] = intensity; motors[26] = intensity;
        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);
    }

    // 4. Tension (Grab)
    private void UpdateTensionFeedback()
    {
        // Rumble effect
        // Higher tension = wider spread upwards from root
        
        int intensity = (int)(currentTension * 100);
        int[] motors = new int[40];

        // Always active: Bottom Root (Row 5)
        motors[37] = intensity; motors[38] = intensity;

        // Spread up based on tension
        if (currentTension > 0.3f) // Row 4
        {
            motors[33] = intensity; motors[34] = intensity;
        }
        if (currentTension > 0.6f) // Row 3
        {
            motors[29] = intensity; motors[30] = intensity;
        }
        if (currentTension > 0.9f) // Row 2
        {
            motors[25] = intensity; motors[26] = intensity;
        }

        // Add random variation for "Rumble" / "Strain" feel
        if (Random.value > 0.5f)
        {
            // Randomly reduce intensity slightly to create "stutter"
             BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 50);
        }
        else
        {
             BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 50);
        }
    }

    // Public API for other scripts to set tension
    public void SetTension(float tension)
    {
        currentTension = Mathf.Clamp01(tension);
    }
}
