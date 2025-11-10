using UnityEngine;
using System.Collections.Generic;
using System.Collections.Concurrent;

public class TailControllerPhysics : MonoBehaviour
{
    public Transform tailRoot; // ���� �Ѹ� (ù ��° ��) - Rigidbody�� ���� ���
    public List<Transform> tailBones; // ���� ������� ������� ���� ����Ʈ

    [Header("IMU Feedback Settings")]
    public float forceMagnitude = 100f; // IMU ���ӵ��� ��ȯ�Ͽ� ���� Root�� ���� ���� ũ��
    public float torqueMagnitude = 50f; // IMU ���ӵ��� ��ȯ�Ͽ� ���� Root�� ���� ��ũ�� ũ��
    public float damping = 0.5f; // ������ ������ ��鸲�� ���̴� �����
    public Vector3 imuOffset = new Vector3(0, 0, 0); // IMU ���� �⺻ �ڼ� ���� (X, Y, Z ������)
    public ForceMode forceMode = ForceMode.Acceleration; // ������ ���� ���ϴ� ���
    public float gravity = 9.81f;

    private IMUReciever imuReciever; // IMUReciever ��ũ��Ʈ ����
    private Rigidbody rootRigidbody; // ���� �Ѹ��� Rigidbody
    private Quaternion initialRootRotation; // ���� �Ѹ��� �ʱ� ���� ȸ��
    private Quaternion initialParentRotation; // tailRoot�� �θ� Transform�� ���� �� ���� ȸ��

    [Header("Joint Settings (������ ����)")]
    public float jointSpring = 0f; // ����Ʈ�� ������ ���� (�������� �ʱ� �ڼ� ���� ���� ����)
    public float jointDamper = 0f; // ����Ʈ�� ����� (�������� ��鸲 ���� ����)
    public float jointMassScale = 1f; // �� ������ ���� ����

    [Header("Angular Limits (�� ������ ȸ�� ����)")]
    // �� ������ Joint ������Ʈ�� Angular Limits�� ���� �����ǰų�, ��ũ��Ʈ���� �����˴ϴ�.
    // Character Joint�� ����ϸ� �����Ϳ��� ������ ���� �����ϴ� ���� �� �Ϲ����Դϴ�.
    public float swingLimit = 45f; // Y, Z�࿡ ���� ���� ���� (Character Joint)
    public float twistLimitMin = -45f; // X��(Twist)�� �ּ� ���� (Character Joint)
    public float twistLimitMax = 45f; // X��(Twist)�� �ִ� ���� (Character Joint)


    void Start()
    {
        imuReciever = FindObjectOfType<IMUReciever>();
        if (imuReciever == null)
        {
            Debug.LogError("IMUReciever ��ũ��Ʈ�� ã�� �� �����ϴ�. IMU ��� �۵����� �ʽ��ϴ�.");
            enabled = false;
            return;
        }

        if (tailRoot == null)
        {
            Debug.LogError("Tail Root�� �Ҵ���� �ʾҽ��ϴ�. ���� �Ѹ��� �������ּ���.");
            enabled = false;
            return;
        }

        rootRigidbody = tailRoot.GetComponent<Rigidbody>();
        if (rootRigidbody == null)
        {
            Debug.LogError("Tail Root�� Rigidbody ������Ʈ�� �����ϴ�. �߰����ּ���.");
            enabled = false;
            return;
        }

        initialRootRotation = tailRoot.localRotation;
        if (tailRoot.parent != null)
        {
            initialParentRotation = tailRoot.parent.rotation;
        }
        else
        {
            initialParentRotation = Quaternion.identity;
        }


        // ���� ���� ����Ʈ�� ����ִٸ�, tailRoot �Ʒ��� ��� �ڽĵ��� �ڵ����� �߰�
        if (tailBones == null || tailBones.Count == 0)
        {
            tailBones = new List<Transform>();
            AddChildrenToTailBones(tailRoot);
            Debug.Log($"Tail Root �Ʒ� {tailBones.Count}���� ���� ���븦 �ڵ����� ã�ҽ��ϴ�.");
        }

        // ��� ���� ���뿡 Rigidbody�� Joint ����
        SetupPhysicsBones();
    }

