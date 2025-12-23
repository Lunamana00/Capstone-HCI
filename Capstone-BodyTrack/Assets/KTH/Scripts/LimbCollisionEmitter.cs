using System;
using UnityEngine;

public class LimbCollisionEmitter : MonoBehaviour, ILimbCollisionSource
{
    public event Action<float, Vector3> OnLimbCollision;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private Vector3 lastContactPoint;
    private float lastImpactForce;
    private float lastCollisionTime;

    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;
        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        lastContactPoint = contactPoint;
        lastImpactForce = impactForce;
        lastCollisionTime = Time.time;

        if (debugLog)
        {
            Debug.Log($"[LimbCollisionEmitter] Bone: {name}, Force: {impactForce:F2}, Point: {contactPoint}");
        }

        OnLimbCollision?.Invoke(impactForce, contactPoint);
    }

    private void OnDrawGizmos()
    {
        if (Time.time - lastCollisionTime < 0.5f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lastContactPoint, 0.05f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(lastContactPoint, Vector3.up * (lastImpactForce * 0.1f));
        }
    }
}
