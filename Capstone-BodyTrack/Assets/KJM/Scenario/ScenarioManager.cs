using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenarioManager : MonoBehaviour
{
    public static ScenarioManager Instance;

    [Header("설정")]
    public string menuSceneName = "Lobby";

    void Awake()
    {
        // 싱글 톤 패턴
        if (Instance != null)
        {
            Destroy(gameObject); 
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 변경 시 파괴 방지
    }

    void Update()
    {
        // ESC 키 입력 감지
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 로비 복귀
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
        Time.timeScale = 1.0f; // 시간 정상화
        SceneManager.LoadScene(menuSceneName);
    }
}