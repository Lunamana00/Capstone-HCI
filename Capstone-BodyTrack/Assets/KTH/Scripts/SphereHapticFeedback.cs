using UnityEngine;
using Bhaptics.SDK2;

public class SphereCollisionHaptics : MonoBehaviour
{
    [Header("Settings")]
    [Range(0, 100)] public int intensity = 80; // 충돌이니 좀 강하게
    [Range(0, 500)] public int duration = 200; // 0.2초 징-
    public string targetTag = "Player"; // 부딪힐 대상의 태그

    private void OnTriggerEnter(Collider other)
    {
        // 1. 부딪힌 게 플레이어인지 확인
        if (other.CompareTag(targetTag))
        {
            // 플레이어의 몸통(Transform) 가져오기
            Transform userTransform = other.transform;

            // 2. 충돌 위치 계산 (플레이어 기준 공의 방향)
            Vector3 direction = transform.position - userTransform.position;
            direction.y = 0; // 높이는 무시

            // 3. 각도 계산 (-180 ~ 180)
            float angle = Vector3.SignedAngle(userTransform.forward, direction, Vector3.up);
            if (angle < 0) angle += 360f;

            // 4. 진동 발사
            TriggerHapticAtAngle(angle, intensity);

            Debug.Log($"충돌 감지! 각도: {angle}");
        }
    }

    // 아까 만든 4열 기준 완벽 매핑 코드 (재사용)
    private void TriggerHapticAtAngle(float angle, int intensity)
    {
        float shiftedAngle = angle + 22.5f;
        if (shiftedAngle >= 360f) shiftedAngle -= 360f;
        int sector = (int)(shiftedAngle / 45f);

        int[] motors = new int[40];

        switch (sector)
        {
            case 0: // Front Center
                SetMotors(motors, new int[] { 1, 2, 5, 6, 9, 10, 13, 14, 17, 18 }, intensity); break;
            case 1: // Front Right
                SetMotors(motors, new int[] { 2, 3, 6, 7, 10, 11, 14, 15, 18, 19 }, intensity); break;
            case 2: // Right Side
                SetMotors(motors, new int[] { 3, 7, 11, 15, 19, 23, 27, 31, 35, 39 }, intensity); break;
            case 3: // Back Right
                SetMotors(motors, new int[] { 22, 23, 26, 27, 30, 31, 34, 35, 38, 39 }, intensity); break;
            case 4: // Back Center
                SetMotors(motors, new int[] { 21, 22, 25, 26, 29, 30, 33, 34, 37, 38 }, intensity); break;
            case 5: // Back Left
                SetMotors(motors, new int[] { 20, 21, 24, 25, 28, 29, 32, 33, 36, 37 }, intensity); break;
            case 6: // Left Side
                SetMotors(motors, new int[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 36 }, intensity); break;
            case 7: // Front Left
                SetMotors(motors, new int[] { 0, 1, 4, 5, 8, 9, 12, 13, 16, 17 }, intensity); break;
        }

        BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, duration);
    }

    private void SetMotors(int[] motors, int[] indices, int intensity)
    {
        foreach (int index in indices)
        {
            if (index >= 0 && index < motors.Length) motors[index] = intensity;
        }
    }
}