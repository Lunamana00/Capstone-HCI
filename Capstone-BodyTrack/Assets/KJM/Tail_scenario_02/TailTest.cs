using UnityEngine;

public class TailHapticsTest : MonoBehaviour
{
    [Header("Target Script")]
    public TailHaptics tailHaptics; // 테스트할 ArmHaptics 스크립트 연결

    [Header("Test Settings")]
    [Range(0f, 10f)] public float testImpactForce = 10.0f; // 가상의 충격량
    public Transform fakeContactPoint_End; // 가상의 손 끝 위치 (테스트용)
    public Transform fakeContactPoint_Root; // 가상의 어깨 위치 (테스트용)


    void Start()    
    {
        // 1. ArmHaptics가 연결 안 되어 있으면 같은 오브젝트에서 찾기
        if (tailHaptics == null)
            tailHaptics = GetComponent<TailHaptics>();

        // 2. 테스트용 위치가 없으면 임시로 현재 위치 설정 (에러 방지)
        if (fakeContactPoint_End == null) fakeContactPoint_End = this.transform;
        if (fakeContactPoint_Root == null) fakeContactPoint_Root = this.transform;
    }

    void Update()
    {
        if (tailHaptics == null) return;

        // [숫자 1] 키: 손/팔뚝 충돌 테스트 (Reverberation 효과)
        // 어깨에서 멀리 떨어진 위치(fakeContactPoint_Hand)를 충돌 지점으로 보냄
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("[Test] End Collision Triggered (Key 1)");
            // 충돌 지점을 꼬리 끝 위치로 설정하여 호출
            tailHaptics.TriggerCollisionFeedback(testImpactForce, fakeContactPoint_End.position);
        }

        // [숫자 2] 키: 어깨 충돌 테스트 (Strong Impact)
        // 어깨와 가까운 위치(fakeContactPoint_Shoulder)를 충돌 지점으로 보냄
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("[Test] Root Collision Triggered (Key 2)");
            // 충돌 지점을 루트 본 위치로 설정하여 호출
            // (ArmHaptics 내부에서 shoulderPoint와의 거리를 계산하여 'isShoulderHit'가 true가 됨)
            tailHaptics.TriggerCollisionFeedback(testImpactForce, fakeContactPoint_Root.position);
        }
    }
}