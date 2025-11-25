using System.Collections;
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

    [Header("Calibration & Offsets")]
    [Tooltip("Automatically force T-Pose (Bind Pose) at start to calibrate.")]
    [SerializeField] private bool autoCalibrate = true;
    
    [Tooltip("Additional rotation for wrists to fix 90-degree twists.")]
    [SerializeField] private Vector3 wristRotationOffset = new Vector3(90, 0, 0);

    [Tooltip("Additional rotation for arms.")]
    [SerializeField] private Vector3 armRotationOffset = Vector3.zero;

    [Header("Natural Walking (3D Movement)")]
    [Tooltip("X: Horizontal Sensitivity, Y: Vertical Scale Multiplier, Z: Unused (Calculated via Depth)")]
    [SerializeField] private Vector3 movementScale = new Vector3(1.5f, 1.0f, 1.0f);
    
    [Tooltip("Estimated distance from camera in meters. Used for Z-axis calculation.")]
    [SerializeField] private float estimatedCameraDistance = 2.5f;
    
    [SerializeField] private float hipHeightOffset = 0.0f;

    [Tooltip("Forces legs to be straighter to compensate for high camera angles.")]
    [SerializeField, Range(0f, 1f)] private float legStraighteningFactor = 0.5f;

    [Header("Coordinate System")]
    [SerializeField] private bool mirrorMode = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool showHandStatus = false;

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
    private readonly Dictionary<Transform, Vector3> bindLocalDir = new(); // Direction to child in local space
    
    // Calibration Data
    private float initialHipHeightUnity;
    private float initialHipToFootDistMP = -1f;
    private float initialShoulderWidthMP = -1f;
    private Vector3 initialHipCenterMP;
    private float verticalScale = 1.0f;

    // Landmark Caches
    private IReadOnlyList<Landmark> latestPoseLandmarks;
    private IReadOnlyList<Landmarks> latestHandLandmarks;
    private IReadOnlyList<Classifications> latestHandedness;
    private readonly object lockObject = new object();

    private readonly Vector3[] lmPoseCache = new Vector3[33];

    // Hand State
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
            Debug.LogError("Humanoid Animator is required!");
            enabled = false;
            return;
        }

        InitializeBones();
        InitializeHandBones();

        if (autoCalibrate)
        {
            StartCoroutine(AutoCalibrateRoutine());
        }
        else
        {
            CacheBindPose();
            if (Hips != null) initialHipHeightUnity = Hips.position.y;
        }
    }

    private IEnumerator AutoCalibrateRoutine()
    {
        // Force Bind Pose (T-Pose)
        characterAnimator.Rebind();
        yield return new WaitForEndOfFrame();

        CacheBindPose();
        if (Hips != null) initialHipHeightUnity = Hips.position.y;
        
        Debug.Log("Auto-Calibration Complete: Bind Pose Cached.");
        
        // Animator will naturally resume animation next frame
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

        // 1. Process Hand Data
        if (handsToProcess != null && handednessToProcess != null && retargetHands)
        {
            ProcessHandData(handsToProcess, handednessToProcess);
        }
        else
        {
            leftHandActive = false;
            rightHandActive = false;
        }

        // 2. Process Body Pose
        if (poseToProcess != null)
        {
            UpdateBody(poseToProcess);
        }

        // 3. Process Fingers
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

    // --- Data Processing ---
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

            for (int j = 0; j < handLMList.Count; j++)
            {
                var lm = handLMList[j];
                // Convert MediaPipe coords to Unity
                if (mirrorMode)
                    targetCache[j] = new Vector3(lm.x, -lm.y, -lm.z);
                else
                    targetCache[j] = new Vector3(-lm.x, -lm.y, -lm.z);
            }

            if (actualLabel == "Left") leftHandActive = true;
            else rightHandActive = true;
        }
    }

    // --- Body Retargeting ---
    private void UpdateBody(IReadOnlyList<Landmark> worldLandmarks)
    {
        if (worldLandmarks == null || worldLandmarks.Count < 33) return;

        // Convert Landmarks
        for (int i = 0; i < worldLandmarks.Count; i++)
        {
            var lm = worldLandmarks[i];
            if (mirrorMode)
                lmPoseCache[i] = new Vector3(lm.x, -lm.y, -lm.z);
            else
                lmPoseCache[i] = new Vector3(-lm.x, -lm.y, -lm.z);
        }

        // Indices
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

        // --- Natural Walking & Grounding ---
        if (Hips != null)
        {
            // 1. Calculate Shoulder Width (for Depth Estimation)
            float currentShoulderWidth = Vector3.Distance(lmPoseCache[leftShoulder], lmPoseCache[rightShoulder]);
            
            // 2. Lowest Foot (for Grounding)
            float leftFootY = lmPoseCache[leftAnkle].y;
            float rightFootY = lmPoseCache[rightAnkle].y;
            float minFootY = Mathf.Min(leftFootY, rightFootY);
            float currentHipToFootDistMP = hipCenter.y - minFootY;

            // 3. Auto-Calibrate on first valid frame
            if (initialShoulderWidthMP < 0 && currentShoulderWidth > 0.001f && currentHipToFootDistMP > 0.001f)
            {
                initialShoulderWidthMP = currentShoulderWidth;
                initialHipToFootDistMP = currentHipToFootDistMP;
                initialHipCenterMP = hipCenter;
                
                // Calculate vertical scale
                verticalScale = initialHipHeightUnity / initialHipToFootDistMP;
                
                Debug.Log($"Auto-Calibrated: Scale={verticalScale}, ShoulderWidth={initialShoulderWidthMP}");
            }

            if (initialShoulderWidthMP > 0)
            {
                // --- Depth Calculation (Z) ---
                // Ratio > 1 means closer (bigger), Ratio < 1 means further (smaller)
                float depthScale = currentShoulderWidth / initialShoulderWidthMP;
                
                // Perspective Z Mapping: Z = Dist * (1/Scale - 1)
                // If Scale=2 (Closer), Z = 2.5 * (0.5 - 1) = -1.25 (Moves forward)
                float zPos = estimatedCameraDistance * (1.0f / depthScale - 1.0f);

                // --- Horizontal Calculation (X) ---
                float xPos = (hipCenter.x - initialHipCenterMP.x) * movementScale.x;

                // --- Vertical Calculation (Y) with Normalization ---
                // Normalize height by depth scale to remove "walking closer = taller" effect
                float normalizedHipHeight = currentHipToFootDistMP / depthScale;
                float targetY = normalizedHipHeight * verticalScale * movementScale.y + hipHeightOffset;

                // Apply Position
                Vector3 targetPos = new Vector3(xPos, targetY, zPos);
                Hips.position = Vector3.Lerp(Hips.position, targetPos, bodySmoothFactor);
            }
        }

        // Body Orientation
        Vector3 torsoUp = (shoulderCenter - hipCenter).normalized;
        Vector3 shoulderRight = (lmPoseCache[rightShoulder] - lmPoseCache[leftShoulder]).normalized;
        Vector3 torsoForward = Vector3.Cross(shoulderRight, torsoUp).normalized;

        // Rotate Hips (Root)
        // We rotate Hips to match the torso orientation. 
        // This ensures the whole body turns, not just the spine.
        if (Hips != null)
        {
             RotateBoneLookRotation(Hips, torsoForward, torsoUp, bodySmoothFactor);
        }

        // Spine Rotation
        RotateBoneLookRotation(Spine, torsoForward, torsoUp, bodySmoothFactor);
        RotateBoneLookRotation(Chest, torsoForward, torsoUp, bodySmoothFactor);
        RotateBoneLookRotation(UpperChest, torsoForward, torsoUp, bodySmoothFactor);

        // Arms (Twist Corrected)
        UpdateLimb(RightUpperArm, RightLowerArm, lmPoseCache[rightShoulder], lmPoseCache[rightElbow], lmPoseCache[rightWrist], -shoulderRight, armRotationOffset);
        UpdateLimb(LeftUpperArm, LeftLowerArm, lmPoseCache[leftShoulder], lmPoseCache[leftElbow], lmPoseCache[leftWrist], shoulderRight, armRotationOffset);

        // Legs (with Straightening)
        // We blend the MP direction with a "Straight Down" vector relative to the Hips
        Vector3 hipDown = -torsoUp; // Approximate down relative to body

        UpdateLeg(RightUpperLeg, lmPoseCache[rightKnee] - lmPoseCache[rightHip], hipDown);
        UpdateLeg(RightLowerLeg, lmPoseCache[rightAnkle] - lmPoseCache[rightKnee], hipDown);
        UpdateLeg(LeftUpperLeg, lmPoseCache[leftKnee] - lmPoseCache[leftHip], hipDown);
        UpdateLeg(LeftLowerLeg, lmPoseCache[leftAnkle] - lmPoseCache[leftKnee], hipDown);
        // Head
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

    private void UpdateLeg(Transform bone, Vector3 mpDir, Vector3 straightDir)
    {
        if (bone == null) return;
        
        // Blend between MediaPipe direction and Straight Down
        Vector3 finalDir = Vector3.Lerp(mpDir.normalized, straightDir.normalized, legStraighteningFactor);
        
        RotateBoneToDirection(bone, finalDir, bodySmoothFactor);
    }

    // --- Limb Update (Twist Corrected) ---
    private void UpdateLimb(Transform upper, Transform lower, Vector3 root, Vector3 mid, Vector3 tip, Vector3 referenceRight, Vector3 offset)
    {
        if (upper == null || lower == null) return;

        // 1. Upper Arm
        Vector3 upperDir = (mid - root).normalized;
        // Calculate Plane Normal (Root-Mid-Tip) to determine twist
        Vector3 armPlaneNormal = Vector3.Cross(upperDir, (tip - mid).normalized).normalized;
        // If arm is straight, use reference right/forward
        if (armPlaneNormal.sqrMagnitude < 0.01f) armPlaneNormal = Vector3.Cross(upperDir, Vector3.up);

        // Apply Rotation
        RotateBoneToDirection(upper, upperDir, bodySmoothFactor, offset);

        // 2. Lower Arm
        Vector3 lowerDir = (tip - mid).normalized;
        RotateBoneToDirection(lower, lowerDir, bodySmoothFactor, offset);
    }

    // --- Hand Update ---
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
        // Wrist Rotation
        Vector3 wrist = handLM[0];
        Vector3 middleMCP = handLM[9];
        Vector3 middleTip = handLM[12];
        Vector3 indexMCP = handLM[5];
        Vector3 pinkyMCP = handLM[17];

        Vector3 handForward = (middleTip - wrist).normalized;
        Vector3 handRight = (indexMCP - pinkyMCP).normalized;
        Vector3 handUp = Vector3.Cross(handRight, handForward).normalized;

        if (side == "Left") handUp = -handUp;

        // Apply Wrist Rotation with Offset
        if (handForward.sqrMagnitude > 1e-6f)
        {
            RotateBoneLookRotation(handBones.Wrist, handForward, handUp, handSmoothFactor, wristRotationOffset);
        }

        // Fingers
        RotateFinger(handBones.Thumb, handLM, 1, 2, 3, 4);
        RotateFinger(handBones.Index, handLM, 5, 6, 7, 8);
        RotateFinger(handBones.Middle, handLM, 9, 10, 11, 12);
        RotateFinger(handBones.Ring, handLM, 13, 14, 15, 16);
        RotateFinger(handBones.Pinky, handLM, 17, 18, 19, 20);
    }

    private void RotateFinger(FingerTransforms finger, Vector3[] lm, int i0, int i1, int i2, int i3)
    {
        if (finger == null) return;
        if (finger.Proximal != null) RotateBoneToDirection(finger.Proximal, lm[i1] - lm[i0], handSmoothFactor);
        if (finger.Intermediate != null) RotateBoneToDirection(finger.Intermediate, lm[i2] - lm[i1], handSmoothFactor);
        if (finger.Distal != null) RotateBoneToDirection(finger.Distal, lm[i3] - lm[i2], handSmoothFactor);
    }

    // --- Core Rotation Logic ---
    private void RotateBoneToDirection(Transform bone, Vector3 targetDirWorld, float smooth, Vector3 additionalOffset = default)
    {
        if (bone == null || bone.parent == null) return;
        if (targetDirWorld.sqrMagnitude < 1e-6f) return;

        targetDirWorld = targetDirWorld.normalized;
        Vector3 targetDirLocal = bone.parent.InverseTransformDirection(targetDirWorld);

        if (!bindLocalDir.TryGetValue(bone, out Vector3 bindDir)) return;

        Quaternion deltaRotation = Quaternion.FromToRotation(bindDir, targetDirLocal);
        
        if (!bindLocalRot.TryGetValue(bone, out Quaternion bindRotation)) bindRotation = Quaternion.identity;

        // Apply Offset
        Quaternion offsetRot = Quaternion.Euler(additionalOffset);
        Quaternion targetLocalRotation = deltaRotation * bindRotation * offsetRot;

        bone.localRotation = Quaternion.Slerp(bone.localRotation, targetLocalRotation, smooth);
    }

    private void RotateBoneLookRotation(Transform bone, Vector3 forward, Vector3 up, float smooth, Vector3 additionalOffset = default)
    {
        if (bone == null || bone.parent == null) return;
        if (forward.sqrMagnitude < 1e-6f || up.sqrMagnitude < 1e-6f) return;

        Quaternion worldRot = Quaternion.LookRotation(forward, up);
        Quaternion localRot = Quaternion.Inverse(bone.parent.rotation) * worldRot;
        
        // Apply Offset
        Quaternion offsetRot = Quaternion.Euler(additionalOffset);
        localRot *= offsetRot;

        bone.localRotation = Quaternion.Slerp(bone.localRotation, localRot, smooth);
    }

    // --- Bind Pose Cache ---
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
            // Fallback for leaf bones
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

    // --- Initialization ---
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
            Thumb = CreateFingerTransforms(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal),
            Index = CreateFingerTransforms(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal),
            Middle = CreateFingerTransforms(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal),
            Ring = CreateFingerTransforms(HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal),
            Pinky = CreateFingerTransforms(HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal)
        };

        RightHandBones = new HandBones
        {
            Wrist = RightHand,
            Thumb = CreateFingerTransforms(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal),
            Index = CreateFingerTransforms(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal),
            Middle = CreateFingerTransforms(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal),
            Ring = CreateFingerTransforms(HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal),
            Pinky = CreateFingerTransforms(HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal)
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
