using UnityEngine;

public class ObstacleBehavior : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("충돌을 감지할 대상의 태그 이름")]
    public string targetTag = "Tail";

    // Collider 충돌 시 실행
    private void OnTriggerEnter(Collider other)
    {
        // 1. Check is this target
        if (other.CompareTag(targetTag))
        {
            // 2. Score up
            if (ExperimentManager.Instance != null)
            {
                ExperimentManager.Instance.RegisterCollision();
            }
            else
            {
                Debug.LogWarning("ExperimentManager가 씬에 없습니다!");
            }
            // 3. Obstacle Destroy
            Destroy(this.gameObject);
        }
    }
}