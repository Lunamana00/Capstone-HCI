using UnityEngine;

public class ArmCollision : MonoBehaviour
{
    // Event for Arm Haptics logic
    public delegate void ArmCollisionHandler(float force, Vector3 contactPoint);
    public event ArmCollisionHandler OnArmCollision;

    // Debug Variables
    private Vector3 lastContactPoint;
    private float lastImpactForce;
    private float lastCollisionTime;

    void OnCollisionEnter(Collision collision)
    {
        // 1. 상대 속도를 기반으로 충격량 계산
        float impactForce = collision.relativeVelocity.magnitude;

        // 2. 정확한 충돌 지점 파악
        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        // --- Debug Visualization ---
        lastContactPoint = contactPoint;
        lastImpactForce = impactForce;
        lastCollisionTime = Time.time;
        // 필요 시 주석 해제
        // Debug.Log($"[ArmCollision] Bone: {name}, Force: {impactForce:F2}");
        // ---------------------------

        // 3. 부모(ArmHaptics)에 이벤트 전달
        OnArmCollision?.Invoke(impactForce, contactPoint);
    }

    void OnDrawGizmos()
    {
        // 충돌 지점을 0.5초간 시각화
        if (Time.time - lastCollisionTime < 0.5f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(lastContactPoint, 0.05f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(lastContactPoint, Vector3.up * (lastImpactForce * 0.1f));
        }
    }
}