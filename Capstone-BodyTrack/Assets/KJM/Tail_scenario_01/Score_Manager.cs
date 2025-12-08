using UnityEngine;
using TMPro;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance;

    [Header("Data Recording")]
    public int collisionCount = 0; // 충돌 횟수

    [Header("UI Connecting")]
    public TextMeshProUGUI scoreText;
    void Start()
    {
        UpdateScoreUI();
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 장애물이 충돌했을 때 이 함수를 호출합니다
    public void RegisterCollision()
    {
        collisionCount++;
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score : {collisionCount}";
        }
    }

}