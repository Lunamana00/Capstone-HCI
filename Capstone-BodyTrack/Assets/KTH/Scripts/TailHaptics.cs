using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

public class TailHaptics : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TailControllerPhysics tailPhysics; // tail physics를 받음 
    [SerializeField] private Transform tailRoot; // tail root의 위치를 받음 

    [Header("Sway Feedback (Inertia)")]
    public bool enableSwayFeedback = true; //sway를 on 하거나 off 하거나 (idle상태 흔들림)
    public float swayVelocityThreshold = 10f; // Minimum angular velocity 
    public float swayIntensityMultiplier = 0.5f;

    [Header("Idle Feedback (Weight)")]
    [SerializeField] private bool enableIdleFeedback = true;
    [SerializeField] private float idleIntensity = 0.1f; // 10%
    [SerializeField] private float breathingInterval = 4.0f; // 4 seconds per breath
    [SerializeField] private float breathingIntensity = 0.15f; // Slightly stronger during breaths

    [Header("Collision Feedback")]
    public bool enableCollisionFeedback = true;
    public float collisionIntensityMultiplier = 0.7f;
    public float maxImpactDistance = 1.0f; // Distance from root to be considered "base"

    [Header("Tension Feedback (Grabbing)")]
    public bool enableTensionFeedback = true;
    [Range(0f, 1f)] public float currentTension = 0f; // Updated by external script
    public bool IsHapticsActive => globalHapticEnabled;

    private float lastSwayTime;

    [SerializeField] private MonoBehaviour hapticOutputBehaviour;
    private IHapticOutput hapticOutput;

    [SerializeField] private bool globalHapticEnabled = true;

    void Start()
    {
        if (tailPhysics == null) tailPhysics = GetComponent<TailControllerPhysics>();
        if (tailRoot == null && tailPhysics != null) tailRoot = tailPhysics.tailRoot;

        if (hapticOutputBehaviour != null)
        {
            hapticOutput = hapticOutputBehaviour as IHapticOutput;
        }
        if (hapticOutput == null)
        {
            hapticOutput = GetComponent<BhapticsVestOutput>();
            if (hapticOutput == null)
            {
                hapticOutput = gameObject.AddComponent<BhapticsVestOutput>();
            }
        }

        // Find all TailCollision components in tail bones and subscribe to their events
        TailCollision[] tailCollisions = GetComponentsInChildren<TailCollision>(); // 자손 tail collision 전부 가져오기 
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
        // 1. 햅틱 켜기 (H 키)
        if (Input.GetKeyDown(KeyCode.H))
        {
            SetHapticsState(true);
        }

        // 2. 햅틱 끄기 (J 키 - 원하시는 키로 변경 가능, 예: KeyCode.N)
        if (Input.GetKeyDown(KeyCode.J))
        {
            SetHapticsState(false);
        }

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




    private void PlayMotors(int[] motors, int durationMs)
    {
        if (hapticOutput == null) return;
        hapticOutput.PlayMotors(PositionType.Vest, motors, durationMs);
    }

    // 2. Inertia & Sway (Motor Mode, Centrifugal)
    private void UpdateSwayFeedback()
    {
        Debug.Log("Sway feedback");
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

            PlayMotors(motors, 100);
        }
    }

    // 3. Collision (Dot Mode, Impact + Reverberation)
    public void TriggerCollisionFeedback(float impactForce, Vector3 contactPoint)
    {
        if (!enableCollisionFeedback) return;

        float intensityVal = Mathf.Clamp01(impactForce+1 * collisionIntensityMultiplier);
        int intensity = (int)((intensityVal + 3)*100);

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
            
            PlayMotors(motors, 100); // Short & Sharp
            
            // Reverberation (Echo up the spine)
            StartCoroutine(PlayReverberation(intensity));
        }
        else
        {
            // Weak, Diffuse Vibration (Impact at Tip)
            // Spread across bottom area but weaker
            int weakIntensity = intensity * 8 / 10;
            int[] motors = new int[40];
            // Activate wider range on bottom rows
            motors[32] = weakIntensity; motors[35] = weakIntensity;
            motors[36] = weakIntensity; motors[39] = weakIntensity;
            
            PlayMotors(motors, 200); // Longer duration
            Debug.Log("weak");
        }
    }

    private IEnumerator PlayReverberation(int startIntensity)
    {
        yield return new WaitForSeconds(0.1f);
        
        // Move up the spine: Row 3 -> Row 2 -> Row 1
        int[] motors = new int[40];
        int intensity = startIntensity;

        // Row 3 (Middle Back)
        motors[29] = intensity; motors[30] = intensity;
        PlayMotors(motors, 100);
        
        yield return new WaitForSeconds(0.1f);

        // Row 2 (Upper Back)
        motors = new int[40];
        intensity /= 2;
        motors[25] = intensity; motors[26] = intensity;
        PlayMotors(motors, 100);
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

        PlayMotors(motors, 100);
    }

    public void SetHapticsState(bool isOn)
    {
        globalHapticEnabled = isOn;
        Debug.Log($"Haptics System is now: {(globalHapticEnabled ? "<color=green>ON</color>" : "<color=red>OFF</color>")}");

        // (선택 사항) 끄는 순간 현재 작동 중인 모든 진동을 멈추고 싶다면:
        if (!isOn)
        {
            hapticOutput?.StopAll(); // bHaptics SDK 함수
        }
    }

}
