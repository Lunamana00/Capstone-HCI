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
    public bool enableIdlesway = false;

    private IMUReciever imuReciever;
    private Rigidbody rootRigidbody;
    private List<Rigidbody> boneRigidbodies = new List<Rigidbody>();
    private Vector3 smoothedTargetDir;
    private Vector3 currentVelocity; // For SmoothDamp


    // --- New Physics Variables ---
    private List<Quaternion> initialLocalRotations = new List<Quaternion>();
    private Quaternion rootInitialGlobalRotation;

    void Start()
    {
        imuReciever = FindObjectOfType<IMUReciever>();
        
        if (tailRoot == null)
        {
            Debug.LogError("Tail Root not assigned.");
            enabled = false;
            return;
        }

        // Ensure Root Rigidbody
        rootRigidbody = tailRoot.GetComponent<Rigidbody>();
        if (rootRigidbody == null) rootRigidbody = tailRoot.gameObject.AddComponent<Rigidbody>();

        // Auto-populate bones
        if (tailBones == null || tailBones.Count == 0)
        {
            tailBones = new List<Transform>();
            AddChildrenToTailBones(tailRoot);
        }

        SetupPhysicsBones();
        
        // Capture Initial Pose (Rest Pose)
        rootInitialGlobalRotation = tailRoot.rotation;
        initialLocalRotations.Clear();
        foreach (Transform bone in tailBones)
        {
            initialLocalRotations.Add(bone.localRotation);
        }
    }

    private void AddChildrenToTailBones(Transform parent)
    {
        foreach (Transform child in parent)
        {
            // Stop if we hit another controller (nested tails?) - usually not the case but good safety
            if (child.GetComponent<TailControllerPhysics>() != null) continue;

            tailBones.Add(child);
            AddChildrenToTailBones(child);
        }
    }

    void SetupPhysicsBones()
    {
        boneRigidbodies.Clear();
        Rigidbody previousRb = rootRigidbody;

        // Root Setup
        // Root should be Kinematic if attached to a character, but here we want to control it with physics/IMU?
        // If it's attached to a moving character, it should probably be IsKinematic = true (or connected via Joint).
        // For this standalone simulation, let's keep it Kinematic but rotate it via MoveRotation.
        rootRigidbody.useGravity = false; 
        rootRigidbody.isKinematic = true; 
        boneRigidbodies.Add(rootRigidbody);

        // Haptics on Root
        if (rootRigidbody.GetComponent<Collider>() != null && rootRigidbody.GetComponent<HapticFeedbackSender>() == null)
            rootRigidbody.gameObject.AddComponent<HapticFeedbackSender>();

        // Child Bones Setup
        for (int i = 0; i < tailBones.Count; i++)
        {
            Transform currentBone = tailBones[i];
            Rigidbody currentRb = currentBone.GetComponent<Rigidbody>();

            if (currentRb == null) currentRb = currentBone.gameObject.AddComponent<Rigidbody>();

            // Enable Gravity for natural sagging
            currentRb.useGravity = true; 
            currentRb.linearDamping = damping;
            currentRb.angularDamping = damping;
            currentRb.mass = 0.5f;

            boneRigidbodies.Add(currentRb);

            // Joint Setup (ConfigurableJoint for better control)
            ConfigurableJoint joint = currentBone.GetComponent<ConfigurableJoint>();
            if (joint == null) joint = currentBone.gameObject.AddComponent<ConfigurableJoint>();
            
            // Clean up old CharacterJoint if exists
            CharacterJoint oldJoint = currentBone.GetComponent<CharacterJoint>();
            if (oldJoint != null) Destroy(oldJoint);

            joint.connectedBody = previousRb;
            joint.autoConfigureConnectedAnchor = true;
            
            // Lock position, limit rotation
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            // Limits
            SoftJointLimit limit = new SoftJointLimit();
            limit.limit = 45f; // Reasonable range
            joint.lowAngularXLimit = limit;
            joint.highAngularXLimit = limit;
            joint.angularYLimit = limit;
            joint.angularZLimit = limit;

            previousRb = currentRb;

            // Colliders & Haptics
            if (currentBone.GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = currentBone.gameObject.AddComponent<CapsuleCollider>();
                collider.direction = 2; // Z-axis
                collider.radius = 0.08f;
                collider.height = 0.5f;
            }
            
            // Add TailCollision for bHaptics
            if (currentBone.GetComponent<TailCollision>() == null)
            {
                currentBone.gameObject.AddComponent<TailCollision>();
            }
        }
        
        // Ignore Self Collisions
        if (GetComponent<TailSelfCollisionIgnorer>() == null)
        {
            gameObject.AddComponent<TailSelfCollisionIgnorer>();
        }
        
        // Setup Haptics Manager
        SetupTailHaptics();
    }

    void SetupTailHaptics()
    {
        TailHaptics tailHaptics = tailRoot.GetComponent<TailHaptics>();
        if (tailHaptics == null)
        {
            tailHaptics = tailRoot.gameObject.AddComponent<TailHaptics>();
        }
    }

    void Update()
    {
        if (simulateInput)
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            simulatedAccel = new Vector3(moveX, 0, moveZ) * simulationIntensity;
        }
    }

    void FixedUpdate()
    {
        // 1. Root Control (IMU + Sway)
        UpdateRootRotation();
        // 2. Muscle Control (Stiffness for children)
        UpdateMuscleForces();
        // 3. Idle Contro;
        if (enableIdlesway)
        {
            ApplyIdleSway();
        }
    }

    void UpdateRootRotation()
    {
        // Get Input
        Vector3 currentAccel = simulateInput ? simulatedAccel : (imuReciever != null ? imuReciever.GetLatestAccel() : Vector3.zero);
        
        // Calculate Target Rotation for Root
        // We want the root to rotate based on IMU, relative to its initial rotation.
        // IMU X -> Pitch (Up/Down), IMU Z -> Yaw (Left/Right) usually.
        
        float pitch = currentAccel.z * forceMagnitude; // Forward/Back tilt
        float yaw = currentAccel.x * forceMagnitude;   // Left/Right tilt
        Debug.Log($" {currentAccel.z} {currentAccel.x}");

        // Add Sway
        float sway = Mathf.Sin(Time.time * idleSwaySpeed) * idleSwayAmount;
        yaw += sway;

        Quaternion targetRot = rootInitialGlobalRotation * Quaternion.Euler(pitch, yaw, 0f);
        
        // Apply to Root Rigidbody
        rootRigidbody.MoveRotation(Quaternion.Slerp(rootRigidbody.rotation, targetRot, Time.fixedDeltaTime * 1f));
    }

    void UpdateMuscleForces()
    {
        // Apply torque to child bones to maintain shape (Stiffness)
        for (int i = 0; i < tailBones.Count; i++)
        {
            Transform bone = tailBones[i];
            Rigidbody rb = boneRigidbodies[i + 1]; // +1 because index 0 is root
            Quaternion initialLocal = initialLocalRotations[i];

            // Target Global Rotation = Parent Rotation * Initial Local Rotation
            Quaternion targetGlobal = bone.parent.rotation * initialLocal;

            // Calculate rotation difference
            Quaternion deltaRot = targetGlobal * Quaternion.Inverse(bone.rotation);
            
            // Convert to Angle-Axis
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
            
            // Normalize angle to -180 ~ 180
            if (angle > 180f) angle -= 360f;

            // Stiffness Gradient (Base is stiffer, Tip is looser)
            float normalizedPos = (float)i / tailBones.Count;
            float stiffness = stiffnessCurve.Evaluate(normalizedPos) * muscleForce;

            // Apply Torque (Spring)
            // T = k * theta - c * omega (Spring - Damper)
            if (Mathf.Abs(angle) > 0.1f)
            {
                Vector3 springTorque = axis.normalized * angle * stiffness;
                Vector3 dampingTorque = -rb.angularVelocity * damping;
                
                rb.AddTorque(springTorque + dampingTorque, ForceMode.Acceleration);
            }
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

    // --- Public Properties ---
    public Vector3 CurrentAngularVelocity
    {
        get
        {
            if (boneRigidbodies.Count > 1 && boneRigidbodies[1] != null)
                return boneRigidbodies[1].angularVelocity;
            return Vector3.zero;
        }
    }
}