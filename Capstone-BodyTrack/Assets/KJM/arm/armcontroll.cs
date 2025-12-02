using UnityEngine;

public class KeyboardArmReach : MonoBehaviour
{
    [Header("1. 필수 연결 (References)")]
    public Transform ikTarget;      // 움직일 IK 타겟 (RightArm_IK > IK_Target)
    public Transform bodyTransform; // 캐릭터 몸통 (방향 기준)

    [Header("2. 설정 (Settings)")]
    public float reachLength = 2.0f; // 팔을 뻗을 거리 (Z축 추가량)
    public float reachSpeed = 10f;   // 뻗는 속도
    public KeyCode actionKey = KeyCode.Space; // 누를 키

    [Header("3. 대기 자세 (Idle Offset)")]
    public Vector3 idleOffset = new Vector3(0.3f, -0.5f, 0.3f); // 몸 중심 기준 손의 기본 위치

    [Header("4. 잡기 설정 (Grabbing)")]
    public Transform grabPoint;     // 실제 손 뼈 아래에 있는 GrabPoint
    public float grabRadius = 0.3f;
    public LayerMask grabLayer;

    private Transform currentGrabbedObject;

    void Update()
    {
        // A. 타겟 위치 계산 및 이동 (회전 X, 위치만 이동)
        HandleArmMovement();

        // B. 잡기 로직 (키를 누르는 순간 잡기 시도, 떼면 놓기)
        if (Input.GetKeyDown(actionKey)) TryGrab();
        if (Input.GetKeyUp(actionKey)) Release();
    }

    void HandleArmMovement()
    {
        // 1. 기본 로컬 위치 설정 (대기 상태)
        Vector3 targetLocalPos = idleOffset;

        // 2. 키 입력 시 Z축 값만 더하기
        if (Input.GetKey(actionKey))
        {
            // [상태 1: 뻗기] 
            // 기존 idleOffset의 Z값에 reachLength를 더함
            targetLocalPos.z += reachLength;
        }

        // 3. 로컬 좌표를 월드 좌표로 변환
        // (캐릭터가 회전하면 손 위치도 같이 회전되어 계산됨 -> 로컬 기준 Z축 전진)
        Vector3 finalPosition = bodyTransform.TransformPoint(targetLocalPos);

        // 4. 부드럽게 위치만 이동 (회전 코드는 삭제됨)
        ikTarget.position = Vector3.Lerp(ikTarget.position, finalPosition, Time.deltaTime * reachSpeed);

        // *중요*: ikTarget.rotation 부분은 요청하신 대로 완전히 제거했습니다.
        // IK 타겟의 회전값은 에디터에서 설정해둔 상태 그대로 유지됩니다.
    }

    void TryGrab()
    {
        if (currentGrabbedObject != null) return;

        // 손 위치(GrabPoint) 주변 감지
        Collider[] cols = Physics.OverlapSphere(grabPoint.position, grabRadius, grabLayer);
        if (cols.Length > 0)
        {
            Grab(cols[0].transform);
        }
    }

    void Grab(Transform item)
    {
        currentGrabbedObject = item;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        item.SetParent(grabPoint);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
    }

    void Release()
    {
        if (currentGrabbedObject == null) return;

        currentGrabbedObject.SetParent(null);

        Rigidbody rb = currentGrabbedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            // 놓을 때 앞으로 살짝 던지기
            rb.AddForce(bodyTransform.forward * 5f, ForceMode.Impulse);
        }

        currentGrabbedObject = null;
    }

    // 디버그용 기즈모 그리기
    void OnDrawGizmos()
    {
        if (bodyTransform != null)
        {
            // 대기 위치 (파란구)
            Gizmos.color = Color.blue;
            Vector3 idlePos = bodyTransform.TransformPoint(idleOffset);
            Gizmos.DrawWireSphere(idlePos, 0.1f);

            // 뻗었을 때 위치 (빨간구)
            Gizmos.color = Color.red;
            // idleOffset에서 z만 더한 좌표를 월드로 변환
            Vector3 reachLocal = idleOffset;
            reachLocal.z += reachLength;
            Vector3 reachPos = bodyTransform.TransformPoint(reachLocal);

            Gizmos.DrawLine(idlePos, reachPos);
            Gizmos.DrawWireSphere(reachPos, 0.1f);
        }
    }
}