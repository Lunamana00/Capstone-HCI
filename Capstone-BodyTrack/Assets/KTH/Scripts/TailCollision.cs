using UnityEngine;
using System.Collections;

public class TailCollision : MonoBehaviour, ILimbCollisionSource
{
    // Event for Haptics (bHaptics)
    public delegate void TailCollisionHandler(float force, Vector3 contactPoint);
    public event TailCollisionHandler OnTailCollision;
    public event System.Action<float, Vector3> OnLimbCollision;

    [Header("Hit Flash")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private bool autoFindRenderer = true;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);

    // Debug Variables
    private Vector3 lastContactPoint;
    private float lastImpactForce;
    private float lastCollisionTime;
    private Color originalColor = Color.white;
    private string colorProperty;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        CacheRenderer();
    }

    private void OnCollisionEnter(Collision collision)
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

        TriggerFlash();

        // Trigger Local Haptics (bHaptics)
        OnTailCollision?.Invoke(impactForce, contactPoint);
        OnLimbCollision?.Invoke(impactForce, contactPoint);
    }

    private void OnDrawGizmos()
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

    private void CacheRenderer()
    {
        if (targetRenderer == null && autoFindRenderer)
        {
            targetRenderer = GetComponent<Renderer>();
        }
        if (targetRenderer == null && autoFindRenderer)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }
        if (targetRenderer == null) return;

        Material mat = targetRenderer.material;
        if (mat.HasProperty("_BaseColor"))
        {
            colorProperty = "_BaseColor";
        }
        else if (mat.HasProperty("_Color"))
        {
            colorProperty = "_Color";
        }
        if (colorProperty != null)
        {
            originalColor = mat.GetColor(colorProperty);
        }
    }

    private void TriggerFlash()
    {
        if (targetRenderer == null || colorProperty == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        Material mat = targetRenderer.material;
        mat.SetColor(colorProperty, flashColor);
        yield return new WaitForSeconds(flashDuration);
        mat.SetColor(colorProperty, originalColor);
    }
}
