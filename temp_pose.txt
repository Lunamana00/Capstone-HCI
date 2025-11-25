using System.Collections.Generic;
using Landmark = Mediapipe.Tasks.Components.Containers.Landmark;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;

[System.Serializable]
public class FingerTransforms
{
    public Transform Proximal;
    public Transform Intermediate;
    public Transform Distal;
}

[System.Serializable]
public class HandBones
{
    public Transform Wrist;
    public FingerTransforms Thumb;
    public FingerTransforms Index;
    public FingerTransforms Middle;
    public FingerTransforms Ring;
    public FingerTransforms Pinky;
}

public class PoseRetargeting : MonoBehaviour
{
    [SerializeField] private Animator characterAnimator;
    [SerializeField, Range(0f, 1f)] private float bodySmoothFactor = 0.5f;
    [SerializeField, Range(0f, 1f)] private float handSmoothFactor = 0.8f;

    [Header("Hand Retargeting")]
    [SerializeField] private bool retargetHands = true;

    [Header("Arm Control Strategy")]
    [Tooltip("손 데이터가 있을 때 팔뚝 제어 방식")]
    [SerializeField] private ArmControlMode armControlMode = ArmControlMode.PoseOnlyDirection;

    [Header("Coordinate System")]
    [SerializeField] private bool mirrorMode = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool showHandStatus = false;

    public enum ArmControlMode
    {
        PoseOnlyDirection,      // 팔뚝 방향만 포즈 데이터 (추천)
        HandTwistOnly,          // 팔뚝 방향+비틀림 모두 포즈, 손목만 손 데이터
        FullHandControl         // 팔뚝도 손 데이터로 제어 (불안정)
    }

    // Bones
    private Transform Hips, Spine, Chest, UpperChest, Neck, Head;
    private Transform LeftUpperArm, LeftLowerArm, LeftHand;
    private Transform RightUpperArm, RightLowerArm, RightHand;
    private Transform LeftUpperLeg, LeftLowerLeg, LeftFoot;
    private Transform RightUpperLeg, RightLowerLeg, RightFoot;

    private HandBones LeftHandBones;
    private HandBones RightHandBones;

    // Bind pose caches
    private readonly Dictionary<Transform, Quaternion> bindLocalRot = new();
    private readonly Dictionary<Transform, Vector3> bindLocalDir = new();

    // Landmark Caches
    private IReadOnlyList<Landmark> latestPoseLandmarks;
    private IReadOnlyList<Landmarks> latestHandLandmarks;
    private IReadOnlyList<Classifications> latestHandedness;
    private readonly object lockObject = new object();

    private readonly Vector3[] lmPoseCache = new Vector3[33];

    // 손 데이터 상태 추적
    private bool leftHandActive = false;
    private bool rightHandActive = false;
    private Vector3[] leftHandCache = new Vector3[21];
    private Vector3[] rightHandCache = new Vector3[21];

