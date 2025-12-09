using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public struct ForceProfile
{
    public string profileName;
    public float speed;      // 날아가는 속도
    public float mass;       // 물체의 무게 (충격량에 영향)
    public float scale;      // 크기
    public Color color;      // 시각적 구분
}

public class VariableForceSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject obstaclePrefab;
    public Transform targetPlayer; // 꼬리 쪽을 향해 발사

    [Header("Force Profiles")]
    public List<ForceProfile> profiles; // 인스펙터에서 Weak, Normal, Strong 설정

    [Header("Spawn Settings")]
    public float spawnInterval = 3.0f;
    public float spawnDistance = 5.0f;

    void Start()
    {
        // 기본 프로필이 없으면 예제 데이터 추가 (안전장치)
        if (profiles == null || profiles.Count == 0)
        {
            profiles = new List<ForceProfile>
            {
                new ForceProfile { profileName="Weak", speed=5f, mass=0.5f, scale=0.2f, color=Color.green },
                new ForceProfile { profileName="Strong", speed=15f, mass=5f, scale=0.5f, color=Color.red }
            };
        }

        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            // 1. 랜덤 프로필 선택
            ForceProfile selectedProfile = profiles[Random.Range(0, profiles.Count)];

            // 2. 위치 선정 (플레이어 정면 부채꼴 범위 내 랜덤)
            float randomAngle = Random.Range(135f, -135f);
            Quaternion rot = Quaternion.Euler(0, randomAngle, 0);
            Vector3 spawnDir = rot * -targetPlayer.forward; // 플레이어 앞쪽에서
            Vector3 spawnPos = targetPlayer.position + (spawnDir * spawnDistance);
            spawnPos.y = targetPlayer.position.y; // 높이 맞춤

            // 3. 생성
            GameObject obs = Instantiate(obstaclePrefab, spawnPos, Quaternion.identity);

            // 4. 프로필 적용 (핵심 로직)
            ApplyProfile(obs, selectedProfile);

            // 5. 발사 (플레이어를 향해)
            Rigidbody rb = obs.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 directionToPlayer = (targetPlayer.position - spawnPos).normalized;
                rb.linearVelocity = directionToPlayer * selectedProfile.speed; // Unity 6 (구버전은 .velocity)
            }

            // 6. 5초 후 삭제
            Destroy(obs, 5.0f);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void ApplyProfile(GameObject obj, ForceProfile profile)
    {
        // A. 크기 변경
        obj.transform.localScale = Vector3.one * profile.scale;

        // B. 색상 변경
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = profile.color;
        }

        // C. 질량 변경 (중요: 충돌 시 꼬리가 밀리는 정도 결정)
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = profile.mass;
        }

        Debug.Log($"Spawned [ {profile.profileName} ] Force Shot!");
    }
}