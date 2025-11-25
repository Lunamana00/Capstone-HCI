using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class HapticFeedbackSender : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private string phoneIpAddress = "192.168.1.10";
    [SerializeField] private int phonePort = 8081;
    [SerializeField] private float requestThrottleSeconds = 0.1f; // Max 10 requests per second

    [Header("Visual Feedback")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color impactColor = Color.red;
    [SerializeField] private Color contactColor = Color.yellow;
    [SerializeField] private float flashDuration = 0.2f;

    private static readonly HttpClient client = new HttpClient();
    private float lastRequestTime;
    private Color originalColor;
    private Material targetMaterial;

    private void Start()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer != null)
        {
            targetMaterial = targetRenderer.material;
            originalColor = targetMaterial.color;
        }
    }

    // Called when the object hits something hard
    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce > 0.5f)
        {
            SendVibrationRequest(impactForce, "impact");
            FlashColor(impactColor);
        }
    }

    // Called when the object is resting on or brushing against something
    private void OnCollisionStay(Collision collision)
    {
        // Send a weaker, throttled vibration for continuous contact
        SendVibrationRequest(0.2f, "contact");
        // Optional: Sustain color or flash yellow? Let's just flash yellow occasionally or blend.
        // For now, let's just flash yellow if enough time has passed to avoid epilepsy risks
        if (Time.time - lastRequestTime > requestThrottleSeconds)
        {
             FlashColor(contactColor);
        }
    }

    public async void SendVibrationRequest(float force, string type)
    {
        // Throttle requests
        if (Time.time - lastRequestTime < requestThrottleSeconds) return;
        lastRequestTime = Time.time;

        string url = $"http://{phoneIpAddress}:{phonePort}/vibrate";
        // JSON payload with force and type (impact vs contact)
        string json = $"{{\"force\": {force}, \"type\": \"{type}\"}}";

        try
        {
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            // Fire and forget (don't await strictly if we don't care about response body)
            _ = client.PostAsync(url, content); 
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send haptic request: {ex.Message}");
        }
    }

    private void FlashColor(Color color)
    {
        if (targetMaterial == null) return;

        // Simple coroutine-like behavior or just set and tween back? 
        // For simplicity in this script, let's use a coroutine.
        StopAllCoroutines();
        StartCoroutine(FlashRoutine(color));
    }

    private System.Collections.IEnumerator FlashRoutine(Color flashColor)
    {
        if (targetMaterial.HasProperty("_EmissionColor"))
        {
             targetMaterial.SetColor("_EmissionColor", flashColor);
             targetMaterial.EnableKeyword("_EMISSION");
        }
        targetMaterial.color = flashColor;
        
        yield return new WaitForSeconds(flashDuration);

        targetMaterial.color = originalColor;
        if (targetMaterial.HasProperty("_EmissionColor"))
        {
            targetMaterial.SetColor("_EmissionColor", Color.black);
        }
    }

    public void SetPhoneIP(string ip)
    {
        phoneIpAddress = ip;
    }
}