    void Start()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<Animator>();
        }

        if (characterAnimator == null || !characterAnimator.isHuman)
        {
            Debug.LogError("? Humanoid Animator가 필요합니다!");
            enabled = false;
            return;
        }

        InitializeBones();
        InitializeHandBones();
        CacheBindPose();

        Debug.Log($"? Pose Retargeting 초기화 완료\n" +
                  $"   Avatar: {characterAnimator.avatar.name}\n" +
                  $"   Arm Control: {armControlMode}\n" +
                  $"   Mirror Mode: {mirrorMode}");
    }

    void Update()
    {
        IReadOnlyList<Landmark> poseToProcess = null;
        IReadOnlyList<Landmarks> handsToProcess = null;
        IReadOnlyList<Classifications> handednessToProcess = null;

        lock (lockObject)
        {
            if (latestPoseLandmarks != null)
            {
                poseToProcess = latestPoseLandmarks;
                latestPoseLandmarks = null;
            }
            if (latestHandLandmarks != null && latestHandedness != null)
            {
                handsToProcess = latestHandLandmarks;
                handednessToProcess = latestHandedness;
                latestHandLandmarks = null;
                latestHandedness = null;
            }
        }

        // --- [핵심 1] 손 데이터를 먼저 처리하여 캐시 업데이트 ---
        if (handsToProcess != null && handednessToProcess != null && retargetHands)
        {
            ProcessHandData(handsToProcess, handednessToProcess);
        }
        else
        {
            leftHandActive = false;
            rightHandActive = false;
        }

        // --- [핵심 2] 포즈 데이터 처리 (손 상태를 알고 있음) ---
        if (poseToProcess != null)
        {
            UpdateBody(poseToProcess);
        }

        // --- [핵심 3] 손목과 손가락 회전 (팔뚝 회전 이후) ---
        if (leftHandActive || rightHandActive)
        {
            UpdateHandRotations();
        }

        if (showHandStatus)
        {
            Debug.Log($"Hand Status - Left: {leftHandActive}, Right: {rightHandActive}");
        }
    }

    public void SetLatestPoseLandmarks(IReadOnlyList<Landmark> worldLandmarks)
    {
        lock (lockObject) latestPoseLandmarks = worldLandmarks;
    }

    public void SetLatestHandLandmarks(IReadOnlyList<Landmarks> worldHandLandmarks, IReadOnlyList<Classifications> handedness)
    {
        lock (lockObject)
        {
            latestHandLandmarks = worldHandLandmarks;
            latestHandedness = handedness;
        }
    }

    // --- [핵심 함수 1] 손 데이터 전처리 ---
    private void ProcessHandData(IReadOnlyList<Landmarks> hands, IReadOnlyList<Classifications> handedness)
    {
        leftHandActive = false;
        rightHandActive = false;

        for (int i = 0; i < hands.Count; i++)
        {
            var handLMList = hands[i].landmarks;
            if (handLMList == null || handLMList.Count < 21) continue;

            string detectedLabel = "Unknown";
            if (handedness[i].categories != null && handedness[i].categories.Count > 0)
            {
                detectedLabel = handedness[i].categories[0].categoryName;
            }
            if (detectedLabel == "Unknown") continue;

            string actualLabel = mirrorMode ?
                (detectedLabel == "Left" ? "Right" : "Left") :
                detectedLabel;

            Vector3[] targetCache = (actualLabel == "Left") ? leftHandCache : rightHandCache;

            // 손 랜드마크 변환 및 캐싱
            for (int j = 0; j < handLMList.Count; j++)
            {
                var lm = handLMList[j];
                if (mirrorMode)
                {
                    targetCache[j] = new Vector3(lm.x, -lm.y, -lm.z);
                }
                else
                {
                    targetCache[j] = new Vector3(-lm.x, -lm.y, -lm.z);
                }
            }

            if (actualLabel == "Left") leftHandActive = true;
            else rightHandActive = true;
        }
    }

    // --- [핵심 함수 2] 몸체 업데이트 (팔뚝 포함) ---
    private void UpdateBody(IReadOnlyList<Landmark> worldLandmarks)
    {
        if (worldLandmarks == null || worldLandmarks.Count < 33) return;

        // 좌표 변환
        for (int i = 0; i < worldLandmarks.Count; i++)
        {
            var lm = worldLandmarks[i];
            if (mirrorMode)
            {
                lmPoseCache[i] = new Vector3(lm.x, -lm.y, -lm.z);
            }
            else
            {
                lmPoseCache[i] = new Vector3(-lm.x, -lm.y, -lm.z);
            }
        }

        // 인덱스 매핑
        int leftShoulder = mirrorMode ? 12 : 11;
        int rightShoulder = mirrorMode ? 11 : 12;
        int leftElbow = mirrorMode ? 14 : 13;
        int rightElbow = mirrorMode ? 13 : 14;
        int leftWrist = mirrorMode ? 16 : 15;
        int rightWrist = mirrorMode ? 15 : 16;
        int leftHip = mirrorMode ? 24 : 23;
        int rightHip = mirrorMode ? 23 : 24;
        int leftKnee = mirrorMode ? 26 : 25;
        int rightKnee = mirrorMode ? 25 : 26;
        int leftAnkle = mirrorMode ? 28 : 27;
        int rightAnkle = mirrorMode ? 27 : 28;

        Vector3 hipCenter = (lmPoseCache[leftHip] + lmPoseCache[rightHip]) * 0.5f;
        Vector3 shoulderCenter = (lmPoseCache[leftShoulder] + lmPoseCache[rightShoulder]) * 0.5f;

        // 상체 방향
        Vector3 torsoUp = (shoulderCenter - hipCenter).normalized;
        Vector3 shoulderRight = (lmPoseCache[rightShoulder] - lmPoseCache[leftShoulder]).normalized;
        Vector3 torsoForward = Vector3.Cross(shoulderRight, torsoUp).normalized;

        // Spine/Chest 회전
        RotateBoneLookRotation(Spine, torsoForward, torsoUp, bodySmoothFactor);
        RotateBoneLookRotation(Chest, torsoForward, torsoUp, bodySmoothFactor);
        RotateBoneLookRotation(UpperChest, torsoForward, torsoUp, bodySmoothFactor);

        // 상박 (UpperArm) - 항상 포즈 데이터
        RotateBoneToDirection(RightUpperArm, lmPoseCache[rightElbow] - lmPoseCache[rightShoulder], bodySmoothFactor);
        RotateBoneToDirection(LeftUpperArm, lmPoseCache[leftElbow] - lmPoseCache[leftShoulder], bodySmoothFactor);

        // --- [핵심] 팔뚝 (LowerArm) 제어 ---
        UpdateLowerArm(RightLowerArm, rightHandActive, lmPoseCache[rightElbow], lmPoseCache[rightWrist], rightHandCache, "Right");
        UpdateLowerArm(LeftLowerArm, leftHandActive, lmPoseCache[leftElbow], lmPoseCache[leftWrist], leftHandCache, "Left");

        // 다리
        RotateBoneToDirection(RightUpperLeg, lmPoseCache[rightKnee] - lmPoseCache[rightHip], bodySmoothFactor);
        RotateBoneToDirection(RightLowerLeg, lmPoseCache[rightAnkle] - lmPoseCache[rightKnee], bodySmoothFactor);
        RotateBoneToDirection(LeftUpperLeg, lmPoseCache[leftKnee] - lmPoseCache[leftHip], bodySmoothFactor);
        RotateBoneToDirection(LeftLowerLeg, lmPoseCache[leftAnkle] - lmPoseCache[leftKnee], bodySmoothFactor);

        // 머리
        if (Head != null)
        {
            Vector3 nosePos = lmPoseCache[0];
            Vector3 headDir = (nosePos - shoulderCenter).normalized;
            if (headDir.sqrMagnitude > 1e-6f)
            {
                RotateBoneLookRotation(Head, headDir, torsoUp, bodySmoothFactor);
            }
        }
    }

    // --- [핵심 함수 3] 팔뚝 회전 (모드별 분기) ---
    private void UpdateLowerArm(Transform lowerArm, bool handActive, Vector3 elbowPos, Vector3 wristPos, Vector3[] handCache, string side)
    {
        if (lowerArm == null) return;

        Vector3 armDir = (wristPos - elbowPos).normalized;
        if (armDir.sqrMagnitude < 1e-6f) return;

        switch (armControlMode)
        {
            case ArmControlMode.PoseOnlyDirection:
                // 방향만 포즈, 비틀림은 자연스럽게 (가장 안정적)
                RotateBoneToDirection(lowerArm, armDir, bodySmoothFactor);
                break;

            case ArmControlMode.HandTwistOnly:
                // 방향은 포즈, 비틀림은 손 데이터
                if (handActive)
                {
                    Vector3 palmUp = CalculateStablePalmUp(handCache, side);
                    RotateBoneLookRotation(lowerArm, armDir, palmUp, bodySmoothFactor);
                }
                else
                {
                    RotateBoneToDirection(lowerArm, armDir, bodySmoothFactor);
                }
                break;

            case ArmControlMode.FullHandControl:
                // 손 데이터로 전체 제어 (덜 안정적)
                if (handActive)
                {
                    Vector3 palmUp = CalculateStablePalmUp(handCache, side);
                    RotateBoneLookRotation(lowerArm, armDir, palmUp, handSmoothFactor);
                }
                else
                {
                    RotateBoneToDirection(lowerArm, armDir, bodySmoothFactor);
                }
                break;
        }
    }

    // --- [핵심 함수 4] 안정적인 손바닥 Up 벡터 계산 ---
    private Vector3 CalculateStablePalmUp(Vector3[] handLM, string side)
    {
        // 손바닥의 불변 삼각형 사용: 손목(0) - 검지중수골(5) - 새끼중수골(17)
        Vector3 wrist = handLM[0];
        Vector3 indexMCP = handLM[5];
        Vector3 pinkyMCP = handLM[17];

        // 손바닥 평면의 법선 벡터
        Vector3 palmRight = (indexMCP - pinkyMCP).normalized;
        Vector3 palmForward = ((indexMCP + pinkyMCP) * 0.5f - wrist).normalized;
        Vector3 palmUp = Vector3.Cross(palmForward, palmRight).normalized;

        // 왼손은 법선 방향 반전
        if (side == "Left")
        {
            palmUp = -palmUp;
        }

        return palmUp;
    }

    // --- [핵심 함수 5] 손목과 손가락 업데이트 ---
    private void UpdateHandRotations()
    {
        if (leftHandActive && LeftHandBones?.Wrist != null)
        {
            UpdateWristAndFingers(LeftHandBones, leftHandCache, "Left");
        }

        if (rightHandActive && RightHandBones?.Wrist != null)
        {
            UpdateWristAndFingers(RightHandBones, rightHandCache, "Right");
        }
    }

    private void UpdateWristAndFingers(HandBones handBones, Vector3[] handLM, string side)
    {
        // --- 손목 회전 (안정적인 삼각형 기준) ---
        Vector3 wrist = handLM[0];
        Vector3 middleMCP = handLM[9];  // 중지 중수골 (손목 바로 위)
        Vector3 middleTip = handLM[12]; // 중지 끝

        Vector3 handForward = (middleTip - wrist).normalized;

        if (handForward.sqrMagnitude > 1e-6f)
        {
            // 손바닥의 right 벡터 (검지 → 새끼)
            Vector3 indexMCP = handLM[5];
            Vector3 pinkyMCP = handLM[17];
            Vector3 handRight = (indexMCP - pinkyMCP).normalized;

            // 손의 up 벡터
            Vector3 handUp = Vector3.Cross(handRight, handForward).normalized;

            if (side == "Left")
            {
                handUp = -handUp;
            }

            RotateBoneLookRotation(handBones.Wrist, handForward, handUp, handSmoothFactor);
        }

        // --- 손가락 ---
        RotateFinger(handBones.Thumb, handLM, 1, 2, 3, 4);
        RotateFinger(handBones.Index, handLM, 5, 6, 7, 8);
        RotateFinger(handBones.Middle, handLM, 9, 10, 11, 12);
        RotateFinger(handBones.Ring, handLM, 13, 14, 15, 16);
        RotateFinger(handBones.Pinky, handLM, 17, 18, 19, 20);
    }

    private void RotateFinger(FingerTransforms finger, Vector3[] lm, int i0, int i1, int i2, int i3)
    {
        if (finger == null) return;

        if (finger.Proximal != null && (lm[i1] - lm[i0]).sqrMagnitude > 1e-6f)
        {
            RotateBoneToDirection(finger.Proximal, lm[i1] - lm[i0], handSmoothFactor);
        }
        if (finger.Intermediate != null && (lm[i2] - lm[i1]).sqrMagnitude > 1e-6f)
        {
            RotateBoneToDirection(finger.Intermediate, lm[i2] - lm[i1], handSmoothFactor);
        }
        if (finger.Distal != null && (lm[i3] - lm[i2]).sqrMagnitude > 1e-6f)
        {
            RotateBoneToDirection(finger.Distal, lm[i3] - lm[i2], handSmoothFactor);
        }
    }

    // --- 회전 유틸리티 함수 ---
    private void RotateBoneToDirection(Transform bone, Vector3 targetDirWorld, float smooth)
    {
        if (bone == null || bone.parent == null) return;
        if (targetDirWorld.sqrMagnitude < 1e-6f) return;

        targetDirWorld = targetDirWorld.normalized;
        Vector3 targetDirLocal = bone.parent.InverseTransformDirection(targetDirWorld);

        if (!bindLocalDir.TryGetValue(bone, out Vector3 bindDir))
        {
            return;
        }

        Quaternion deltaRotation = Quaternion.FromToRotation(bindDir, targetDirLocal);

        if (!bindLocalRot.TryGetValue(bone, out Quaternion bindRotation))
        {
            bindRotation = Quaternion.identity;
        }

        Quaternion targetLocalRotation = deltaRotation * bindRotation;
        bone.localRotation = Quaternion.Slerp(bone.localRotation, targetLocalRotation, smooth);
    }

    private void RotateBoneLookRotation(Transform bone, Vector3 forward, Vector3 up, float smooth)
    {
        if (bone == null || bone.parent == null) return;
        if (forward.sqrMagnitude < 1e-6f || up.sqrMagnitude < 1e-6f) return;

        Quaternion worldRot = Quaternion.LookRotation(forward, up);
        Quaternion localRot = Quaternion.Inverse(bone.parent.rotation) * worldRot;
        bone.localRotation = Quaternion.Slerp(bone.localRotation, localRot, smooth);
    }

    // --- Bind Pose 캐싱 ---
    private void CacheBindPose()
    {
        CacheBone(Hips);
        CacheBone(Spine);
        CacheBone(Chest);
        CacheBone(UpperChest);
        CacheBone(Neck);
        CacheBone(Head);

        CacheBone(LeftUpperArm);
        CacheBone(LeftLowerArm);
        CacheBone(LeftHand);

        CacheBone(RightUpperArm);
        CacheBone(RightLowerArm);
        CacheBone(RightHand);

        CacheBone(LeftUpperLeg);
        CacheBone(LeftLowerLeg);
        CacheBone(LeftFoot);

        CacheBone(RightUpperLeg);
        CacheBone(RightLowerLeg);
        CacheBone(RightFoot);

        CacheHandBindPose(LeftHandBones);
        CacheHandBindPose(RightHandBones);
    }

    private void CacheBone(Transform bone)
    {
        if (bone == null || bone.parent == null) return;

        bindLocalRot[bone] = bone.localRotation;

        if (bone.childCount > 0)
        {
            Vector3 toChild = (bone.GetChild(0).position - bone.position).normalized;
            bindLocalDir[bone] = bone.parent.InverseTransformDirection(toChild);
        }
        else
        {
            bindLocalDir[bone] = bone.parent.InverseTransformDirection(bone.forward);
        }
    }

    private void CacheHandBindPose(HandBones hand)
    {
        if (hand == null) return;

        CacheBone(hand.Wrist);
        CacheFingerBindPose(hand.Thumb);
        CacheFingerBindPose(hand.Index);
        CacheFingerBindPose(hand.Middle);
        CacheFingerBindPose(hand.Ring);
        CacheFingerBindPose(hand.Pinky);
    }

    private void CacheFingerBindPose(FingerTransforms finger)
    {
        if (finger == null) return;
        CacheBone(finger.Proximal);
        CacheBone(finger.Intermediate);
        CacheBone(finger.Distal);
    }

    // --- 초기화 ---
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
    }

    private void InitializeHandBones()
    {
        LeftHandBones = new HandBones
        {
            Wrist = LeftHand,
            Thumb = CreateFingerTransforms(
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate,
                HumanBodyBones.LeftThumbDistal
            ),
            Index = CreateFingerTransforms(
                HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal
            ),
            Middle = CreateFingerTransforms(
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal
            ),
            Ring = CreateFingerTransforms(
                HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal
            ),
            Pinky = CreateFingerTransforms(
                HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal
            )
        };

        RightHandBones = new HandBones
        {
            Wrist = RightHand,
            Thumb = CreateFingerTransforms(
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate,
                HumanBodyBones.RightThumbDistal
            ),
            Index = CreateFingerTransforms(
                HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal
            ),
            Middle = CreateFingerTransforms(
                HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal
            ),
            Ring = CreateFingerTransforms(
                HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal
            ),
            Pinky = CreateFingerTransforms(
                HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal
            )
        };
    }

    private FingerTransforms CreateFingerTransforms(HumanBodyBones proximal, HumanBodyBones intermediate, HumanBodyBones distal)
    {
        return new FingerTransforms
        {
            Proximal = characterAnimator.GetBoneTransform(proximal),
            Intermediate = characterAnimator.GetBoneTransform(intermediate),
            Distal = characterAnimator.GetBoneTransform(distal)
        };
    }
}
