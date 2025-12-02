using UnityEngine;

public class SimpleMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float amplitude = 1.0f; // 최대 이동 높이 (위/아래)
    public float speed = 1.0f;     // 이동 속도

    private Vector3 startPosition;

    void Start()
    {
        // 오브젝트의 시작 위치를 저장합니다.
        startPosition = transform.position;
    }

    void Update()
    {
        // 1. 시간에 따른 사인(Sin) 값을 계산합니다. (값이 -1과 1 사이를 반복)
        // Time.time은 게임이 시작된 이후의 시간을 나타냅니다.
        float newY = Mathf.Sin(Time.time * speed) * amplitude;

        // 2. 시작 위치의 Y값에 계산된 값을 더하여 새로운 위치를 설정합니다.
        transform.position = startPosition + new Vector3(0, newY, 0);
    }
}