using MotoSquid.Bike;
using MotoSquid.Controls;
using MotoSquid.DevTools;
using MotoSquid.Settings;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Rendering.HighDefinition;

namespace MotoSquid.Cameras
{
public class CameraController : MonoBehaviour
{
    public CinemachineCamera[] cameras;
    [SerializeField] private BikeController bikeController;
    [Header("Speed Shake")]
    public float speedThresholdForShake = 50f;
    public float shakeAmplitude = 1.2f;
    public float shakeFrequency = 3.0f;
    public float shakeSpeedExponent = 2f;
    [SerializeField] private InputActionAsset inputActions;
    // -1 = all devices 0+ = specific gamepad only
    [SerializeField] private int  playerDeviceIndex = -1;
    [SerializeField] private bool lockToKeyboard    = false;
    [SerializeField] private bool allowKeyboard      = false;
    private InputAction _switchCamera;
    private InputAction _look;
    private InputAction _lookBehind;

    [Header("Look Behind Cameras")]
    public CinemachineCamera[] lookBehindCameras;

    bool _isLookingBehind;

    // Read by BikeAnimationController so the rider poses a look back over her shoulder
    public bool IsLookingBehind => _isLookingBehind;
    [HideInInspector] public bool lookEnabled = false;

    [Header("Camera Look")]
    public bool[] cameraIsFirstPerson;
    public float mouseLookSensitivity   = 0.15f;
    public float gamepadLookSensitivity = 120f;
    public float lookYawClamp           = 45f;
    public float lookPitchClamp         = 16f;
    public float lookPitchSensitivity   = 0.6f;
    public bool  invertLookY            = false;
    public float lookRecentreSpeed      = 2f;
    public float lookRecentreDelay      = 1.5f;

    float _lookYaw;
    float _lookPitch;
    float _lastLookTime = -999f;

    public float minFOV = 60f;
    public float maxFOV = 80f;
    public float accessibilityFovBias = 0f;
    public float fovSmoother = 5f;
    public float fovSpeedExponent = 1.5f;
    public float fovStartSpeed = 0f;
    public float fovFullSpeed = 0f;

    [Header("Gear FOV Build Up")]
    public float gearFOVBuild     = 3f;
    // High exponent keeps the pull-back in the last sliver of the rev range, so it reads as a
    // brief punch at the shift rather than a swell across the whole gear
    public float gearFOVExponent  = 6f;
    public float gearFOVReleaseTime = 0.07f;
    float m_GearFOVOffset, m_GearFOVVel;
    public Transform ragdollFollowTarget;
    public Transform ragdollLookAtTarget;

    [Header("Ground Rush")]
    public float groundRushYDrop     = 0.6f;
    public float groundRushFullSpeed = 60f;
    public float groundRushSmoothTime = 0.35f;

    private CinemachineBasicMultiChannelPerlin[] cameraNoise;
    private CinemachineFollow[]              cameraFollows;
    private int  currentCameraIndex = 0;
    private bool isShaking          = false;

    Transform[] initialCameraFollowTargets;
    Transform[] initialCameraLookAtTargets;
    Transform[] initialLookBehindFollowTargets;
    Transform[] initialLookBehindLookAtTargets;
    Vector3[]   baseFollowOffsets;

    float m_GroundRushT;
    float m_GroundRushVel;
    float smoothFOV      = 60f;
    float m_BoostFOV;
    float m_BoostFOVVel;

    // Gear shift shake
    int   m_LastGear;
    float m_GearShakeTimer;
    [Header("Gear Shift Shake")]
    public float gearShakeAmplitude = 0.8f;
    public float gearShakeDuration  = 0.16f;
    public float gearShakeFrequency = 8f;
    public float gearShakeSpeedScale = 1.5f;

    [Header("Per-Camera Shake Scale")]
    public float[] cameraShakeScale;

    [Header("Near Miss")]
    public NearMissFX nearMissFX;

    [Header("Boost FOV")]
    public BoostSystem boostSystem;
    public float boostFOVDelta      = 25f;
    public float boostFOVPushTime   = 0.4f;
    public float boostFOVReleaseTime = 0.6f;

