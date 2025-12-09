using UnityEngine;
using TMPro;

public class BlindTestManager : MonoBehaviour
{
    public static BlindTestManager Instance;

    [Header("Game State")]
    public int score = 0;
    private int currentAnswerKey = 0; // 0: 대기, 1: Root, 2: Tip

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI feedbackText; // "Correct" or "Wrong"
    public TextMeshProUGUI statusText;   // "Phase 1: Visible" or "Phase 2: Blind"

    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        UpdateScoreUI();
        if (feedbackText) feedbackText.text = "Press 'C'";
    }

    void Update()
    {
        // 정답 입력 처리 (1: Root, 2: Tip)
        if (Input.GetKeyDown(KeyCode.Alpha1)) SubmitAnswer(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SubmitAnswer(2);
    }

    // 스포너가 문제를 낼 때 호출
    public void SetNewProblem(int answerKey)
    {
        currentAnswerKey = answerKey;
        if (feedbackText) feedbackText.text = "???"; // 문제 출제 표시
    }

    // 키보드 입력 시 호출
    void SubmitAnswer(int inputKey)
    {
        if (currentAnswerKey == 0) return; // 출제된 문제가 없으면 무시

        if (inputKey == currentAnswerKey)
        {
            score++;
            if (feedbackText) feedbackText.text = "<color=green>Correct!</color>";
            // 필요한 경우 효과음 재생
        }
        else
        {
            if (feedbackText) feedbackText.text = "<color=red>Wrong!</color>";
            // 필요한 경우 오답음 재생
        }

        currentAnswerKey = 0; // 중복 입력 방지 (다음 문제까지 대기)
        UpdateScoreUI();
    }

    // 모드(Visible/Blind) 상태 텍스트 업데이트
    public void UpdateModeText(bool isBlind)
    {
        if (statusText)
            statusText.text = isBlind ? "Phase 2: Blind Mode (Invisible)" : "Phase 1: Learning Mode (Visible)";
    }

    void UpdateScoreUI()
    {
        if (scoreText) scoreText.text = $"Score: {score}";
    }
}