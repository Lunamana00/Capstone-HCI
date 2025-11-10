using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class TailCollisionSender : MonoBehaviour
{
    [Header("IP Settings")]
    [SerializeField]
    private string phoneIpAddress = "192.168.1.10"; // 폰의 Wi-Fi IP

    [Header("Port")]
    [SerializeField]
    private int phonePort = 8081; // 폰 서버 포트 (IMU와 다른 포트 사용)

    private static readonly HttpClient client = new HttpClient();

    // 💡 2. Rigidbody가 다른 Collider와 '충돌'할 때 호출됩니다.
    // (꼬리 뼈 중 하나에 이 스크립트와 Rigidbody가 있어야 함)
    void OnCollisionEnter(Collision collision)
    {
        // 3. 충돌 세기 계산 (속도에 기반)
        float impactForce = collision.relativeVelocity.magnitude;

        Debug.Log($"꼬리 충돌 감지! 세기: {impactForce}");

        // 4. 일정 세기 이상일 때만 폰으로 전송 (비동기)
        if (impactForce > 1.0f) // (임계값 1.0은 조절 필요)
        {
            SendVibrationRequest(impactForce);
        }
    }

    public async void SendVibrationRequest(float force)
    {
        string url = $"http://{phoneIpAddress}:{phonePort}/vibrate";
        string json = $"{{\"force\": {force}}}"; // 간단한 JSON 생성

        try
        {
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Vibration command sent to phone.");
            }
            else
            {
                Debug.LogWarning($"Failed to send. Phone server responded: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Network Error (to Phone): {ex.Message}");
        }
    }

    // 💡 (선택) 폰 IP를 설정하는 public 함수
    public void SetPhoneIP(string ip)
    {
        phoneIpAddress = ip;
    }
}