    [Header("Dutch Roll")]
    public bool  dutchRollEnabled        = true;
    public float dutchRollStrength       = 1f;
    public float dutchRollPerLeanDegree  = 0.15f;
    public float maxDutch                = 6f;
    public float dutchSmooth             = 6f;
    float m_Dutch;

    [Header("Apex Look")]
    public bool  apexLookEnabled = true;
    public float apexLookYaw     = 14f;   // Max yaw toward the apex at full steer (degrees)
    public float apexLookSmooth  = 4f;    // How quickly the head turn eases in/out
    private CinemachinePanTilt[] cameraPanTilts;
    float m_ApexYaw;       // Current smoothed apex look yaw (degrees)
    float m_ApexYawPrev;   // Last frame's value, for additive POV injection in first person

    [Header("Split Screen")]
    [SerializeField] private Camera physicalCamera;
    [SerializeField] private LayerMask cameraVolumeLayerMask = ~0;
    [SerializeField] private int[] allPlayerFXLayers = { 16, 17 };

    private void OnEnable()
    {
        _switchCamera?.Enable();
        _look?.Enable();
        _lookBehind?.Enable();
    }

    private void OnDisable()
    {
        _switchCamera?.Disable();
        _look?.Disable();
        _lookBehind?.Disable();
    }

    float CurrentShakeScale()
    {
        if (cameraShakeScale == null || cameraShakeScale.Length == 0) return 1f;
        if (currentCameraIndex >= cameraShakeScale.Length) return 1f;
        return cameraShakeScale[currentCameraIndex];
    }

    private void Awake()
    {
        if (inputActions != null) inputActions = Instantiate(inputActions);
        var cameraMap = inputActions?.FindActionMap("Camera");
        _switchCamera = cameraMap?.FindAction("SwitchCamera");
        _look         = cameraMap?.FindAction("CameraLook");
        _lookBehind   = cameraMap?.FindAction("Look Behind");

        if (bikeController == null)
        {
            Debug.LogError("bikeController is not assigned on " + gameObject.name);
            enabled = false;
            return;
        }

        transform.parent = null;
        smoothFOV        = minFOV;
        transform.name   = "CameraController_" +
                           RemovePrefix(bikeController.transform.name, prefix: "Bike");
    }

    void Start()
    {
        cameraNoise                = new CinemachineBasicMultiChannelPerlin[cameras.Length];
        cameraFollows          = new CinemachineFollow[cameras.Length];
        cameraPanTilts                 = new CinemachinePanTilt[cameras.Length];
        initialCameraFollowTargets = new Transform[cameras.Length];
        initialCameraLookAtTargets = new Transform[cameras.Length];
        initialLookBehindFollowTargets = new Transform[cameras.Length];
        initialLookBehindLookAtTargets = new Transform[cameras.Length];
        baseFollowOffsets          = new Vector3[cameras.Length];

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
            {
                Debug.LogError($"{name}: cameras[{i}] is empty, that camera is skipped. " +
                               "Run MotoSquid > Characters > Repair Camera Wiring.", this);
                continue;
            }

            cameras[i].gameObject.SetActive(i == currentCameraIndex);
            cameraNoise[i]       = cameras[i].GetComponent<CinemachineBasicMultiChannelPerlin>();
            cameraFollows[i] = cameras[i].GetComponent<CinemachineFollow>();
            cameraPanTilts[i]        = cameras[i].GetComponent<CinemachinePanTilt>();

            initialCameraFollowTargets[i] = cameras[i].Follow;
            initialCameraLookAtTargets[i] = cameras[i].LookAt;

            var lb = LookBehind(i);
            if (lb != null)
            {
                initialLookBehindFollowTargets[i] = lb.Follow;
                initialLookBehindLookAtTargets[i] = lb.LookAt;
            }

            // Capture the designer set follow offset as the base for ground rush
            if (cameraFollows[i] != null)
                baseFollowOffsets[i] = cameraFollows[i].FollowOffset;
        }

