using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenarioManager : MonoBehaviour
{
    // 어디서든 접근 가능하게 만드는 정적 변수 (싱글톤)
    public static ScenarioManager Instance;

    [Header("설정")]
    public string menuSceneName = "Lobby";

    void Awake()
    {
        // 1. 이미 매니저가 존재한다면? (중복 방지)
        if (Instance != null)
        {
            Destroy(gameObject); // 새로 생긴 녀석을 파괴
            return;
        }

        // 2. 내가 유일한 매니저라면?
        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬이 바뀔 때 나를 파괴하지 마라!
    }

    void Update()
    {
        // ESC 키 입력 감지 (어떤 씬에 있든 작동함)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 현재 씬이 로비가 아닐 때만 로비로 복귀
            if (SceneManager.GetActiveScene().name != menuSceneName)
            {
                ReturnToMenu();
            }
        }
    }

    public void LoadScenario(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void ReturnToMenu()
    {
        Debug.Log("로비로 복귀");
        Time.timeScale = 1.0f; // 혹시 멈춰있었다면 시간 정상화
        SceneManager.LoadScene(menuSceneName);
    }
}