using System.Collections.Generic;
using UnityEngine;

public class LimbPhysicsController : MonoBehaviour, ILimbPhysicsData
{
    [Header("Limb Setup")]
    public Transform limbRoot;
    public List<Transform> limbBones;
    public LimbPhysicsConfig config;

    [Header("Input")]
    [SerializeField] private MonoBehaviour imuProviderBehaviour;
    private IImuInputProvider imuProvider;

    [Header("Input Toggle")]
    public bool allowKeyboardToggle = true;
    public KeyCode toggleInputKey = KeyCode.I;

    [Header("Debug / Simulation")]
    public bool simulateInput = false;
    public float simulationIntensity = 10.0f;
    public Vector3 simulatedAccel = Vector3.zero;
    public bool useRawKeyboardInput = true;
    public float keyboardInputMultiplier = 3.0f;
    public float keyboardForceMultiplier = 1.0f;

    [Header("Collision Emitters")]
    public bool addCollisionEmitters = true;
    public bool ignoreSelfCollisions = true;

    private Rigidbody rootRigidbody;
    private readonly List<Rigidbody> boneRigidbodies = new List<Rigidbody>();
    private readonly List<Quaternion> initialLocalRotations = new List<Quaternion>();
    private readonly List<Quaternion> initialRotations = new List<Quaternion>();
    private Quaternion rootInitialGlobalRotation;

    public Transform RootTransform => limbRoot != null ? limbRoot : transform;

    public Vector3 CurrentAngularVelocity
    {
        get
        {
            if (boneRigidbodies.Count > 1 && boneRigidbodies[1] != null)
                return boneRigidbodies[1].angularVelocity;
            return Vector3.zero;
        }
    }

    private void Start()
    {
        if (limbRoot == null)
        {
            Debug.LogError("Limb Root not assigned.");
            enabled = false;
            return;
        }

        SetupInputProvider();
        SetupBones();
        SetupPhysicsChain();

        rootInitialGlobalRotation = limbRoot.rotation;
        initialLocalRotations.Clear();
        initialRotations.Clear();
        foreach (Transform bone in limbBones)
        {
            initialLocalRotations.Add(bone.localRotation);
            initialRotations.Add(bone.localRotation);
        }
    }

    private void SetupInputProvider()
    {
        if (imuProviderBehaviour != null)
        {
            imuProvider = imuProviderBehaviour as IImuInputProvider;
        }
        if (imuProvider == null)
        {
            imuProvider = FindObjectOfType<IMUReciever>();
        }
        if (imuProvider == null)
        {
            Debug.LogWarning("IMU input provider not found. Limb will use zero input.");
        }
    }

    private void SetupBones()
    {
        if (limbBones != null && limbBones.Count > 0) return;

        limbBones = new List<Transform>();
        AddChildrenToBones(limbRoot);
    }

    private void AddChildrenToBones(Transform parent)
    {
        foreach (Transform child in parent)
        {
            limbBones.Add(child);
            AddChildrenToBones(child);
        }
    }

    private void SetupPhysicsChain()
    {
        boneRigidbodies.Clear();
        rootRigidbody = limbRoot.GetComponent<Rigidbody>();
        if (rootRigidbody == null) rootRigidbody = limbRoot.gameObject.AddComponent<Rigidbody>();

        rootRigidbody.useGravity = false;
        rootRigidbody.isKinematic = true;
        boneRigidbodies.Add(rootRigidbody);

        Rigidbody previousRb = rootRigidbody;
        foreach (Transform bone in limbBones)
        {
            Rigidbody rb = bone.GetComponent<Rigidbody>();
            if (rb == null) rb = bone.gameObject.AddComponent<Rigidbody>();

            rb.useGravity = true;
            rb.linearDamping = config != null ? config.damping : 0.5f;
            rb.angularDamping = config != null ? config.damping : 0.5f;
            rb.mass = config != null ? config.boneMass : 0.5f;
            boneRigidbodies.Add(rb);

            ConfigurableJoint joint = bone.GetComponent<ConfigurableJoint>();
            if (joint == null) joint = bone.gameObject.AddComponent<ConfigurableJoint>();

            joint.connectedBody = previousRb;
            joint.autoConfigureConnectedAnchor = true;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            SoftJointLimit limit = new SoftJointLimit();
            limit.limit = config != null ? config.jointLimit : 45f;
            joint.lowAngularXLimit = limit;
            joint.highAngularXLimit = limit;
            joint.angularYLimit = limit;
            joint.angularZLimit = limit;

            if (bone.GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = bone.gameObject.AddComponent<CapsuleCollider>();
                collider.direction = 2;
                collider.radius = config != null ? config.colliderRadius : 0.08f;
                collider.height = config != null ? config.colliderHeight : 0.5f;
            }

            if (addCollisionEmitters && bone.GetComponent<LimbCollisionEmitter>() == null)
            {
                bone.gameObject.AddComponent<LimbCollisionEmitter>();
            }

            previousRb = rb;
        }

        if (ignoreSelfCollisions && GetComponent<LimbSelfCollisionIgnorer>() == null)
        {
            gameObject.AddComponent<LimbSelfCollisionIgnorer>();
        }
    }

