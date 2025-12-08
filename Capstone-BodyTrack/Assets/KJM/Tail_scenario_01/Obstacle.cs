using UnityEngine;

public class ObstacleBehavior : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("충돌을 감지할 대상의 태그 이름")]
    public string targetTag = "Tail";

    [Tooltip("충돌 후 오브젝트가 파괴되기까지 걸리는 시간 (초)")]
    public float destroyDelay = 2.0f; // 2초 뒤 삭제 (원하는 시간으로 조절 가능)

    private bool isHit = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (isHit) return;

        // 1. Check is this target
        if (collision.gameObject.CompareTag(targetTag))
        {
            isHit = true;

            // 2. Score up
            if (ExperimentManager.Instance != null)
            {
                ExperimentManager.Instance.RegisterCollision();
            }
            else
            {
                Debug.LogWarning("ExperimentManager가 씬에 없습니다!");
            }

            // [선택 사항] 충돌 후 물리적인 충돌은 더 이상 안 일어나게 하려면 아래 주석 해제
            // GetComponent<Collider>().enabled = false; 

            // [선택 사항] 시각적 피드백 (예: 색상 변경, 투명도 조절 등)이 필요하면 이곳에 작성

            // 3. Obstacle Destroy (지연 삭제)
            // 두 번째 인자로 시간을 넘겨주면 그 시간만큼 기다렸다가 삭제됩니다.
            Destroy(this.gameObject, destroyDelay);
        }
    }
}