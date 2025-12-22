using UnityEngine;
using System.Collections;

public class PredictableSpawner : MonoBehaviour
{
    [Header("=== Objects ===")]
    [Tooltip("Obstacle Prefab")]
    public GameObject realObstaclePrefab;

    [Tooltip("Warning Prefab")]
    public GameObject warningIndicatorPrefab;

    [Header("=== Locations ===")]
    [Tooltip("Obstacle sapwn point")]
    public Transform[] spawnPoints;

    [Tooltip("Warning spawn point")]
    public Transform[] warningLocations; 

    [Header("=== Timing ===")]
    [Tooltip("Warning Duration")]
    public float warningDuration = 0.5f;

    [Tooltip("Spawn Interval")]
    public float spawnInterval = 2.0f;

    [Tooltip("Obstacle Life TIme")]
    public float obstacleLifeTime = 1.0f;

    [Header("=== Movement ===")]
    [Tooltip("Shooting Speed")]
    public float projectileSpeed = 10.0f;
    public bool enableShot = false;

    private bool isSpawning = false;

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    public void StartExperiment()
    {
        if (!isSpawning)
        {
            isSpawning = true;
            StartCoroutine(SpawnRoutine());
        }
    }

    public void StopExperiment()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    IEnumerator SpawnRoutine()
    {
        // 시작 시 잠시 대기 (선택 사항)
        yield return new WaitForSeconds(1.0f);

        isSpawning = true;

        while (isSpawning)
        {
            // 위치 선정
            if (spawnPoints.Length == 0) yield break;

            // 위치 랜덤 결정
            int targetIndex = Random.Range(0, spawnPoints.Length);

            Transform targetSpot = spawnPoints[targetIndex];     // 실제 발사 위치
            Transform warningSpot = targetSpot;                  // 초기 설정 실제 = 경고 위치

            // 경고 위치 설정 시 교체
            if (warningLocations != null && warningLocations.Length > targetIndex && warningLocations[targetIndex] != null)
            {
                warningSpot = warningLocations[targetIndex];
            }

            // Warning Spawn
            GameObject warningObj = null;
            if (warningIndicatorPrefab != null)
            {
                // 설정된 warningSpot 위치에 생성
                warningObj = Instantiate(warningIndicatorPrefab, warningSpot.position, warningSpot.rotation);
            }

            // 경고 시간만큼 대기
            yield return new WaitForSeconds(warningDuration);

            // 경고 객체 제거
            if (warningObj != null) Destroy(warningObj);

            // Obstacle 스폰 후 발사
            GameObject realObj = Instantiate(realObstaclePrefab, targetSpot.position, targetSpot.rotation);

            Rigidbody rb = realObj.GetComponent<Rigidbody>();

            // Rigidbody 확인 및 추가
            if (rb == null)
            {
                rb = realObj.AddComponent<Rigidbody>();
                rb.useGravity = false;
            }

            if (enableShot)
            {
                rb.linearVelocity = -targetSpot.forward * projectileSpeed;
            }

            // Obstacle 자동 파괴
            Destroy(realObj, obstacleLifeTime);

            yield return new WaitForSeconds(spawnInterval);
        }
    }
}