        if (lookBehindCameras != null)
            foreach (var lb in lookBehindCameras)
                lb?.gameObject.SetActive(false);

        m_LastGear = bikeController.currentGear;

        if (nearMissFX == null)
            nearMissFX = bikeController.GetComponentInChildren<NearMissFX>(true);

        if (nearMissFX != null)
            nearMissFX.cameraController = this;

        if (boostSystem == null)
            boostSystem = bikeController.GetComponentInChildren<BoostSystem>(true);
        if (boostSystem == null)
            boostSystem = bikeController.GetComponentInParent<BoostSystem>(true);
        if (boostSystem == null)
            Debug.LogWarning("CameraController, BoostSystem not found assign it manually in the Inspector.");
        if (physicalCamera != null)
        {
            var hdData = physicalCamera.GetComponent<HDAdditionalCameraData>();
            if (hdData != null)
                hdData.volumeLayerMask = BuildIsolatedVolumeMask();
            else
                Debug.LogWarning("CameraController: physicalCamera has no HDAdditionalCameraData; " +
                                 "speed FX can't be isolated per player.", this);
        }
        else
        {
            Debug.LogWarning("CameraController: physicalCamera not assigned, speed FX can't be " +
                             "isolated per player.", this);
        }
    }

    int BuildIsolatedVolumeMask()
    {
        int ownFXLayer = -1;
        if (bikeController != null)
        {
            var fx = bikeController.GetComponentInChildren<BikeSpeedFX>(true);
            if (fx == null)
                fx = bikeController.GetComponentInParent<BikeSpeedFX>(true);
            if (fx != null) ownFXLayer = fx.FXLayer;
        }

        int mask = cameraVolumeLayerMask;
        if (allPlayerFXLayers != null)
            foreach (int fxLayer in allPlayerFXLayers)
                if (fxLayer >= 0 && fxLayer != ownFXLayer)
                    mask &= ~(1 << fxLayer);   // Never blend another player's FX volume

        if (ownFXLayer >= 0)
            mask |= (1 << ownFXLayer);         // Always blend our own FX volume

        return mask;
    }

    void Update()
    {
        // Awake unparents this rig so it does not inherit the bike's lean, which also means nothing
        // destroys it with the bike. Spawned bikes come and go now, so it has to clean itself up
        if (bikeController == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!lookEnabled && bikeController.canMove)
            lookEnabled = true;

        if (lookEnabled && _switchCamera != null && _switchCamera.WasPressedThisFrame() && IsDeviceAllowed(_switchCamera.activeControl?.device))
        {
            SwitchCamera();
        }

        float bikeSpeed = bikeController.localBikeVelocity.magnitude;

        if (bikeSpeed > speedThresholdForShake && bikeController.isActiveAndEnabled)
            UpdateShake(bikeSpeed);
        else if (isShaking)
            StopShake();

        UpdateFOV(bikeSpeed);
        UpdateGroundRush(bikeSpeed);
        UpdateLookBehind();
        UpdateApexLook();
        UpdateLookOrbit();
        UpdateGearShake();
        UpdateNearMissShake();
    }

    public bool IsFirstPersonView => IsCurrentCameraFirstPerson();

    bool IsCurrentCameraFirstPerson()
    {
        return cameraIsFirstPerson != null &&
               currentCameraIndex < cameraIsFirstPerson.Length &&
               cameraIsFirstPerson[currentCameraIndex];
    }

    void UpdateLookBehind()
    {
        if (!lookEnabled)
        {
            if (_isLookingBehind) { _isLookingBehind = false; lookBehindCameras?[currentCameraIndex]?.gameObject.SetActive(false); }
            return;
        }
        bool wants = _lookBehind != null && _lookBehind.IsPressed() && IsDeviceAllowed(_lookBehind.activeControl?.device);
        if (wants == _isLookingBehind) return;

        _isLookingBehind = wants;

        if (lookBehindCameras == null || currentCameraIndex >= lookBehindCameras.Length) return;
        lookBehindCameras[currentCameraIndex]?.gameObject.SetActive(_isLookingBehind);
    }

