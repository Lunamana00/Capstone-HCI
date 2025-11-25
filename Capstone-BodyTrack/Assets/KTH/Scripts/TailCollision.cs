using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class TailCollision : MonoBehaviour
{
    [Header("IP Settings")]
    [SerializeField]
    private string phoneIpAddress = "192.168.1.10"; // Phone Wi-Fi IP

    [Header("Port")]
    [SerializeField]
    private int phonePort = 8081; // Phone Server Port

    private static readonly HttpClient client = new HttpClient();

    // Event for Haptics (bHaptics)
    public delegate void TailCollisionHandler(float force, Vector3 contactPoint);
    public event TailCollisionHandler OnTailCollision;

    void OnCollisionEnter(Collision collision)
    {
        // Calculate impact force based on relative velocity
        float impactForce = collision.relativeVelocity.magnitude;

        // 1. Send to Phone (Throttled or Threshold check)
        if (impactForce > 1.0f)
        {
            SendVibrationRequest(impactForce);
        }

        // 2. Trigger Local Haptics (bHaptics)
        // Get the first contact point
        Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        OnTailCollision?.Invoke(impactForce, contactPoint);
    }

    private async void SendVibrationRequest(float force)
    {
        string url = $"http://{phoneIpAddress}:{phonePort}/vibrate";
        string json = $"{{\"force\": {force}}}";

        try
        {
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            // Fire and forget (async)
            HttpResponseMessage response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogWarning($"Failed to send vibration to phone. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Network Error (to Phone): {ex.Message}");
        }
    }

    public void SetPhoneIP(string ip)
    {
        phoneIpAddress = ip;
    }
}