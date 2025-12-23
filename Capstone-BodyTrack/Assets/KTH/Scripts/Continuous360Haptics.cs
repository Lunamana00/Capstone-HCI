using UnityEngine;
using Bhaptics.SDK2;
using System.Collections;

public class Continuous360Haptics : MonoBehaviour
{
    [Header("References")]
    public string playerTag = "Player";
    public Transform playerTransform;

    [Header("Haptic Settings")]
    [Range(0, 100)] public int baseIntensity = 100;
    [Range(100, 1000)] public int duration = 200; 
    public float hapticInterval = 0.1f; 

    [Header("Tuning")]
    [Range(40, 90)] public float spreadRange = 70f; 
    private float sideThreshold = 50f; 

    [Header("Debug Visuals")]
    public bool showDebugColor = true;
    private Renderer myRenderer;
    private Color originalColor;
    
    private float nextHapticTime = 0f;

    private void Start()
    {
        myRenderer = GetComponent<Renderer>();
        if (myRenderer != null) originalColor = myRenderer.material.color;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null) playerTransform = player.transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsValidCollision(other))
        {
            if (showDebugColor) StartCoroutine(FlashColor());
            ProcessHaptics();
            nextHapticTime = Time.time + hapticInterval;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsValidCollision(other) && Time.time >= nextHapticTime)
        {
            if (showDebugColor) StartCoroutine(FlashColor());
            ProcessHaptics();
            nextHapticTime = Time.time + hapticInterval;
        }
    }

    private bool IsValidCollision(Collider other)
    {
        if (other.CompareTag(playerTag)) return false;
        if (other.CompareTag("Sphere")) return false;
        return true;
    }

    private void ProcessHaptics()
    {
        if (playerTransform == null) return;

        Vector3 direction = transform.position - playerTransform.position;
        direction.y = 0;
        
        float impactAngle = Vector3.SignedAngle(playerTransform.forward, direction, Vector3.up);
        TriggerSmoothHaptics(impactAngle);
    }

    private void TriggerSmoothHaptics(float impactAngle)
    {
        int[] motors = new int[40];
        bool isHit = false;

        bool isFrontImpact = Mathf.Abs(impactAngle) <= 90f;

        for (int col = 0; col < 8; col++)
        {
            bool isFrontColumn = (col < 4);
            float colAngle = GetColumnAngle(col);
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(impactAngle, colAngle));

            if (isFrontImpact != isFrontColumn)
            {
                if (angleDiff > sideThreshold) 
                {
                    continue; 
                }
            }

            if (angleDiff < spreadRange)
            {   
                float strength = 1.0f - (angleDiff / spreadRange);
                int finalIntensity = (int)(baseIntensity * strength);

                if (!isFrontColumn) finalIntensity = (int)(finalIntensity * 1.3f);
                finalIntensity = Mathf.Clamp(finalIntensity, 0, 100);

                if (finalIntensity > 5) 
                {
                    SetColumnMotors(motors, col, finalIntensity);
                    isHit = true;
                }
            }
        }

        if (isHit)
        {
            BhapticsLibrary.PlayMotors((int)PositionType.Vest, motors, duration);
            HapticsDebugBus.NotifyPlayMotors(PositionType.Vest, motors, duration);
        }
    }

    private float GetColumnAngle(int col)
    {
        switch (col)
        {
            case 0: return -67.5f; 
            case 1: return -22.5f; 
            case 2: return 22.5f; 
            case 3: return 67.5f;
            case 4: return 135.0f; 
            case 5: return 165.0f; 
            case 6: return -165.0f; 
            case 7: return -135.0f;
        }
        return 0;
    }

    private void SetColumnMotors(int[] motors, int col, int intensity)
    {
        // bHaptics X40 표준 레이아웃:
        // 앞면 (0-19): Row 0: 0-3, Row 1: 4-7, Row 2: 8-11, Row 3: 12-15, Row 4: 16-19
        // 뒷면 (20-39): Row 0: 20-23, Row 1: 24-27, Row 2: 28-31, Row 3: 32-35, Row 4: 36-39
        
        if (col < 4) // Front (0-19)
        {
            motors[col] = intensity;
            motors[col + 4] = intensity;
            motors[col + 8] = intensity;
        }
        else // Back (20-39)
        {
            int backColIndex = 0;
            if (col == 4) backColIndex = 3;
            else if (col == 5) backColIndex = 2;
            else if (col == 6) backColIndex = 1;
            else if (col == 7) backColIndex = 0;

            motors[20 + backColIndex] = intensity;
            motors[20 + backColIndex + 4] = intensity;
            motors[20 + backColIndex + 8] = intensity;
        }
    }

    IEnumerator FlashColor()
    {
        if (myRenderer != null)
        {
            myRenderer.material.color = Color.magenta;
            yield return new WaitForSeconds(hapticInterval * 0.8f);
            myRenderer.material.color = originalColor;
        }
    }
}