    private void AddChildrenToTailBones(Transform parent)
    {
        foreach (Transform child in parent)
        {
            // TailControllerPhysics ��ũ��Ʈ�� �پ��ִ� GameObject�� �ǳʶٴ� ���� �߰�
            if (child.GetComponent<TailControllerPhysics>() == null)
            {
                tailBones.Add(child);
                AddChildrenToTailBones(child); // ��������� �ڽ��� �ڽĵ� �߰�
            }
        }
    }

    void SetupPhysicsBones()
    {
        Rigidbody previousRb = rootRigidbody;

        // Root Rigidbody ����
        rootRigidbody.useGravity = false; // �߷��� IMU ��꿡 �����ϹǷ� Unity �߷��� ��Ȱ��ȭ
        rootRigidbody.linearDamping = damping;
        rootRigidbody.angularDamping = damping;

        for (int i = 0; i < tailBones.Count; i++)
        {
            Transform currentBone = tailBones[i];
            Rigidbody currentRb = currentBone.GetComponent<Rigidbody>();

            if (currentRb == null)
            {
                currentRb = currentBone.gameObject.AddComponent<Rigidbody>();
            }
            currentRb.useGravity = false; // �� ���뿡�� Unity �߷� ��Ȱ��ȭ
            currentRb.linearDamping = damping;
            currentRb.angularDamping = damping;
            currentRb.mass = jointMassScale; // ���� ���� ����

            CharacterJoint joint = currentBone.GetComponent<CharacterJoint>();
            if (joint == null)
            {
                joint = currentBone.gameObject.AddComponent<CharacterJoint>();
            }

            joint.connectedBody = previousRb; // ���� ����(�θ�)�� ����

            // ����Ʈ�� ������ ���� (�ʱ� �ڼ��� ���ư����� ����)
            SoftJointLimitSpring limitSpring = new SoftJointLimitSpring();
            limitSpring.spring = jointSpring;
            limitSpring.damper = jointDamper;
            joint.swingLimitSpring = limitSpring;
            joint.twistLimitSpring = limitSpring;

            // ���� ���� ���� (Character Joint�� �ַ� ���� X���� Twist��, Y/Z���� Swing���� ���)
            SoftJointLimit sLimit = new SoftJointLimit();
            sLimit.limit = swingLimit;
            joint.swing1Limit = sLimit; // Y�� ����
            joint.swing2Limit = sLimit; // Z�� ����

            SoftJointLimit tLimit = new SoftJointLimit();
            tLimit.limit = twistLimitMax;
            joint.lowTwistLimit = tLimit; // X�� Twist �ִ�
            tLimit.limit = twistLimitMin; // X�� Twist �ּ� (����)
            joint.highTwistLimit = tLimit;

            // �θ� Rigidbody ����
            previousRb = currentRb;

            // �浹 ���� �� ���� �ǵ���� ���� Collider�� �ʿ�
            if (currentBone.GetComponent<Collider>() == null)
            {
                // Collider �߰� (��: Capsule Collider)
                CapsuleCollider collider = currentBone.gameObject.AddComponent<CapsuleCollider>();
                collider.direction = 2; // Z�� ���� (���� ���� ����)
                collider.radius = 0.1f;
                collider.height = 1f; // ���� �� ���� ����
                collider.center = new Vector3(0, 0, 0.5f); // ���� ���� ������ Ŀ��
            }
            // TailCollisionSender�� Rigidbody�� Collider�� �־�� �۵��ϹǷ�,
            // �� ��ũ��Ʈ�� �߰��Ͽ� ���� �ǵ���� Ȱ��ȭ�� �� �ֽ��ϴ�.
            if (currentBone.GetComponent<TailCollisionSender>() == null)
            {
                currentBone.gameObject.AddComponent<TailCollisionSender>();
            }
        }
    }


