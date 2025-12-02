using UnityEngine;
using System.Collections;

public class PredictableSpawner : MonoBehaviour
{
    [Header("=== Objects ===")]
    [Tooltip("실제 충돌할 장애물 프리팹 (Collider 있음)")]
    public GameObject realObstaclePrefab;

    [Tooltip("생성 위치를 미리 알려줄 예고용 프리팹 (Collider 없음, 반투명 등)")]
    public GameObject warningIndicatorPrefab;

    [Header("=== Locations ===")]
    public Transform[] spawnPoints;

    [Header("=== Timing ===")]
    [Tooltip("예고 표시가 떠 있는 시간 (초)")]
    public float warningDuration = 0.5f;

    [Tooltip("장애물 생성 후 다음 사이클까지의 대기 시간")]
    public float spawnInterval = 2.0f;

    [Tooltip("실제 장애물이 유지되는 시간")]
    public float obstacleLifeTime = 1.0f;

    private bool isSpawning = false;

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    // 껐다 켜거나 할 때 코루틴 제어
    public void StartExperiment()
    {
        isSpawning = true;
        StartCoroutine(SpawnRoutine());
    }

    public void StopExperiment()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    IEnumerator SpawnRoutine()
    {
        isSpawning = true;

        while (isSpawning)
        {
            // 1. 위치 선정 (랜덤)
            if (spawnPoints.Length == 0) yield break;
            Transform targetSpot = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // 2. 예고(Warning) 단계: 반투명 물체 소환
            GameObject warningObj = null;
            if (warningIndicatorPrefab != null)
            {
                warningObj = Instantiate(warningIndicatorPrefab, targetSpot.position, targetSpot.rotation);
            }

            // 예고 시간만큼 대기 (플레이어가 보고 피할 시간)
            yield return new WaitForSeconds(warningDuration);

            // 3. 예고 물체 삭제
            if (warningObj != null) Destroy(warningObj);

            // 4. 실체화(Real Spawn) 단계: 진짜 장애물 소환
            GameObject realObj = Instantiate(realObstaclePrefab, targetSpot.position, targetSpot.rotation);

            // 진짜 장애물은 일정 시간 후 사라짐 (또는 충돌 시 사라짐)
            Destroy(realObj, obstacleLifeTime);

            // 5. 다음 턴까지 대기
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}