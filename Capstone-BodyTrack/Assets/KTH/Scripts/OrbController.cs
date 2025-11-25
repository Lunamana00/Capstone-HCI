using UnityEngine;

public class OrbController : MonoBehaviour
{
    [Header("Orbit Settings")]
    public Transform target; // The object to orbit around (e.g., the user's head or body)
    public float orbitDistance = 1.5f;
    public float orbitSpeed = 30f; // Degrees per second
    public float heightOffset = 1.0f;

    [Header("Float Settings")]
    public float floatAmplitude = 0.2f; // How much it bobs up and down
    public float floatFrequency = 1.0f; // Speed of bobbing

    [Header("Physics Settings")]
    public float smoothTime = 0.3f; // Smoothing for movement
    public float repulsionForce = 5f; // Force to push away if too close
    public float minDistance = 0.5f; // Minimum distance allowed

    private Vector3 currentVelocity;
    private float currentAngle;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 1f;
        }

        // Ensure HapticFeedbackSender is present
        if (GetComponent<HapticFeedbackSender>() == null)
        {
            gameObject.AddComponent<HapticFeedbackSender>();
        }

        // Initialize angle based on current position relative to target
        if (target != null)
        {
            Vector3 direction = transform.position - target.position;
            currentAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        }
    }

    void FixedUpdate()
    {
        if (target == null) return;

        // 1. Calculate Orbit Position
        currentAngle += orbitSpeed * Time.fixedDeltaTime;
        float rad = currentAngle * Mathf.Deg2Rad;

        float x = Mathf.Cos(rad) * orbitDistance;
        float z = Mathf.Sin(rad) * orbitDistance;

        // 2. Add Floating (Bobbing) Effect
        float y = heightOffset + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;

        Vector3 targetPosition = target.position + new Vector3(x, y, z);

        // 3. Move towards target position smoothly
        // We use MovePosition for kinematic-like control but allowing physics collisions
        Vector3 smoothedPosition = Vector3.SmoothDamp(rb.position, targetPosition, ref currentVelocity, smoothTime);
        rb.MovePosition(smoothedPosition);

        // 4. Repulsion (Magnetic feel)
        // If the orb gets physically pushed too close to the user (e.g. by a wall), push it back
        float distanceToTarget = Vector3.Distance(rb.position, target.position);
        if (distanceToTarget < minDistance)
        {
            Vector3 pushDir = (rb.position - target.position).normalized;
            rb.AddForce(pushDir * repulsionForce, ForceMode.Acceleration);
        }

        // 5. Rotate to face target (optional, looks nice)
        transform.LookAt(target);
    }
}
