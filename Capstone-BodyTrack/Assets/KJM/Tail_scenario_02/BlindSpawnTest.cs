using UnityEngine;
using System.Collections;

public class BlindTestSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject obstaclePrefab;      // 장애물 프리팹
    public TailControllerPhysics tailPhysics; // 꼬리 뼈대 정보 가져오기용

    [Header("Settings")]
    public float interval = 4.0f; // 문제 출제 간격
    public bool isBlindMode = false; // V키로 토글

    // 원하는 회전 각도 (Inspector에서 조절 가능)
    public Vector3 spawnRotation = new Vector3(0, 0, 0);
    public bool alignWithBoneRotation = true; //
    public Vector3 positionOffset = Vector3.zero;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip spawnCueSound; // "삑" 소리

    void Start()
    {
        // 시작 시 모드 텍스트 업데이트
        if (BlindTestManager.Instance != null)
            BlindTestManager.Instance.UpdateModeText(isBlindMode);
    }

    void Update()
    {
        // V키: 투명 모드 토글
        if (Input.GetKeyDown(KeyCode.V))
        {
            isBlindMode = !isBlindMode;
            if (BlindTestManager.Instance != null)
                BlindTestManager.Instance.UpdateModeText(isBlindMode);
        }

        // C키: 테스트 시작
        if (Input.GetKeyDown(KeyCode.C))
        {
            StopAllCoroutines();
            StartCoroutine(SpawnRoutine());
        }
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            if (tailPhysics != null)
            {
                tailPhysics.ResetTailDynamics();
            }

            // 1. Root(1) vs Tip(2) 랜덤 선정 (50% 확률)
            bool isRootTarget = Random.value > 0.5f;

            // Root는 0번 인덱스, Tip은 마지막 인덱스
            int boneIndex = isRootTarget ? 0 : tailPhysics.tailBones.Count - 1;
            int correctAnswer = isRootTarget ? 1 : 2;

            Transform targetBone = tailPhysics.tailBones[boneIndex];

            // 2. 매니저에게 정답 등록 알림
            if (BlindTestManager.Instance != null)
            {
                BlindTestManager.Instance.SetNewProblem(correctAnswer);
            }

            // (옵션) 청각 큐: 자극 시작 알림
            if (audioSource && spawnCueSound) audioSource.PlayOneShot(spawnCueSound);

            yield return new WaitForSeconds(0.5f); // 소리 듣고 약간 뒤에 충돌

            Vector3 finalPosition = targetBone.position;
            //if (isLocalOffset)
            //{
            //    // 로컬 기준: 꼬리가 휘어진 방향을 고려하여 위치 이동 (추천)
            //    // 예: Z축으로 0.1 이동하면 항상 뼈대가 바라보는 앞쪽으로 이동
            //    finalPosition += targetBone.TransformDirection(positionOffset);
            //}
            //else
            {
                // 월드 기준: 꼬리 회전 상관없이 절대 좌표로 이동
                // 예: Y축으로 0.1 이동하면 항상 위쪽으로 이동
                finalPosition += positionOffset;
            }

            // --- 회전 값 결정 로직 ---
            Quaternion finalRotation;
            if (alignWithBoneRotation)
            {
                // 옵션 A: 꼬리 뼈대의 회전 방향을 그대로 따름
                finalRotation = targetBone.rotation * Quaternion.Euler(spawnRotation);
            }
            else
            {
                // 옵션 B: 그냥 설정한 각도로 고정 (예: 90, 0, 0)
                finalRotation = Quaternion.Euler(spawnRotation);
            }

            

            // 3. 장애물 생성 (수정됨: Quaternion.identity -> finalRotation)
            GameObject obs = Instantiate(obstaclePrefab, finalPosition, finalRotation);

            // 4. 투명화 처리 (핵심)
            MeshRenderer[] renderers = obs.GetComponentsInChildren<MeshRenderer>();

            foreach (MeshRenderer mr in renderers)
            {
                mr.enabled = !isBlindMode; // Blind 모드면 모든 렌더러 끄기
            }

            // 5. 물리 설정 (중력 끄기, 제자리에 고정)
            Rigidbody rb = obs.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
            }

            // 6. 삭제 (충돌 후 자동 삭제되지만 안전장치로 1초 뒤 강제 삭제)
            Destroy(obs, 1.0f);

            // 7. 다음 문제까지 대기
            yield return new WaitForSeconds(interval);
        }
    }
}