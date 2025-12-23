using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

public class LimbHapticsController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform limbRoot;
    [SerializeField] private MonoBehaviour limbPhysicsBehaviour;
    [SerializeField] private MonoBehaviour hapticOutputBehaviour;

    [Header("Haptic Modules")]
    public InertiaHapticMapper inertia = new InertiaHapticMapper();
    public ImpactHapticMapper impact = new ImpactHapticMapper();
    public TensionHapticMapper tension = new TensionHapticMapper();

    [Header("Runtime Inputs")]
    [Range(0f, 1f)] public float currentTension = 0f;
    public bool globalHapticEnabled = true;

    private ILimbPhysicsData limbPhysics;
    private IHapticOutput hapticOutput;
    private readonly List<ILimbCollisionSource> collisionSources = new List<ILimbCollisionSource>();

    private void Start()
    {
        if (limbPhysicsBehaviour != null)
        {
            limbPhysics = limbPhysicsBehaviour as ILimbPhysicsData;
        }
        if (limbPhysics == null)
        {
            limbPhysics = GetComponent<ILimbPhysicsData>();
        }

        if (limbRoot == null && limbPhysics != null)
        {
            limbRoot = limbPhysics.RootTransform;
        }
        if (limbRoot == null)
        {
            limbRoot = transform;
        }

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

        CacheCollisionSources();
        SubscribeCollisions();
    }

    private void OnDestroy()
    {
        UnsubscribeCollisions();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            SetHapticsState(true);
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            SetHapticsState(false);
        }

        if (!globalHapticEnabled) return;

        if (inertia != null && inertia.enabled && limbPhysics != null)
        {
            inertia.Process(limbPhysics.CurrentAngularVelocity, PlayMotors);
        }

        if (tension != null && tension.enabled && currentTension > 0.01f)
        {
            tension.Process(currentTension, PlayMotors);
        }
    }

    public void SetHapticsState(bool isOn)
    {
        globalHapticEnabled = isOn;
        if (!isOn)
        {
            hapticOutput?.StopAll();
        }
    }

    private void CacheCollisionSources()
    {
        collisionSources.Clear();
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ILimbCollisionSource source)
            {
                collisionSources.Add(source);
            }
        }
    }

    private void SubscribeCollisions()
    {
        foreach (ILimbCollisionSource source in collisionSources)
        {
            source.OnLimbCollision += HandleCollision;
        }
    }

    private void UnsubscribeCollisions()
    {
        foreach (ILimbCollisionSource source in collisionSources)
        {
            if (source != null)
            {
                source.OnLimbCollision -= HandleCollision;
            }
        }
    }

    private void HandleCollision(float force, Vector3 contactPoint)
    {
        if (!globalHapticEnabled) return;
        if (impact == null || !impact.enabled) return;

        Vector3 rootPos = limbRoot != null ? limbRoot.position : transform.position;
        impact.Process(force, contactPoint, rootPos, PlayMotors, StartCoroutine);
    }

    private void PlayMotors(int[] motors, int durationMs)
    {
        if (hapticOutput == null) return;
        hapticOutput.PlayMotors(PositionType.Vest, motors, durationMs);
    }

    [System.Serializable]
    public class InertiaHapticMapper
    {
        public bool enabled = true;
        public float swayVelocityThreshold = 10f;
        public float swayIntensityMultiplier = 0.5f;
        public float updateInterval = 0.1f;

        private float lastUpdateTime;

        public void Process(Vector3 angularVelocity, System.Action<int[], int> playMotors)
        {
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;

            float angularVelY = angularVelocity.y;
            if (Mathf.Abs(angularVelY) <= swayVelocityThreshold) return;

            float intensityVal = Mathf.Clamp01(Mathf.Abs(angularVelY) * swayIntensityMultiplier / 100f);
            int intensity = (int)(intensityVal * 100);

            int[] motors = new int[40];
            if (angularVelY > 0)
            {
                motors[24] = intensity;
                motors[28] = intensity;
                motors[32] = intensity;
            }
            else
            {
                motors[27] = intensity;
                motors[31] = intensity;
                motors[35] = intensity;
            }

            playMotors?.Invoke(motors, 100);
        }
    }

    [System.Serializable]
    public class ImpactHapticMapper
    {
        public bool enabled = true;
        public float collisionIntensityMultiplier = 0.7f;
        public float maxImpactDistance = 1.0f;

        public void Process(float impactForce, Vector3 contactPoint, Vector3 rootPos, System.Action<int[], int> playMotors, System.Func<IEnumerator, Coroutine> startCoroutine)
        {
            float intensityVal = Mathf.Clamp01(impactForce + 1 * collisionIntensityMultiplier);
            int intensity = (int)((intensityVal + 3) * 100);

            float distance = Vector3.Distance(rootPos, contactPoint);
            bool isNearRoot = distance < (maxImpactDistance * 0.3f);

            if (isNearRoot)
            {
                int[] motors = new int[40];
                motors[33] = intensity; motors[34] = intensity;
                motors[37] = intensity; motors[38] = intensity;
                playMotors?.Invoke(motors, 100);

                if (startCoroutine != null)
                {
                    startCoroutine(PlayReverberation(intensity, playMotors));
                }
            }
            else
            {
                int weakIntensity = intensity * 8 / 10;
                int[] motors = new int[40];
                motors[32] = weakIntensity; motors[35] = weakIntensity;
                motors[36] = weakIntensity; motors[39] = weakIntensity;
                playMotors?.Invoke(motors, 200);
            }
        }

        private IEnumerator PlayReverberation(int startIntensity, System.Action<int[], int> playMotors)
        {
            yield return new WaitForSeconds(0.1f);

            int[] motors = new int[40];
            int intensity = startIntensity;
            motors[29] = intensity; motors[30] = intensity;
            playMotors?.Invoke(motors, 100);

            yield return new WaitForSeconds(0.1f);

            motors = new int[40];
            intensity /= 2;
            motors[25] = intensity; motors[26] = intensity;
            playMotors?.Invoke(motors, 100);
        }
    }

    [System.Serializable]
    public class TensionHapticMapper
    {
        public bool enabled = true;

        public void Process(float tension, System.Action<int[], int> playMotors)
        {
            int intensity = (int)(tension * 100);
            int[] motors = new int[40];

            motors[37] = intensity; motors[38] = intensity;
            if (tension > 0.3f) { motors[33] = intensity; motors[34] = intensity; }
            if (tension > 0.6f) { motors[29] = intensity; motors[30] = intensity; }
            if (tension > 0.9f) { motors[25] = intensity; motors[26] = intensity; }

            playMotors?.Invoke(motors, 100);
        }
    }
}