    bool ShouldLookOrbitDriveOffset()
    {
        if (!lookEnabled || _isLookingBehind) return false;
        if (cameraFollows == null || currentCameraIndex >= cameraFollows.Length) return false;
        if (cameraFollows[currentCameraIndex] == null) return false;

        // FP cameras use CinemachinePanTilt for free look, not a follow offset orbit
        if (IsCurrentCameraFirstPerson()) return false;
        return true;
    }

    void UpdateLookOrbit()
    {
        if (!ShouldLookOrbitDriveOffset()) return;
        var trans = cameraFollows[currentCameraIndex];

        // Free look accumulate yaw from input, auto recentre when idle
        Vector2 raw = _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;

        // Discard input from disallowed devices (same filter used by switchCamera/lookBehind)
        if (raw.sqrMagnitude > 0.001f && !IsDeviceAllowed(_look?.activeControl?.device))
            raw = Vector2.zero;

        if (raw.sqrMagnitude > 0.001f)
        {
            bool  isGamepad = _look?.activeControl?.device is Gamepad;
            float scale     = isGamepad ? gamepadLookSensitivity * Time.deltaTime : mouseLookSensitivity;
            _lookYaw        = Mathf.Clamp(_lookYaw + raw.x * scale, -lookYawClamp, lookYawClamp);

            float pitchDir  = invertLookY ? 1f : -1f;
            _lookPitch      = Mathf.Clamp(_lookPitch + raw.y * scale * lookPitchSensitivity * pitchDir,
                                  -lookPitchClamp, lookPitchClamp);
            _lastLookTime   = Time.time;
        }
        else if (Time.time - _lastLookTime > lookRecentreDelay)
        {
            _lookYaw   = Mathf.Lerp(_lookYaw,   0f, lookRecentreSpeed * Time.deltaTime);
            _lookPitch = Mathf.Lerp(_lookPitch, 0f, lookRecentreSpeed * Time.deltaTime);
        }

        Vector3 baseOff = baseFollowOffsets[currentCameraIndex];
        Vector3 grOff   = new Vector3(baseOff.x, baseOff.y - groundRushYDrop * m_GroundRushT, baseOff.z);
        // Apex look adds an automatic yaw toward the corner on top of the player's free look orbit
        trans.FollowOffset = Quaternion.Euler(_lookPitch, _lookYaw + m_ApexYaw, 0f) * grOff;
    }

    void UpdateApexLook()
    {
        float target = 0f;
        if (apexLookEnabled && lookEnabled && !_isLookingBehind && bikeController != null)
        {
            // Tone the turn down with the accessibility "reduce motion" setting, like Dutch roll
            float motionScale = AccessibilityManager.Instance != null
                                    ? AccessibilityManager.Instance.ScreenShakeScale : 1f;
            target = Mathf.Clamp(bikeController.AnalogSteer, -1f, 1f) * apexLookYaw * motionScale;
        }
        m_ApexYaw = Mathf.Lerp(m_ApexYaw, target, apexLookSmooth * Time.deltaTime);

        if (IsCurrentCameraFirstPerson() && cameraPanTilts != null && currentCameraIndex < cameraPanTilts.Length)
        {
            var panTilt = cameraPanTilts[currentCameraIndex];
            if (panTilt != null)
                panTilt.PanAxis.Value += (m_ApexYaw - m_ApexYawPrev);
        }
        m_ApexYawPrev = m_ApexYaw;
    }