    private void Update()
    {
        if (allowKeyboardToggle && Input.GetKeyDown(toggleInputKey))
        {
            simulateInput = !simulateInput;
            if (!simulateInput)
            {
                simulatedAccel = Vector3.zero;
            }
            Debug.Log($"Limb input source: {(simulateInput ? "Keyboard" : "IMU")}");
        }

        if (simulateInput)
        {
            float moveX = useRawKeyboardInput ? Input.GetAxisRaw("Horizontal") : Input.GetAxis("Horizontal");
            float moveZ = useRawKeyboardInput ? Input.GetAxisRaw("Vertical") : Input.GetAxis("Vertical");
            simulatedAccel = new Vector3(moveX, 0, moveZ) * simulationIntensity * keyboardInputMultiplier;
        }
    }

    private void FixedUpdate()
    {
        UpdateRootRotation();
        UpdateMuscleForces();
    }

    private void UpdateRootRotation()
    {
        Vector3 currentAccel = simulateInput ? simulatedAccel : (imuProvider != null ? imuProvider.GetLatestAccel() : Vector3.zero);
        if (config != null)
        {
            currentAccel += config.imuOffset;
        }

        float forceMagnitude = config != null ? config.forceMagnitude : 100f;
        float forceScale = simulateInput ? keyboardForceMultiplier : 1f;
        float pitch = currentAccel.z * forceMagnitude * forceScale;
        float yaw = currentAccel.x * forceMagnitude * forceScale;

        if (config != null)
        {
            float sway = Mathf.Sin(Time.time * config.idleSwaySpeed) * config.idleSwayAmount;
            yaw += sway;
        }

        Quaternion targetRot = rootInitialGlobalRotation * Quaternion.Euler(pitch, yaw, 0f);
        rootRigidbody.MoveRotation(Quaternion.Slerp(rootRigidbody.rotation, targetRot, Time.fixedDeltaTime));
    }

    private void UpdateMuscleForces()
    {
        for (int i = 0; i < limbBones.Count; i++)
        {
            Transform bone = limbBones[i];
            Rigidbody rb = boneRigidbodies[i + 1];
            Quaternion initialLocal = initialLocalRotations[i];

            Quaternion targetGlobal = bone.parent.rotation * initialLocal;
            Quaternion deltaRot = targetGlobal * Quaternion.Inverse(bone.rotation);
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            float normalizedPos = limbBones.Count > 0 ? (float)i / limbBones.Count : 0f;
            float stiffness = (config != null ? config.stiffnessCurve.Evaluate(normalizedPos) : 1f) * (config != null ? config.muscleForce : 20f);

            if (Mathf.Abs(angle) > 0.1f)
            {
                Vector3 springTorque = axis.normalized * angle * stiffness;
                Vector3 dampingTorque = -rb.angularVelocity * (config != null ? config.damping : 0.5f);
                rb.AddTorque(springTorque + dampingTorque, ForceMode.Acceleration);
            }
        }
    }

    public void ResetDynamics()
    {
        for (int i = 0; i < limbBones.Count; i++)
        {
            Transform bone = limbBones[i];
            Rigidbody rb = bone.GetComponent<Rigidbody>();
            if (i < initialRotations.Count)
            {
                bone.localRotation = initialRotations[i];
            }
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }
        }
    }
}
