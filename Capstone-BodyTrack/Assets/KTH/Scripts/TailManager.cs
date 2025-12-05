using UnityEngine;
using System.Collections.Generic;

public class TailManager : MonoBehaviour
{
    [Header("설정")]
    public bool autoConfigureOnStart = true; // 켜두면 시작할 때 자동 세팅됨

    [Header("물리 세팅값 (자식 뼈들)")]
    public float boneDrag = 2.0f; // 공기 저항 (높을수록 덜 흔들림)
    public float jointLimit = 40f; // 꺾이는 최대 각도

    [Header("제어 세팅값 (Joint1)")]
    public float rotateSpeed = 10f;
    public float swayAmount = 10f;

    void Start()
    {
        if (autoConfigureOnStart)
        {
            SetupTailStructure();
        }
    }

    [ContextMenu("지금 수동으로 세팅하기")] // 컴포넌트 우클릭 메뉴로도 실행 가능
    public void SetupTailStructure()
    {
        // 1. Joint1 찾기 (Tail 바로 아래 첫 번째 자식)
        if (transform.childCount == 0)
        {
            Debug.LogError("Tail 아래에 Joint1이 없습니다!");
            return;
        }

        Transform joint1 = transform.GetChild(0);

        // 2. Joint1 세팅 (모터)
        SetupActiveRoot(joint1);

        // 3. 나머지 뼈들 세팅 (밧줄) - 재귀적으로 탐색
        // Joint1의 자식부터 시작
        if (joint1.childCount > 0)
        {
            SetupPassiveChain(joint1.GetChild(0), joint1.GetComponent<Rigidbody>());
        }

        Debug.Log("꼬리 세팅 완료! (Gravity Off, Drag 적용됨)");
    }

    // --- 내부 로직 ---

    // 1단계: Joint1 (능동 제어 모터) 세팅
    void SetupActiveRoot(Transform rootBone)
    {
        // Rigidbody 추가 및 설정
        Rigidbody rb = rootBone.GetComponent<Rigidbody>();
        if (rb == null) rb = rootBone.gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;   // 중력 끄기
        rb.isKinematic = true;   // 물리 엔진 무시 (스크립트로 돌릴 거니까)
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // 제어 스크립트(TailMotor) 추가
        TailMotor motor = rootBone.GetComponent<TailMotor>();
        if (motor == null) motor = rootBone.gameObject.AddComponent<TailMotor>();

        // 매니저의 설정값을 모터에 전달
        motor.rotationSpeed = rotateSpeed;
        motor.swayAngle = swayAmount;
    }

    // 2단계: 나머지 뼈들 (수동 물리) 세팅
    void SetupPassiveChain(Transform currentBone, Rigidbody parentRb)
    {
        // A. Rigidbody (물리)
        Rigidbody rb = currentBone.GetComponent<Rigidbody>();
        if (rb == null) rb = currentBone.gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;    // 중력 끄기 (우주 유영 느낌)
        rb.isKinematic = false;   // 물리 적용 (펄럭거려야 함)
        rb.linearDamping = boneDrag;    // 공기 저항
        rb.angularDamping = 0.5f; // 회전 저항
        rb.mass = 0.5f;           // 가볍게

        // B. Collider (충돌체 - 없으면 관절이 이상해질 수 있음)
        if (currentBone.GetComponent<Collider>() == null)
        {
            CapsuleCollider col = currentBone.gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2; // Z-axis
            col.radius = 0.05f;
            col.height = 0.5f;
        }

        // C. CharacterJoint (관절)
        CharacterJoint joint = currentBone.GetComponent<CharacterJoint>();
        if (joint == null) joint = currentBone.gameObject.AddComponent<CharacterJoint>();

        joint.connectedBody = parentRb; // 부모 뼈와 연결
        joint.autoConfigureConnectedAnchor = true;

        // 관절 제한 (너무 꺾이지 않게)
        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = jointLimit; // 40도 정도
        joint.swing1Limit = limit;
        joint.swing2Limit = limit;
        joint.lowTwistLimit = limit;
        joint.highTwistLimit = limit;

        // 다음 자식이 있으면 계속 진행 (재귀)
        if (currentBone.childCount > 0)
        {
            SetupPassiveChain(currentBone.GetChild(0), rb);
        }
    }
}