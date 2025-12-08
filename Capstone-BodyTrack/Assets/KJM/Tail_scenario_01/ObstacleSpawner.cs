using UnityEngine;
using System.Collections;

public class PredictableSpawner : MonoBehaviour
{
    [Header("=== Objects ===")]
    [Tooltip("���� �浹�� ��ֹ� ������ (Collider ����)")]
    public GameObject realObstaclePrefab;

    [Tooltip("���� ��ġ�� �̸� �˷��� ������ ������ (Collider ����, ������ ��)")]
    public GameObject warningIndicatorPrefab;

    [Header("=== Locations ===")]
    public Transform[] spawnPoints;

    [Header("=== Timing ===")]
    [Tooltip("���� ǥ�ð� �� �ִ� �ð� (��)")]
    public float warningDuration = 0.5f;

    [Tooltip("��ֹ� ���� �� ���� ����Ŭ������ ��� �ð�")]
    public float spawnInterval = 2.0f;

    [Tooltip("���� ��ֹ��� �����Ǵ� �ð�")]
    public float obstacleLifeTime = 1.0f;

    [Header("=== Movement ===")]
    [Tooltip("��ֹ��� ���ư� �ӵ�")]
    public float projectileSpeed = 10.0f;
    public bool enableShot=false;

    private bool isSpawning = false;



    void Start()
    {
        StartCoroutine(SpawnRoutine());

    }

    // ���� �Ѱų� �� �� �ڷ�ƾ ����
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
            // 1. ��ġ ���� (����)
            if (spawnPoints.Length == 0) yield break;
            Transform targetSpot = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // 2. ����(Warning) �ܰ�: ������ ��ü ��ȯ
            GameObject warningObj = null;
            if (warningIndicatorPrefab != null)
            {
                warningObj = Instantiate(warningIndicatorPrefab, targetSpot.position, targetSpot.rotation);
            }

            // ���� �ð���ŭ ��� (�÷��̾ ���� ���� �ð�)
            yield return new WaitForSeconds(warningDuration);

            // 3. ���� ��ü ����
            if (warningObj != null) Destroy(warningObj);

            // 4. ��üȭ(Real Spawn) �ܰ�: ��¥ ��ֹ� ��ȯ
            GameObject realObj = Instantiate(realObstaclePrefab, targetSpot.position, targetSpot.rotation);

            Rigidbody rb = realObj.GetComponent<Rigidbody>();

            //  Rigidbody 확인
            if (rb == null)
            {
                rb = realObj.AddComponent<Rigidbody>();
                rb.useGravity = false; 
            }

            if (enableShot)
            {
                rb.linearVelocity = -targetSpot.forward * projectileSpeed;
            }
            

            // ��¥ ��ֹ��� ���� �ð� �� ����� (�Ǵ� �浹 �� �����)
            Destroy(realObj, obstacleLifeTime);

            // 5. ���� �ϱ��� ���
            yield return new WaitForSeconds(spawnInterval);
        }
    }
}