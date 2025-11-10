using UnityEngine;
using System.Collections.Generic;
using System.Collections.Concurrent; // For IMUReciever data access

public class TailController : MonoBehaviour
{
    public Transform tailRoot; // 꼬리 뿌리 (첫 번째 뼈)
    public List<Transform> tailBones; // 꼬리 뼈대들을 순서대로 담을 리스트

    [Header("IMU Feedback Settings")]
    public float sensitivity = 1.0f; // IMU 데이터에 대한 꼬리의 반응 민감도
    public float followSpeed = 0.1f; // 꼬리가 IMU 움직임을 따라가는 속도 (0.01 ~ 1.0)
    public float gravity = 9.81f; // 꼬리에 가해지는 중력 효과 (Y축)
    public Vector3 imuOffset = new Vector3(0, 0, 0); // IMU 센서 기본 자세 보정 (X, Y, Z 오프셋)

    private IMUReciever imuReciever; // IMUReciever 스크립트 참조
    private Vector3 currentAccel = Vector3.zero; // 현재 IMU 가속도
    private Quaternion[] initialLocalRotations; // 각 뼈의 초기 로컬 회전값 저장
    private Quaternion[] targetLocalRotations; // 각 뼈의 목표 로컬 회전값

    [Header("Joint Rotation Limits (FK)")]
    public bool enableRotationLimits = true;
    public Vector3 maxRotationAngle = new Vector3(45f, 45f, 45f); // 각 축의 최대 회전 각도 (root 기준 상대적)
    public Vector3 minRotationAngle = new Vector3(-45f, -45f, -45f); // 각 축의 최소 회전 각도 (root 기준 상대적)

    // 각 뼈에 대한 개별적인 각도 제한을 설정하고 싶다면 아래 주석을 해제하고 사용
    // public List<Vector3> boneMaxRotationAngles;
    // public List<Vector3> boneMinRotationAngles;

    void Start()
    {
        imuReciever = FindObjectOfType<IMUReciever>();
        if (imuReciever == null)
        {
            Debug.LogError("IMUReciever 스크립트를 찾을 수 없습니다. IMU 제어가 작동하지 않습니다.");
            enabled = false;
            return;
        }

        // 꼬리 뼈대 리스트가 비어있다면, tailRoot 아래의 모든 자식들을 자동으로 추가
        if (tailBones == null || tailBones.Count == 0)
        {
            if (tailRoot == null)
            {
                Debug.LogError("Tail Root가 할당되지 않았습니다. 꼬리 뼈대를 수동으로 할당하거나 tailRoot를 지정해주세요.");
                enabled = false;
                return;
            }

            tailBones = new List<Transform>();
            AddChildrenToTailBones(tailRoot);
            Debug.Log($"Tail Root 아래 {tailBones.Count}개의 꼬리 뼈대를 자동으로 찾았습니다.");
        }

        // 각 뼈의 초기 로컬 회전값 저장
        initialLocalRotations = new Quaternion[tailBones.Count];
        targetLocalRotations = new Quaternion[tailBones.Count];
        for (int i = 0; i < tailBones.Count; i++)
        {
            initialLocalRotations[i] = tailBones[i].localRotation;
            targetLocalRotations[i] = initialLocalRotations[i]; // 초기 목표는 현재 회전
        }
    }

    // tailRoot의 자식들을 재귀적으로 tailBones 리스트에 추가 (self 제외)
    private void AddChildrenToTailBones(Transform parent)
    {
        foreach (Transform child in parent)
        {
            // TailController 스크립트가 붙어있는 GameObject를 건너뛰는 조건 추가
            // (예: Rigidbody나 Collider 등 물리 관련 컴포넌트를 가진 자식만 포함할 수도 있음)
            if (child.GetComponent<TailController>() == null) // 현재 스크립트가 붙은 오브젝트는 건너뛰기
            {
                tailBones.Add(child);
                AddChildrenToTailBones(child); // 재귀적으로 자식의 자식도 추가
            }
        }
    }

