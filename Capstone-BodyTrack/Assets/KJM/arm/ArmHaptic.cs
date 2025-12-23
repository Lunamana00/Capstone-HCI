using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

public enum ArmSide
{
    Left,
    Right
}

public class ArmHaptics : MonoBehaviour
{
    [Header("Settings")]
    public ArmSide armSide = ArmSide.Right; 

    [Header("References")]
    [SerializeField] private Rigidbody armRigidbody; 
    [SerializeField] private Transform shoulderPoint; 

    [Header("Sway Feedback (Swing/Wind)")]
    public bool enableSwayFeedback = true;
    public float swayVelocityThreshold = 2.0f; 
    public float swayIntensityMultiplier = 0.5f;

    [Header("Collision Feedback")]
    public bool enableCollisionFeedback = true;
    public float collisionIntensityMultiplier = 1.0f;
    public float maxArmLength = 0.7f; 

    [Header("Tension Feedback (Grabbing)")]
    public bool enableTensionFeedback = true;
    [Range(0f, 1f)] public float currentTension = 0f; 

    private float lastSwayTime;

    void Start()
    {
  
        if (armRigidbody == null) armRigidbody = GetComponent<Rigidbody>();
        if (shoulderPoint == null) shoulderPoint = transform;

  
        ArmCollision[] armCollisions = GetComponentsInChildren<ArmCollision>();
        if (armCollisions.Length > 0)
        {
            foreach (ArmCollision col in armCollisions)
            {
                col.OnArmCollision += TriggerCollisionFeedback;
            }
            Debug.Log($"ArmHaptics ({armSide}): Subscribed to {armCollisions.Length} collision sensors.");
        }
        else
        {
            Debug.LogWarning($"ArmHaptics ({armSide}): No ArmCollision components found in children.");
        }

    }

    void OnDestroy()
    {
     
        ArmCollision[] armCollisions = GetComponentsInChildren<ArmCollision>();
        foreach (ArmCollision col in armCollisions)
        {
            if (col != null) col.OnArmCollision -= TriggerCollisionFeedback;
        }
    }

    void Update()
    {
 
        if (enableTensionFeedback && currentTension > 0.01f)
        {

            UpdateTensionFeedback();
        }
    }


    private int[] GetSideMotors(int row, ArmSide side)
    {
        // row: 0(Top) ~ 4(Bottom)
        int[] indices = new int[2];

        if (side == ArmSide.Left)
        {
            // Left Side: Front Col 0 or 1 / Back Col 0 or 1 (Index�����δ� 20, 21...)
            // Vest Front Left: 0,1 / 4,5 / 8,9 ...
            // Vest Back Left: 20,21 / 24,25 ...
            indices[0] = row * 4 + 0; // Front Outer Left
            indices[1] = 20 + (row * 4) + 0; // Back Outer Left
        }
        else // Right
        {
            // Right Side: Front Col 3 / Back Col 3
            indices[0] = row * 4 + 3; // Front Outer Right
            indices[1] = 20 + (row * 4) + 3; // Back Outer Right
        }
        return indices;
    }



    // 2. Collision (�浹)
    public void TriggerCollisionFeedback(float impactForce, Vector3 contactPoint)
    {
        if (!enableCollisionFeedback) {

            Debug.Log("collsion feedback disable");
            return;
        }
        

        float intensityVal = Mathf.Clamp01(impactForce * collisionIntensityMultiplier);
        int intensity = (int)(intensityVal * 100);

  
        float distance = Vector3.Distance(shoulderPoint.position, contactPoint);
        bool isShoulderHit = distance < (maxArmLength * 0.3f); 

        int[] motors = new int[40];

        if (isShoulderHit)
        {
  
            int[] row0 = GetSideMotors(0, armSide);
            int[] row1 = GetSideMotors(1, armSide);

            foreach (int idx in row0) motors[idx] = intensity;
            foreach (int idx in row1) motors[idx] = intensity;

            BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 150);
            HapticsDebugBus.NotifyPlayMotors(PositionType.Vest, motors, 150);
            Debug.Log("Shoulder hit");
        }
        else
        {
            //(Reverberation)
            StartCoroutine(PlayArmReverberation(intensity));
            Debug.Log("hand hit");
        }
    }

    private IEnumerator PlayArmReverberation(int startIntensity)
    {
        // Row 1 -> 2 -> 3 -> 4

        for (int row = 1; row <= 4; row++) // 1부터 4까지 증가하도록 변경
        {
            int[] motors = new int[40];
            int Intensity = startIntensity;
            Intensity /= 2;

            int[] targetIndices = GetSideMotors(row, armSide);
            foreach (int idx in targetIndices) motors[idx] = Intensity;

            BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 80);
            HapticsDebugBus.NotifyPlayMotors(PositionType.Vest, motors, 80);
            yield return new WaitForSeconds(0.08f);
        }
    }

    // 3. Tension 
    private void UpdateTensionFeedback()
    {
        int intensity = (int)(currentTension * 80); 

        int[] motors = new int[40];

        for (int r = 1; r <= 4; r++)
        {
            int[] sideIndices = GetSideMotors(r, armSide);
            foreach (int idx in sideIndices)
            {
                motors[idx] = intensity;
            }
        }

        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, 100);
        HapticsDebugBus.NotifyPlayMotors(PositionType.Vest, motors, 100);
    }
}