    void SwitchCamera()
    {
        Debug.Log($"[CameraController] SwitchCamera fired on '{name}' at t={Time.time:0.00}, " +
                  $"index {currentCameraIndex} -> {(currentCameraIndex + 1) % cameras.Length}. " +
                  $"device={_switchCamera?.activeControl?.device?.displayName ?? "none"}", this);

        if (cameras == null || cameras.Length == 0) return;

        // Restore ground rush offset on old camera before switching
        var oldTrans = cameraFollows[currentCameraIndex];
        if (oldTrans != null)
            oldTrans.FollowOffset = baseFollowOffsets[currentCameraIndex];

        // Disable any active look behind camera before switching main camera
        if (_isLookingBehind && lookBehindCameras != null && currentCameraIndex < lookBehindCameras.Length)
            lookBehindCameras[currentCameraIndex]?.gameObject.SetActive(false);
        _isLookingBehind = false;

        cameras[currentCameraIndex].gameObject.SetActive(false);
        currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
        cameras[currentCameraIndex].gameObject.SetActive(true);
        _lookYaw      = 0f;
        _lookPitch    = 0f;
        _lastLookTime = -999f;
        StopShake();
    }

    void GetSpeedShakeGains(float bikeSpeed, out float amplitude, out float frequency)
    {
        float topSpeed = bikeController.bikeSettings.maxSpeed;
        if (boostSystem != null)
            topSpeed = Mathf.Max(topSpeed, boostSystem.maxBoostSpeedKmh / 3.6f);

        float t = Mathf.InverseLerp(speedThresholdForShake, topSpeed, bikeSpeed);

        t = Mathf.Pow(t, Mathf.Max(shakeSpeedExponent, 0.01f));
        if (bikeSpeed <= speedThresholdForShake) t = 0f;

        amplitude = shakeAmplitude * CurrentShakeScale() * t;
        frequency = shakeFrequency * t;
    }

    void UpdateShake(float bikeSpeed)
    {
        isShaking = true;
        if (nearMissFX != null && nearMissFX.nearMissShakeTimer > 0f) return;
        if (cameraNoise == null || currentCameraIndex >= cameraNoise.Length) return;
        if (cameraNoise[currentCameraIndex] != null)
        {
            GetSpeedShakeGains(bikeSpeed, out float amp, out float freq);
            cameraNoise[currentCameraIndex].AmplitudeGain = amp;
            cameraNoise[currentCameraIndex].FrequencyGain = freq;
        }
    }

    void StopShake()
    {
        isShaking = false;
        for (int i = 0; i < cameras.Length; i++)
            if (cameraNoise[i] != null)
            {
                cameraNoise[i].AmplitudeGain = 0f;
                cameraNoise[i].FrequencyGain = 0f;
            }
    }

    void UpdateFOV(float bikeSpeed)
    {
        if (bikeController == null || bikeController.bikeSettings == null) return;
        if (cameras == null || cameras.Length == 0 ||
            currentCameraIndex < 0 || currentCameraIndex >= cameras.Length) return;

        float maxSpeed   = bikeController.bikeSettings.maxSpeed;
        float fovFull    = fovFullSpeed > 0f ? fovFullSpeed : maxSpeed;
        float t          = Mathf.Clamp01(Mathf.InverseLerp(fovStartSpeed, fovFull, bikeSpeed));
        t                = Mathf.Pow(t, Mathf.Max(fovSpeedExponent, 0.01f));
        float speedFOV   = Mathf.Lerp(minFOV, maxFOV, t);

        float overT = 0f;
        if (boostSystem != null)
        {
            float boostTop = boostSystem.maxBoostSpeedKmh / 3.6f;
            overT = Mathf.Clamp01(Mathf.InverseLerp(maxSpeed, boostTop, bikeSpeed));
        }
        bool  boosting   = boostSystem != null && boostSystem.isBoosting;
        float boostExtra = boosting ? boostFOVDelta * Mathf.Lerp(0.4f, 1f, overT) : 0f;
        float targetFOV  = speedFOV + boostExtra;

        smoothFOV = Mathf.SmoothDamp(smoothFOV, targetFOV, ref m_BoostFOVVel,
                        boosting ? boostFOVPushTime : boostFOVReleaseTime);

        float gearFOVTarget = gearFOVBuild * Mathf.Pow(bikeController.GearProgress01,
                                  Mathf.Max(gearFOVExponent, 0.01f));
        m_GearFOVOffset = Mathf.SmoothDamp(m_GearFOVOffset, gearFOVTarget, ref m_GearFOVVel,
                              Mathf.Max(0.01f, gearFOVReleaseTime));

        if (cameras[currentCameraIndex] != null)
        {
            cameras[currentCameraIndex].Lens.FieldOfView = smoothFOV + m_GearFOVOffset + accessibilityFovBias;

            float motionScale = AccessibilityManager.Instance != null
                                    ? AccessibilityManager.Instance.ScreenShakeScale : 1f;
            float dutchTarget = dutchRollEnabled
                ? Mathf.Clamp(bikeController.currentLeanAngle * dutchRollPerLeanDegree,
                              -maxDutch, maxDutch) * motionScale * dutchRollStrength
                : 0f;
            m_Dutch = Mathf.Lerp(m_Dutch, dutchTarget, dutchSmooth * Time.deltaTime);
            cameras[currentCameraIndex].Lens.Dutch = m_Dutch;
        }
    }

