using UnityEngine;

public class Armcontroll : MonoBehaviour
{
    [Header("1. 필수 연결 (References)")]
    public Transform ikTarget;      // 움직일 IK 타겟
    public Transform bodyTransform; // 캐릭터 몸통

    [Header("2. 설정 (Settings)")]
    public float reachLength = 2.0f; // 앞으로 뻗을 위치 조정 
    public float reachSpeed = 10f;   // 뻗는 속도
    public KeyCode actionKey = KeyCode.Space; // 팔을 뻗는 키 할당

    [Header("3. 대기 자세 (Idle Offset)")]
    public Vector3 idleOffset = new Vector3(0.3f, -0.5f, 0.3f); // 몸 중심 기준 손의 기본 위치

    [Header("4. 잡기 설정 (Grabbing)")]
    public Transform grabPoint;     // 실제 손 뼈 아래에 있는 GrabPoint
    public float grabRadius = 0.3f;
    public LayerMask grabLayer;

    [Header("5. 조준 설정 (Aiming)")]
    public float aimSensitivity = 0.1f; // 방향키 이동 거리
    public float maxAimOffset = 1.0f;   // X, Y축 최대 조준 이동 범위

    private Transform currentGrabbedObject;
    private Vector3 currentAimOffset = Vector3.zero; // 현재 X-Y 평면 조준 오프셋 (로컬 좌표계)

    void Update()
    {
        // 위치 이동 
        HandleArmMovement();

        // 잡기 로직 (키를 누르는 순간 잡기 시도, 떼면 놓기)
        if (Input.GetKeyDown(actionKey)) TryGrab();
        if (Input.GetKeyUp(actionKey)) Release();
    }

    void HandleArmMovement()
    {
        // 1. 기본 위치 설정 (대기 상태)
        Vector3 targetLocalPos = idleOffset;

        // 2. 방향키 입력을 받아 X-Y 평면 조준 오프셋 업데이트
        float aimX = Input.GetAxis("Horizontal"); // A/D 또는 좌우 방향키
        float aimY = Input.GetAxis("Vertical");   // W/S 또는 상하 방향키

        // 현재 조준 오프셋을 입력에 따라 업데이트
        // X축: 캐릭터의 'Right' 방향 (로컬 X축)
        // Y축: 캐릭터의 'Up' 방향 (로컬 Y축)
        currentAimOffset.x += aimX * aimSensitivity * Time.deltaTime * 50f; // 50f는 반응성 증가를 위한 임의의 계수
        currentAimOffset.y += aimY * aimSensitivity * Time.deltaTime * 50f;

        // 조준 오프셋을 최대 범위로 제한
        currentAimOffset.x = Mathf.Clamp(currentAimOffset.x, -maxAimOffset, maxAimOffset);
        currentAimOffset.y = Mathf.Clamp(currentAimOffset.y, -maxAimOffset, maxAimOffset);


        // 3. 뻗기 기능 적용 (Z축)
        float reachZ = 0f;
        if (Input.GetKey(actionKey))
        {
            // [상태 1: 뻗기] 
            reachZ = reachLength;
        }

        // 4. 모든 오프셋을 합쳐 최종 로컬 위치 계산
        // idleOffset (기본 위치) + currentAimOffset (X, Y 조준) + Z축 뻗기
        targetLocalPos = idleOffset + new Vector3(currentAimOffset.x, currentAimOffset.y, reachZ);


        // 5. 로컬 좌표를 월드 좌표로 변환
        Vector3 finalPosition = bodyTransform.TransformPoint(targetLocalPos);

        // 6. 부드럽게 위치만 이동
        ikTarget.position = Vector3.Lerp(ikTarget.position, finalPosition, Time.deltaTime * reachSpeed);
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