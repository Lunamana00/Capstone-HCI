using UnityEngine;

public class TailMotor : MonoBehaviour
{
    [Header("Control Settings")]
    public float rotationSpeed = 10f;
    public float swaySpeed = 2f;
    public float swayAngle = 10f;
    
    [Header("Debug")]
    public bool useManualInput = false;
    [Range(-1f, 1f)] public float inputX;
    [Range(-1f, 1f)] public float inputY;

    [SerializeField] private MonoBehaviour imuProviderBehaviour;
    private IImuInputProvider imuProvider;
    private Rigidbody rb;
    private Quaternion defaultLocalRotation;

    void Start()
    {
        if (imuProviderBehaviour != null)
        {
            imuProvider = imuProviderBehaviour as IImuInputProvider;
        }
        if (imuProvider == null)
        {
            imuProvider = FindObjectOfType<IMUReciever>();
        }
        rb = GetComponent<Rigidbody>();
        defaultLocalRotation = transform.localRotation;
    }

    void FixedUpdate()
    {
        // 1. 입력 값 가져오기
        float targetX = 0f;
        float targetY = 0f;

        if (useManualInput)
        {
            targetX = inputX;
            targetY = inputY;
        }
        else if (imuProvider != null)
        {
            Vector3 accel = imuProvider.GetLatestAccel();
            // 센서 방향에 따라 x, z 매핑은 조절 필요
            targetX = Mathf.Clamp(accel.x, -1f, 1f); 
            targetY = Mathf.Clamp(accel.z, -1f, 1f); 
        }

        // 2. Sway (살랑거림) 더하기
        float time = Time.time * swaySpeed;
        float swayH = Mathf.Sin(time) * swayAngle;
        float swayV = Mathf.Cos(time * 0.8f) * (swayAngle * 0.5f);

        // 3. 목표 회전 계산 (입력 + Sway)
        // Joint1은 로컬 회전만 제어하면 됨
        float yaw = (targetX * 60f) + swayH;   // 좌우
        float pitch = (targetY * 60f) + swayV; // 상하

        Quaternion targetRot = defaultLocalRotation * Quaternion.Euler(pitch, yaw, 0f);

        // 4. 적용 (MoveRotation으로 부드럽게)
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, transform.parent.rotation * targetRot, Time.fixedDeltaTime * rotationSpeed));
    }
}