    void UpdateGearShake()
    {
        int gear = bikeController.currentGear;

        if (gear != m_LastGear)
        {
            // Trigger a brief shake on any gear change
            m_GearShakeTimer = gearShakeDuration;
            m_LastGear = gear;
        }

        if (cameraNoise == null || currentCameraIndex >= cameraNoise.Length
            || cameraNoise[currentCameraIndex] == null) return;

        // Near miss owns the shake entirely, never fight it
        bool nearMissActive = nearMissFX != null && nearMissFX.nearMissShakeTimer > 0f;
        if (nearMissActive || m_GearShakeTimer <= 0f) return;

        m_GearShakeTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(m_GearShakeTimer / Mathf.Max(gearShakeDuration, 0.001f));

        float bikeSpeed = bikeController.localBikeVelocity.magnitude;
        GetSpeedShakeGains(bikeSpeed, out float baseAmp, out float baseFreq);

        float speedT = Mathf.Clamp01(Mathf.InverseLerp(speedThresholdForShake,
                           bikeController.bikeSettings.maxSpeed, bikeSpeed));
        float burst  = gearShakeAmplitude * t * (1f + speedT * gearShakeSpeedScale) * CurrentShakeScale();

        var noise = cameraNoise[currentCameraIndex];
        noise.AmplitudeGain = baseAmp + burst;
        noise.FrequencyGain = Mathf.Max(baseFreq, gearShakeFrequency);
    }

    void UpdateNearMissShake()
    {
        if (nearMissFX == null || nearMissFX.nearMissShakeTimer <= 0f) return;
        if (cameraNoise == null || currentCameraIndex >= cameraNoise.Length) return;
        if (cameraNoise[currentCameraIndex] == null) return;

        // Near miss is highest priority, overrides speed and gear shake
        float t = nearMissFX.nearMissShakeTimer / Mathf.Max(nearMissFX.shakeDuration, 0.001f);
        cameraNoise[currentCameraIndex].AmplitudeGain =
            nearMissFX.nearMissShakeAmplitude * t * CurrentShakeScale();
        cameraNoise[currentCameraIndex].FrequencyGain = 8f;
    }

    void UpdateGroundRush(float bikeSpeed)
    {
        if (cameras == null || cameras.Length == 0) return;
        if (currentCameraIndex < 0 || currentCameraIndex >= cameras.Length) return;

        bool  braking    = bikeController.bikeInput != null && bikeController.bikeInput.HandBrake > 0.5f;
        float rushTarget = braking ? 0f
                         : Mathf.Clamp01(bikeSpeed / Mathf.Max(groundRushFullSpeed, 0.1f));

        m_GroundRushT = Mathf.SmoothDamp(m_GroundRushT, rushTarget,
                            ref m_GroundRushVel, groundRushSmoothTime);

        if (ShouldLookOrbitDriveOffset()) return;

        var trans = cameraFollows[currentCameraIndex];
        if (trans == null) return;

        Vector3 baseOffset   = baseFollowOffsets[currentCameraIndex];
        trans.FollowOffset = new Vector3(
            baseOffset.x,
            baseOffset.y - groundRushYDrop * m_GroundRushT,
            baseOffset.z);
    }

