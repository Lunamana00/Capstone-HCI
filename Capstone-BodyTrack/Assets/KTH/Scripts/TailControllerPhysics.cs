using UnityEngine;
using System.Collections.Generic;

public class TailControllerPhysics : MonoBehaviour
{
    public Transform tailRoot; // The first bone of the tail
    public List<Transform> tailBones; // List of all tail bones

    [Header("IMU Feedback Settings")]
    public float forceMagnitude = 100f; // Base force magnitude
    public float torqueMagnitude = 50f; // Base torque magnitude
    public float damping = 0.5f; // Rigidbody damping
    public Vector3 imuOffset = new Vector3(0, 0, 0); // Calibration offset
    public float gravity = 9.81f;

    [Header("Active Muscle Settings")]
    public float muscleForce = 20f; // How hard the tail tries to hold its shape
    public AnimationCurve stiffnessCurve = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0.1f)); // Stiff at base, loose at tip
    public float dragMultiplier = 0.1f; // Dynamic drag based on velocity

    [Header("Organic Movement")]
    public float idleSwayAmount = 5f; // Degrees of sway
    public float idleSwaySpeed = 1f; // Speed of sway
    public float inertiaDelay = 0.1f; // Time delay for inertia effect (simulated via smoothing)

    [Header("Debug / Simulation")]
    public bool simulateInput = false;
    public float simulationIntensity = 10.0f; // Default to 10 for stronger effect
    public Vector3 simulatedAccel = Vector3.zero;

    private IMUReciever imuReciever;
    private Rigidbody rootRigidbody;
    private List<Rigidbody> boneRigidbodies = new List<Rigidbody>();
    private Vector3 smoothedTargetDir;
    private Vector3 currentVelocity; // For SmoothDamp

    void Start()
    {
        imuReciever = FindObjectOfType<IMUReciever>();
        if (imuReciever == null)
        {
            Debug.LogError("IMUReciever not found.");
            // enabled = false; // Don't disable if we want to simulate
        }

        if (tailRoot == null)
        {
            Debug.LogError("Tail Root not assigned.");
            enabled = false;
            return;
        }

        rootRigidbody = tailRoot.GetComponent<Rigidbody>();
        if (rootRigidbody == null)
        {
            // Ensure root has RB
            rootRigidbody = tailRoot.gameObject.AddComponent<Rigidbody>();
        }

        // Auto-populate if empty
        if (tailBones == null || tailBones.Count == 0)
        {
            tailBones = new List<Transform>();
            AddChildrenToTailBones(tailRoot);
        }

        SetupPhysicsBones();
    }

    private void AddChildrenToTailBones(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.GetComponent<TailControllerPhysics>() == null)
            {
                tailBones.Add(child);
                AddChildrenToTailBones(child);
            }
        }
    }

    void SetupPhysicsBones()
    {
        boneRigidbodies.Clear();
        Rigidbody previousRb = rootRigidbody;

        // Root setup
        rootRigidbody.useGravity = false;
        rootRigidbody.isKinematic = true; // Anchor to body
        rootRigidbody.linearDamping = damping;
        rootRigidbody.angularDamping = damping;
        boneRigidbodies.Add(rootRigidbody);

        // Ensure HapticSender on Root if it has a collider
        if (rootRigidbody.GetComponent<Collider>() != null && rootRigidbody.GetComponent<HapticFeedbackSender>() == null)
            rootRigidbody.gameObject.AddComponent<HapticFeedbackSender>();


        for (int i = 0; i < tailBones.Count; i++)
        {
            Transform currentBone = tailBones[i];
            Rigidbody currentRb = currentBone.GetComponent<Rigidbody>();

            if (currentRb == null)
                currentRb = currentBone.gameObject.AddComponent<Rigidbody>();

            currentRb.useGravity = true; // Enable Gravity for natural hanging
            currentRb.linearDamping = damping;
            currentRb.angularDamping = damping;
            currentRb.mass = 0.5f; // Lighter mass for better reaction

            boneRigidbodies.Add(currentRb);

            // Joint Setup
            CharacterJoint joint = currentBone.GetComponent<CharacterJoint>();
            if (joint == null)
                joint = currentBone.gameObject.AddComponent<CharacterJoint>();

            joint.connectedBody = previousRb;
            joint.autoConfigureConnectedAnchor = true;
            joint.enableProjection = true; // Prevents separation
            joint.projectionDistance = 0.1f;
            joint.projectionAngle = 180f;

            // Soft limits for organic feel
            SoftJointLimitSpring limitSpring = new SoftJointLimitSpring();
            limitSpring.spring = 5f; // Softer spring
            limitSpring.damper = 0.5f;
            joint.swingLimitSpring = limitSpring;
            joint.twistLimitSpring = limitSpring;

            // Limits - Relaxed for hanging
            SoftJointLimit sLimit = new SoftJointLimit();
            sLimit.limit = 170f; // Allow almost full swing
            joint.swing1Limit = sLimit;
            joint.swing2Limit = sLimit;

            SoftJointLimit tLimit = new SoftJointLimit();
            tLimit.limit = 30f; // More twist
            joint.lowTwistLimit = tLimit;
            joint.highTwistLimit = tLimit;

            previousRb = currentRb;

            // Collider & Haptics
            if (currentBone.GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = currentBone.gameObject.AddComponent<CapsuleCollider>();
                collider.direction = 2; // Z-axis
                collider.radius = 0.1f;
                collider.height = 0.5f;
            }

            if (currentBone.GetComponent<HapticFeedbackSender>() == null)
            {
                currentBone.gameObject.AddComponent<HapticFeedbackSender>();
            }
        }
    }

    void Update()
    {
        // Keyboard Control for Simulation
        if (simulateInput)
        {
            float moveX = Input.GetAxis("Horizontal"); // A/D or Left/Right
            float moveZ = Input.GetAxis("Vertical");   // W/S or Up/Down
            
            // Map to acceleration (tilting the sensor)
            // Multiply by intensity to simulate strong tilt
            simulatedAccel = new Vector3(moveX, 0, moveZ) * simulationIntensity;
        }
    }

    void FixedUpdate()
    {
        // 1. Get IMU Input
        Vector3 currentAccel;
        
        if (simulateInput)
        {
            currentAccel = simulatedAccel;
        }
        else
        {
            currentAccel = imuReciever != null ? imuReciever.GetLatestAccel() : Vector3.zero;
        }
        
        // 2. Calculate Target Direction (with Inertia Smoothing)
        // We smooth the input to simulate the heavy tail lagging behind body movements
        Vector3 rawTargetDir = (currentAccel * forceMagnitude) + Vector3.down * gravity;
        Quaternion imuRotationOffset = Quaternion.Euler(imuOffset);
        Vector3 targetWorldDir = imuRotationOffset * rawTargetDir.normalized;

        smoothedTargetDir = Vector3.SmoothDamp(smoothedTargetDir, targetWorldDir, ref currentVelocity, inertiaDelay);

        // 3. Apply Active Muscle Forces
        ApplyMuscleForces(smoothedTargetDir);

        // 4. Apply Idle Sway
        ApplyIdleSway();
    }

    void ApplyMuscleForces(Vector3 targetDir)
    {
        // We apply torque to align the tail with the target direction
        // But we distribute it: Base gets more force to hold up the tail, Tip gets less (whiplash effect)
        
        for (int i = 0; i < boneRigidbodies.Count; i++)
        {
            Rigidbody rb = boneRigidbodies[i];
            float normalizedPos = (float)i / boneRigidbodies.Count;
            float stiffness = stiffnessCurve.Evaluate(normalizedPos);

            // Calculate rotation to align Bone's Length vector with Target Direction
            // Assuming standard Unity bone chain where Z-axis (Forward) is the bone length.
            // If the tail sticks out horizontally, it's because we were aligning -Up to Gravity.
            // Now we align Forward to Gravity so it hangs.
            

            Vector3 currentBoneVector = rb.transform.forward; // Changed from -rb.transform.up
            
            Vector3 torqueAxis = Vector3.Cross(currentBoneVector, targetDir);
            float angleDiff = Vector3.Angle(currentBoneVector, targetDir);

            if (angleDiff > 0.1f)
            {
                // Active Muscle Torque: Tries to correct the angle
                // Stiffness determines how strong this correction is
                // Removed Time.fixedDeltaTime because ForceMode.Acceleration already handles time step internally for continuous application?
                // Actually ForceMode.Acceleration adds 'a' to velocity? No, it adds a*dt.
                // If we pass X, change in vel is X*dt.
                // Previously we passed X*dt, so change was X*dt*dt. Too small.
                rb.AddTorque(torqueAxis.normalized * angleDiff * muscleForce * stiffness, ForceMode.Acceleration);
            }

            // Dynamic Drag: Increase drag if moving fast to prevent jitter
            rb.angularDamping = damping + (rb.angularVelocity.magnitude * dragMultiplier);
        }
    }

    void ApplyIdleSway()
    {
        // Simple Sine wave sway based on time
        float swayAngle = Mathf.Sin(Time.time * idleSwaySpeed) * idleSwayAmount;
        
        // Apply torque to the root bone to initiate the sway wave
        if (boneRigidbodies.Count > 0)
        {
             // Sway around the local Forward axis (Z) -> Roll.
             boneRigidbodies[0].AddTorque(tailRoot.forward * swayAngle * 0.5f, ForceMode.Force);
        }
    }

    // --- Public Properties for Haptics ---
    public Vector3 CurrentAngularVelocity
    {
        get
        {
            // Return the angular velocity of the first active joint (base of tail)
            if (boneRigidbodies.Count > 0 && boneRigidbodies[0] != null)
            {
                return boneRigidbodies[0].angularVelocity;
            }
            return Vector3.zero;
        }
    }
}