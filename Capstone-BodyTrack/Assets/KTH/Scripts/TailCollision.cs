using UnityEngine;

public class TailCollision : MonoBehaviour
{
    // Event for Haptics (bHaptics)
    public delegate void TailCollisionHandler(float force, Vector3 contactPoint);
    public event TailCollisionHandler OnTailCollision;

    // Debug Variables
    private Vector3 lastContactPoint;
    private float lastImpactForce;
    private float lastCollisionTime;

    void OnCollisionEnter(Collision collision)
    {
        // Calculate impact force based on relative velocity
        float impactForce = collision.relativeVelocity.magnitude;

        // Get the first contact point
        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        // --- Debug Visualization ---
        lastContactPoint = contactPoint;
        lastImpactForce = impactForce;
        lastCollisionTime = Time.time;
        Debug.Log($"[TailCollision] Bone: {name}, Force: {impactForce:F2}, Point: {contactPoint}");
        // ---------------------------

        // Trigger Local Haptics (bHaptics)
        OnTailCollision?.Invoke(impactForce, contactPoint);
    }

    void OnDrawGizmos()
    {
        // Draw collision for 0.5 seconds
        if (Time.time - lastCollisionTime < 0.5f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lastContactPoint, 0.05f); // Impact Point
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(lastContactPoint, Vector3.up * (lastImpactForce * 0.1f)); // Force indicator (Upward for visibility)
        }
    }
}