    CinemachineCamera LookBehind(int i) =>
        lookBehindCameras != null && i < lookBehindCameras.Length ? lookBehindCameras[i] : null;

    public void SetCameraTarget(Transform followTarget, Transform lookAtTarget)
    {
        foreach (var camera in cameras)
        {
            camera.LookAt = lookAtTarget;
            camera.Follow = followTarget;
        }

        // The look behind pair was never retargeted, so glancing back mid crash snapped to a bike
        // that is no longer where the action is.
        if (lookBehindCameras != null)
            foreach (var lb in lookBehindCameras)
            {
                if (lb == null) continue;
                lb.LookAt = lookAtTarget;
                lb.Follow = followTarget;
            }

        StopShake();
    }

    public void AssignBike(BikeController bike) => bikeController = bike;

    // The rendering camera lives in the scene, so a spawned rig cannot carry the reference
    public void AssignPhysicalCamera(Camera cam) => physicalCamera = cam;
    public Camera PhysicalCamera => physicalCamera;
    public BikeController Bike => bikeController;
    public void RetargetCameras(Transform follow, Transform lookAt)
    {
        if (cameras != null)
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                cam.Follow = follow;
                cam.LookAt = lookAt;
            }
        // If Start already captured base targets, keep them in sync so ResetCameraTarget returns here
        if (initialCameraFollowTargets != null && initialCameraLookAtTargets != null)
            for (int i = 0; i < initialCameraFollowTargets.Length; i++)
            {
                initialCameraFollowTargets[i] = follow;
                initialCameraLookAtTargets[i] = lookAt;
            }
    }

    public void ResetCameraTarget()
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].Follow = initialCameraFollowTargets[i];
            cameras[i].LookAt = initialCameraLookAtTargets[i];

            var lb = LookBehind(i);
            if (lb != null)
            {
                lb.Follow = initialLookBehindFollowTargets[i];
                lb.LookAt = initialLookBehindLookAtTargets[i];
            }
        }
        StopShake();

        // Reset any look orbit so we don't snap back with a stale yaw offset
        _lookYaw      = 0f;
        _lookPitch    = 0f;
        _lastLookTime = -999f;

        var active = cameras[currentCameraIndex];

        // Force a brain level cut so no blend occurs between virtual cameras
        CinemachineBrain brain = physicalCamera != null ? physicalCamera.GetComponent<CinemachineBrain>() : null;
        if (brain != null)
        {
            var savedBlend = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            active.gameObject.SetActive(false);
            active.gameObject.SetActive(true);
            StartCoroutine(RestoreBlendNextFrame(brain, savedBlend));
        }
        else
        {
            active.gameObject.SetActive(false);
            active.gameObject.SetActive(true);
        }

        active.PreviousStateIsValid = false;
    }

    private IEnumerator RestoreBlendNextFrame(CinemachineBrain brain, CinemachineBlendDefinition savedBlend)
    {
        yield return null;
        brain.DefaultBlend = savedBlend;
    }

    private string RemovePrefix(string originalName, string prefix)
    {
        if (originalName.StartsWith(prefix))
            return originalName.Substring(prefix.Length);
        return originalName;
    }

    public void SetCameraTargetToRagdoll() => SetCameraTarget(ragdollFollowTarget, ragdollLookAtTarget);
    public void ResetCameraToBike()        => ResetCameraTarget();

    // The mouse belongs to whoever holds the keyboard, never to both players: passing it through
    // unconditionally linked the right stick and the mouse so either moved both cameras.
    bool IsDeviceAllowed(InputDevice device) =>
        InputDeviceFilter.Allows(device, lockToKeyboard, playerDeviceIndex, allowKeyboard,
                                          allowMouse: lockToKeyboard || allowKeyboard);

    public void SetDeviceFilter(bool lockKb, int deviceIndex, bool allowKb = false)
    {
        lockToKeyboard    = lockKb;
        playerDeviceIndex = deviceIndex;
        allowKeyboard     = allowKb;
    }
}
}