    void FixedUpdate() // ���� ������Ʈ�� FixedUpdate����
    {
        // 1. IMU ���ӵ� ������ ��������
        Vector3 currentAccel = imuReciever.GetLatestAccel();

        // 2. IMU ���ӵ��� �߷��� �̿��� ��ǥ ���� ���
        // ���ӵ��� �����°� �߷��� ���Ͽ� ������ ������ '����'�� ���
        Vector3 targetDirection = (currentAccel * forceMagnitude) + Vector3.down * gravity; // gravity�� �ܺ� ������ �����ؾ� ��

        // IMU Offset�� �����Ͽ� ���� ȸ�� ���
        // (IMUOffset�� Rigidbody�� ���� ��ũ�� ���� �� ���� ������ ���߱� ����)
        Quaternion imuRotationOffset = Quaternion.Euler(imuOffset);

        // ���� Root�� ���� ȸ���� �������� �Ͽ� ��ǥ ���� ȸ���� ���� �������� ��ȯ
        // ��ǥ ���� ���͸� ȸ������ ��ũ�� ���� ������ ���
        Vector3 rotatedTargetDirection = imuRotationOffset * targetDirection.normalized;


        // 3. ���� �Ѹ��� ��(��ũ) ���ϱ�
        // IMU�� �����ӿ� ���� Root Rigidbody�� ��ũ�� ���Ͽ� ȸ���� ����
        // ���� Root�� UP ���͸� ��ǥ �������� ���ߵ��� ��ũ ���
        Vector3 currentUp = rootRigidbody.transform.up;
        Vector3 torqueAxis = Vector3.Cross(currentUp, -rotatedTargetDirection); // ���� ��������(up)�� ��ǥ����(-rotatedTargetDirection)���� ������ ���� ��
        float angle = Vector3.Angle(currentUp, -rotatedTargetDirection);

        if (angle > 0.1f) // ����� ���� ���̰� ���� ���� ��ũ ����
        {
            rootRigidbody.AddTorque(torqueAxis.normalized * angle * torqueMagnitude * Time.fixedDeltaTime, ForceMode.Acceleration);
        }

        // damping�� Rigidbody�� drag/angularDrag�� �̹� �����Ǿ� �ֽ��ϴ�.

        // �߰�������, IMU Offset�� ������ '���� �ڼ�'�� �����Ϸ��� ���� Root�� ���� ���� �ֽ��ϴ�.
        // Quaternion targetWorldRotation = initialParentRotation * initialRootRotation * imuRotationOffset;
        // Quaternion deltaRotation = targetWorldRotation * Quaternion.Inverse(rootRigidbody.rotation);
        // rootRigidbody.AddTorque(new Vector3(deltaRotation.x, deltaRotation.y, deltaRotation.z) * torqueMagnitude, ForceMode.Acceleration);


        // ������ ��ġ ���� (�ɼ�, �ʿ��� ���)
        // rootRigidbody.position = tailRoot.position;
        // rootRigidbody.rotation = Quaternion.Slerp(rootRigidbody.rotation, tailRoot.rotation, followSpeed);
    }

    // �����Ϳ��� tailBones ����Ʈ�� ������� �� tailRoot�� �ڽĵ��� �ڵ����� �߰��ϴ� ��ư
    [ContextMenu("Auto-Populate Tail Bones from Root")]
    void AutoPopulateTailBones()
    {
        if (tailRoot == null)
        {
            Debug.LogError("Tail Root�� �Ҵ���� �ʾҽ��ϴ�. ���� Tail Root�� �Ҵ����ּ���.");
            return;
        }

        tailBones.Clear();
        AddChildrenToTailBones(tailRoot);
        Debug.Log($"Tail Root �Ʒ� {tailBones.Count}���� ���� ���븦 �ڵ����� ã�ҽ��ϴ�.");
        SetupPhysicsBones(); // ���� ���� �����
    }
}