    void LateUpdate()
    {
        // 1. IMU 가속도 데이터 가져오기 (메인 스레드에서 접근)
        currentAccel = imuReciever.GetLatestAccel();

        // 2. IMU 가속도와 중력을 이용한 꼬리 뿌리(Root)의 목표 회전 계산
        // IMU 가속도에 오프셋과 중력을 더하여 꼬리가 반응할 '방향'을 계산
        Vector3 targetDirection = (currentAccel * sensitivity) + Vector3.down * gravity;

        // targetDirection이 너무 작으면 꼬리가 불안정해질 수 있으므로 정규화 전에 확인
        if (targetDirection.magnitude < 0.001f)
        {
            targetDirection = Vector3.down; // 기본적으로 아래를 향하게 함
        }
        else
        {
            targetDirection.Normalize();
        }

        // 목표 방향을 기준으로 한 꼬리 뿌리의 회전
        // 기본 꼬리 방향(일반적으로 Z축)을 목표 방향으로 향하게 함
        Quaternion imuTargetRotation = Quaternion.FromToRotation(Vector3.up, -targetDirection);
        // 초기 로컬 회전값과 IMU 회전을 합쳐서 최종 목표 회전 계산 (옵셋 적용)
        Quaternion rootTargetRotation = initialLocalRotations[0] * Quaternion.Euler(imuOffset) * imuTargetRotation;


        // 3. FK 방식으로 꼬리 관절의 회전 적용 및 각도 제한
        for (int i = 0; i < tailBones.Count; i++)
        {
            Transform currentBone = tailBones[i];
            Quaternion currentInitialLocalRotation = initialLocalRotations[i]; // 이 뼈의 초기 로컬 회전

            if (i == 0) // 꼬리의 첫 번째 뼈 (Root)
            {
                // Root는 IMU 데이터를 직접 따름
                targetLocalRotations[i] = Quaternion.Slerp(currentBone.localRotation, rootTargetRotation, followSpeed);
            }
            else // 나머지 자식 뼈대들
            {
                // 부모 뼈대의 회전을 따라감 (FK)
                Transform parentBone = tailBones[i - 1]; // 이전 뼈가 부모 뼈

                // 부모 뼈의 현재 월드 회전에서 이 뼈의 초기 로컬 회전을 기준으로 목표 회전을 계산
                // 이는 각 뼈가 부모에 대해 상대적으로 초기 자세를 유지하려는 경향을 시뮬레이션
                Quaternion parentCurrentWorldRotation = parentBone.rotation;

                // 목표 로컬 회전을 초기 로컬 회전을 기준으로 부드럽게 따라가도록 함
                // 기존의 followSpeed는 Slerp의 t값으로 사용되어 유연성을 제공
                targetLocalRotations[i] = Quaternion.Slerp(currentBone.localRotation, currentInitialLocalRotation, followSpeed);

                // 여기에 각도 제한 적용 (부모의 현재 회전에 대한 상대적인 제한)
                if (enableRotationLimits)
                {
                    // 이 뼈의 현재 로컬 회전을 가져옴 (기준점)
                    Quaternion currentLocal = currentBone.localRotation;

                    // 이 뼈의 목표 로컬 회전을 제한
                    targetLocalRotations[i] = ApplyRotationLimits(targetLocalRotations[i], currentInitialLocalRotation, minRotationAngle, maxRotationAngle);
                    // 만약 뼈마다 다른 각도 제한을 적용하고 싶다면 아래 주석 해제 및 변수 사용
                    // targetLocalRotations[i] = ApplyRotationLimits(targetLocalRotations[i], currentInitialLocalRotation, boneMinRotationAngles[i], boneMaxRotationAngles[i]);
                }
            }
            // 최종 계산된 목표 로컬 회전을 적용
            currentBone.localRotation = targetLocalRotations[i];
        }
    }

    // 지정된 min/max 각도 내에서 로컬 회전을 제한하는 함수
    private Quaternion ApplyRotationLimits(Quaternion targetLocalRotation, Quaternion initialLocalRotation, Vector3 minAngles, Vector3 maxAngles)
    {
        // 목표 로컬 회전을 오일러 각으로 변환
        Vector3 euler = targetLocalRotation.eulerAngles;

        // 오일러 각을 -180 ~ 180 범위로 정규화 (각도 계산의 정확성을 위함)
        euler.x = NormalizeAngle(euler.x);
        euler.y = NormalizeAngle(euler.y);
        euler.z = NormalizeAngle(euler.z);

        // 초기 로컬 회전 기준의 상대적인 각도 계산
        Vector3 initialEuler = initialLocalRotation.eulerAngles;
        initialEuler.x = NormalizeAngle(initialEuler.x);
        initialEuler.y = NormalizeAngle(initialEuler.y);
        initialEuler.z = NormalizeAngle(initialEuler.z);

        // 각 축별 제한 적용
        euler.x = Mathf.Clamp(euler.x, initialEuler.x + minAngles.x, initialEuler.x + maxAngles.x);
        euler.y = Mathf.Clamp(euler.y, initialEuler.y + minAngles.y, initialEuler.y + maxAngles.y);
        euler.z = Mathf.Clamp(euler.z, initialEuler.z + minAngles.z, initialEuler.z + maxAngles.z);

        return Quaternion.Euler(euler);
    }

    // 각도를 -180 ~ 180 범위로 정규화
    private float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    // 에디터에서 tailBones 리스트가 비어있을 때 tailRoot의 자식들을 자동으로 추가하는 버튼
    [ContextMenu("Auto-Populate Tail Bones from Root")]
    void AutoPopulateTailBones()
    {
        if (tailRoot == null)
        {
            Debug.LogError("Tail Root가 할당되지 않았습니다. 먼저 Tail Root를 할당해주세요.");
            return;
        }

        tailBones.Clear();
        AddChildrenToTailBones(tailRoot);
        Debug.Log($"Tail Root 아래 {tailBones.Count}개의 꼬리 뼈대를 자동으로 찾았습니다.");

        // 초기 로컬 회전값 재설정
        initialLocalRotations = new Quaternion[tailBones.Count];
        targetLocalRotations = new Quaternion[tailBones.Count];
        for (int i = 0; i < tailBones.Count; i++)
        {
            initialLocalRotations[i] = tailBones[i].localRotation;
            targetLocalRotations[i] = initialLocalRotations[i];
        }
    }
}