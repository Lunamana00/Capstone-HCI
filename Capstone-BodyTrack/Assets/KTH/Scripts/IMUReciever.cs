using UnityEngine;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

// 1. JSON 데이터 구조에 맞춘 C# 클래스
[System.Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class SensorData
{
    public long timestamp;
    public Vector3Data accelerometer;
    public Vector3Data gyroscope;
}

public class IMUReciever : MonoBehaviour, IImuInputProvider
{
    private HttpListener httpListener;
    private CancellationTokenSource cts;
    private readonly object lockObject = new object();

    // 2. 최신 센서 값을 저장할 변수 (Volatile은 멀티스레드용)
    private Vector3 latestAccel = Vector3.zero;
    private Vector3 latestGyro = Vector3.zero;

    private int requestCounter = 0; // 데이터 수신 확인용   

    // 3. 다른 스크립트가 안전하게 데이터를 가져갈 수 있는 public 메서드
    public Vector3 GetLatestAccel()
    {
        lock (lockObject) return latestAccel;
    }
    public Vector3 GetLatestGyro()
    {
        lock (lockObject) return latestGyro;
    }

    void Start()
    {
        // 4. HttpListener 시작 (포트: 8080. Flutter 앱과 일치해야 함)
        cts = new CancellationTokenSource();
        httpListener = new HttpListener();
        httpListener.Prefixes.Add("http://+:928/"); // 모든 IP의 8080 포트에서 수신
        httpListener.Start();

        // 5. 비동기 Task로 리스너 실행 (메인 스레드 차단 방지)
        Task.Run(() => Listen(cts.Token));

        Debug.Log($"IP adress 확인");
        Debug.Log($"PC IP: {GetLocalIPAddress()}");
        Debug.Log($"Flutter 앱에서 http://{GetLocalIPAddress()}:928/ 로 데이터를 보내세요.");
    }


    private async Task Listen(CancellationToken token)
    {
        while (httpListener.IsListening && !token.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext context = await httpListener.GetContextAsync();
                ProcessRequest(context);
            }
            catch (HttpListenerException) when (token.IsCancellationRequested)
            {
                // 리스너가 중지될 때 발생하는 예외는 무시
            }
            catch (Exception ex)
            {
                Debug.LogError($"Receiver Error: {ex.Message}");
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        string requestBody;
        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
        {
            requestBody = reader.ReadToEnd();
        }

        // 6. JSON 파싱 및 데이터 업데이트
        try
        {
            Debug.Log("request 시작");
            SensorData data = JsonUtility.FromJson<SensorData>(requestBody);

            // Unity 좌표계에 맞게 변환 (Flutter 앱과 동일하게 X/Z축 반전)
            Vector3 accel = new Vector3(-data.accelerometer.x, data.accelerometer.y, -data.accelerometer.z);
            Vector3 gyro = new Vector3(-data.gyroscope.x, data.gyroscope.y, -data.gyroscope.z);
            //Debug.Log(accel);
            //Debug.Log(gyro);
            lock (lockObject)
            {
                latestAccel = accel;
                latestGyro = gyro;
                requestCounter++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON Parse Error: {ex.Message} \nRaw: {requestBody}");
        }

        // 7. 폰(클라이언트)에 응답
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Close();
    }

    // 8. 앱 종료 시 리스너 종료
    void OnDestroy()
    {
        cts?.Cancel();
        if (httpListener != null && httpListener.IsListening)
        {
            httpListener.Stop();
            httpListener.Close();
        }
        cts?.Dispose();
    }

    // 9. PC의 로컬 IP 주소를 찾는 헬퍼 함수
    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
}