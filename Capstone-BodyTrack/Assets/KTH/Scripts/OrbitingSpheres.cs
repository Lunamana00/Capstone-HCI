using UnityEngine;
using System.Collections.Generic;

public class OrbitingSpheres : MonoBehaviour
{
    [Header("Orbit Settings")]
    public Transform target; // The center object (User)
    public int sphereCount = 3;
    public float orbitDistance = 1.5f;
    public float orbitSpeed = 50f; // Degrees per second
    public float heightOffset = 1.0f;

    [Header("Visuals")]
    public GameObject spherePrefab; // Optional: Assign a prefab
    public float sphereSize = 0.3f;
    public Color[] sphereColors = { Color.red, Color.green, Color.blue };

    [Header("Public Access")]
    public List<Transform> spheres = new List<Transform>();

    private float currentAngle = 0f;

    void Start()
    {
        if (target == null) target = this.transform;

        // Create spheres if not assigned
        if (spheres.Count == 0)
        {
            CreateSpheres();
        }
    }

    void CreateSpheres()
    {
        for (int i = 0; i < sphereCount; i++)
        {
            GameObject sphere = spherePrefab != null 
                ? Instantiate(spherePrefab) 
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            
            sphere.name = $"OrbitSphere_{i}";
            sphere.transform.localScale = Vector3.one * sphereSize;
            
            // --- [수정된 부분 시작] ---
            // 1. 방금 만든 공에서 진동 스크립트를 가져옵니다.
            Continuous360Haptics haptics = sphere.GetComponent<Continuous360Haptics>();
            
            // 2. 스크립트가 있다면, 내가 알고 있는 타겟(Y Bot)을 직접 넣어줍니다.
            if (haptics != null)
            {
                haptics.playerTransform = this.target; // OrbitingSpheres의 target이 바로 Y Bot입니다.
            }
            // --- [수정된 부분 끝] ---

            // Remove collider logic (if needed)...
            // Color logic...

            spheres.Add(sphere.transform);
        }
    }
    void Update()
    {
        if (target == null) return;

        currentAngle += orbitSpeed * Time.deltaTime;
        if (currentAngle >= 360f) currentAngle -= 360f;

        float angleStep = 360f / spheres.Count;

        for (int i = 0; i < spheres.Count; i++)
        {
            if (spheres[i] == null) continue;

            float angle = currentAngle + (angleStep * i);
            float rad = angle * Mathf.Deg2Rad;

            float x = Mathf.Cos(rad) * orbitDistance;
            float z = Mathf.Sin(rad) * orbitDistance;

            Vector3 offset = new Vector3(x, heightOffset, z);
            spheres[i].position = target.position + offset;
        }
    }
}
