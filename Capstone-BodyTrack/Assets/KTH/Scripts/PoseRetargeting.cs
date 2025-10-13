using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

public class PoseRetargeting : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    [SerializeField, Range(0f, 1f)] private float smoothFactor = 0.2f;

    // Bones
    private Transform Hips, Spine, Chest, UpperChest, Neck, Head;
    private Transform LeftUpperArm, LeftLowerArm, LeftHand;
    private Transform RightUpperArm, RightLowerArm, RightHand;
    private Transform LeftUpperLeg, LeftLowerLeg, LeftFoot;
    private Transform RightUpperLeg, RightLowerLeg, RightFoot;

    // Bind pose caches
    private readonly Dictionary<Transform, Quaternion> bindLocalRot = new();
    private readonly Dictionary<Transform, Vector3> bindAimParent = new();
    private readonly Dictionary<Transform, Transform> childOf = new();

    private IReadOnlyList<Landmark> latestWorldLandmarks;
    private readonly object lockObject = new object();

    void Start()
    {
        if (characterAnimator == null)
        {
            Debug.LogError("Animator가 연결되지 않았습니다!");
            enabled = false;
            return;
        }
        InitializeBones();
        CacheBindPose();
    }

    void Update()
    {
        IReadOnlyList<Landmark> toProcess = null;
        lock (lockObject)
        {
            if (latestWorldLandmarks != null)
            {
                toProcess = latestWorldLandmarks;
                latestWorldLandmarks = null;
            }
        }
        if (toProcess != null) UpdatePose(toProcess);
    }

    public void SetLatestLandmarks(IReadOnlyList<Landmark> worldLandmarks)
    {
        lock (lockObject) latestWorldLandmarks = worldLandmarks;
    }

    private void UpdatePose(IReadOnlyList<Landmark> worldLandmarks)
    {
        if (worldLandmarks == null || worldLandmarks.Count < 33) return;

        // Convert MediaPipe world coords -> Unity (flip X only)
        Vector3[] lm = ConvertLandmarks(worldLandmarks);

        // Build stable body frame
        Vector3 hipCenter = (lm[23] + lm[24]) * 0.5f;
        Vector3 shoulderCenter = (lm[11] + lm[12]) * 0.5f;
        Vector3 upW = (shoulderCenter - hipCenter).normalized;
        Vector3 rightW = (lm[12] - lm[11]).normalized;
        if (rightW.sqrMagnitude < 1e-6f) rightW = characterAnimator.transform.right;
        Vector3 fwdW = Vector3.Cross(rightW, upW).normalized; // Unity: forward = cross(right, up)

        // Hips / Chest (use parent-space local rotations)
        AimBoneWorld(Hips, fwdW, upW);
        Transform chestBone = UpperChest != null ? UpperChest : (Chest != null ? Chest : Spine);
        AimBoneWorld(chestBone, fwdW, upW);

        // Arms
        AimBoneDir(RightUpperArm, (lm[14] - lm[12])); // shoulder->elbow
        AimBoneDir(RightLowerArm, (lm[16] - lm[14])); // elbow->wrist
        AimBoneDir(LeftUpperArm, (lm[13] - lm[11]));
        AimBoneDir(LeftLowerArm, (lm[15] - lm[13]));

        // Legs
        AimBoneDir(RightUpperLeg, (lm[26] - lm[24])); // hip->knee
        AimBoneDir(RightLowerLeg, (lm[28] - lm[26])); // knee->ankle
        AimBoneDir(LeftUpperLeg, (lm[25] - lm[23]));
        AimBoneDir(LeftLowerLeg, (lm[27] - lm[25]));

        // Neck / Head (use nose as look dir, keep roll stable with body up)
        if (Head != null)
        {
            Vector3 headDir = (lm[0] - shoulderCenter).normalized;
            if (headDir.sqrMagnitude < 1e-6f) headDir = fwdW;
            AimBoneWorld(Head, headDir, upW);
        }
    }

    // Apply world-space forward/up to a bone as localRotation (respects parent + bind)
    private void AimBoneWorld(Transform bone, Vector3 forwardW, Vector3 upW)
    {
        if (bone == null || bone.parent == null) return;
        if (forwardW.sqrMagnitude < 1e-6f) return;

        Quaternion worldTarget = Quaternion.LookRotation(forwardW, upW);
        // Convert to parent space then re-apply bind rotation
        Quaternion localTarget = Quaternion.Inverse(bone.parent.rotation) * worldTarget;
        Quaternion finalLocal = Quaternion.Slerp(bone.localRotation, localTarget, smoothFactor);
        bone.localRotation = finalLocal;
    }

    // Aim bone so that its local aim (bindAimParent) matches target direction in parent space
    private void AimBoneDir(Transform bone, Vector3 targetDirWorld)
    {
        if (bone == null || bone.parent == null) return;
        if (targetDirWorld.sqrMagnitude < 1e-8f) return;

        Vector3 dirParent = bone.parent.InverseTransformDirection(targetDirWorld.normalized);
        Vector3 bindAim = bindAimParent[bone];
        Quaternion delta = Quaternion.FromToRotation(bindAim, dirParent);
        Quaternion targetLocal = delta * bindLocalRot[bone];
        bone.localRotation = Quaternion.Slerp(bone.localRotation, targetLocal, smoothFactor);
    }

    private Vector3[] ConvertLandmarks(IReadOnlyList<Landmark> w)
    {
        var lm = new Vector3[w.Count];
        for (int i = 0; i < w.Count; i++)
            // X: 좌우 반전(좌표계 변환), Y: 상하 반전, Z: 앞뒤 반전
            lm[i] = new Vector3(-w[i].x, -w[i].y, -w[i].z);
        return lm;
    }

    private void CacheBindPose()
    {
        // Save initial local rotations
        foreach (var t in AllBones())
        {
            if (t == null) continue;
            bindLocalRot[t] = t.localRotation;

            // Pick a child to define the bind aim
            Transform c = null;
            childOf.TryGetValue(t, out c);
            if (c != null && t.parent != null)
            {
                Vector3 dirW = (c.position - t.position);
                if (dirW.sqrMagnitude < 1e-6f) dirW = t.forward;
                bindAimParent[t] = t.parent.InverseTransformDirection(dirW.normalized);
            }
            else
            {
                bindAimParent[t] = Vector3.forward; // fallback
            }
        }
    }

    private IEnumerable<Transform> AllBones()
    {
        yield return Hips; yield return Spine; yield return Chest; yield return UpperChest; yield return Neck; yield return Head;
        yield return LeftUpperArm; yield return LeftLowerArm; yield return LeftHand;
        yield return RightUpperArm; yield return RightLowerArm; yield return RightHand;
        yield return LeftUpperLeg; yield return LeftLowerLeg; yield return LeftFoot;
        yield return RightUpperLeg; yield return RightLowerLeg; yield return RightFoot;
    }

    private void InitializeBones()
    {
        Hips = characterAnimator.GetBoneTransform(HumanBodyBones.Hips);
        Spine = characterAnimator.GetBoneTransform(HumanBodyBones.Spine);
        Chest = characterAnimator.GetBoneTransform(HumanBodyBones.Chest);
        UpperChest = characterAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
        Neck = characterAnimator.GetBoneTransform(HumanBodyBones.Neck);
        Head = characterAnimator.GetBoneTransform(HumanBodyBones.Head);

        LeftUpperArm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        LeftLowerArm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        LeftHand = characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

        RightUpperArm = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        RightLowerArm = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        RightHand = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);

        LeftUpperLeg = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        LeftLowerLeg = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        LeftFoot = characterAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);

        RightUpperLeg = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        RightLowerLeg = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        RightFoot = characterAnimator.GetBoneTransform(HumanBodyBones.RightFoot);

        // Child map for bind aim
        if (Hips) childOf[Hips] = Spine ? Spine : (LeftUpperLeg ? LeftUpperLeg : RightUpperLeg);
        if (Spine) childOf[Spine] = Chest ? Chest : (UpperChest ? UpperChest : Neck);
        if (Chest) childOf[Chest] = UpperChest ? UpperChest : Neck;
        if (UpperChest) childOf[UpperChest] = Neck;
        if (Neck) childOf[Neck] = Head;

        if (LeftUpperArm) childOf[LeftUpperArm] = LeftLowerArm;
        if (LeftLowerArm) childOf[LeftLowerArm] = LeftHand;

        if (RightUpperArm) childOf[RightUpperArm] = RightLowerArm;
        if (RightLowerArm) childOf[RightLowerArm] = RightHand;

        if (LeftUpperLeg) childOf[LeftUpperLeg] = LeftLowerLeg;
        if (LeftLowerLeg) childOf[LeftLowerLeg] = LeftFoot;

        if (RightUpperLeg) childOf[RightUpperLeg] = RightLowerLeg;
        if (RightLowerLeg) childOf[RightLowerLeg] = RightFoot;
    }
}
