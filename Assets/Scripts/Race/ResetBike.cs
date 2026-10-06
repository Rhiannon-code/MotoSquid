using MotoSquid.Bike;
using MotoSquid.Controls;
using MotoSquid.Rider;
using MotoSquid.Track;
using MotoSquid.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.Race
{

public class ResetBike : MonoBehaviour
{
    public BikeController    bikeController;
    public BikeAIController  aiController;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private int  playerDeviceIndex = -1;
    [SerializeField] private bool lockToKeyboard    = false;
    // The fields were here but nothing ever set them, so any device respawned both players' bikes.
    [SerializeField] private bool allowKeyboard      = false;

    public void SetDeviceFilter(bool lockKb, int deviceIndex, bool allowKb = false)
    {
        lockToKeyboard    = lockKb;
        playerDeviceIndex = deviceIndex;
        allowKeyboard     = allowKb;
    }
    private InputAction _reset;

    [Header("Auto Reset")]
    public float autoResetDelay = 3f;
    // An AI that crashes while a player leads is put straight back, so one mistake does not end its race
    public float aiResetDelayWhenPlayerLeads = 0f;

    [Header("Track Respawn")]
    public CheckpointTracker checkpointTracker;

    public float respawnHeightOffset    = 0.5f;
    public float respawnAdvanceDistance = 20f;

    [Range(0f, 1f)] public float speedCarryOver = 0.5f;

    [Header("Race Start Lock")]
    public RaceManager raceManager;
    public float raceStartLockDuration = 5f;

    [Header("Invulnerability")]
    public float invulnerabilityDuration = 3f;

    [Header("Reset Prompt (player)")]
    public GameObject resetPromptUI;

    [Header("Stuck Respawn (AI only)")]
    public bool  enableStuckRespawn  = true;
    public float stuckTimeThreshold  = 5f;
    public float stuckMoveThreshold  = 3f;

    [Header("Off Track Respawn")]
    public bool  enableOffTrackRespawn      = true;
    public float offTrackDistance           = 30f;
    public float playerOffTrackRespawnDelay = 10f;
    public float aiOffTrackRespawnDelay     = 5f;

    [Header("Fell Through The Track")]
    public bool  enableFallThroughRespawn = true;
    public float fallThroughDepth = 8f;
    public float fallThroughDelay = 0.5f;

    [Header("HUD")]
    public HUDManager hud;

    [Header("Wrong Way Warning (player)")]
    public bool  enableWrongWayWarning   = true;
    public float wrongWaySpeedThreshold  = 3f;
    public float wrongWayDot             = -0.25f;
    // Clearing on the same threshold it triggers on makes the warning strobe through every corner,
    // where the track direction swings either side of the entry angle. It has to be won back.
    public float wrongWayClearDot        = 0.15f;
    public float wrongWayWarningDelay     = 0.5f;
    public float wrongWayClearDelay      = 0.6f;
    private RagdollActivator _ragdoll;
    private RagdollActivator _aiRagdoll;
    private GameObject _pendingPlayerReset;
    private GameObject _pendingAIReset;
    private float _resetLockedUntil = -1f;
    private bool    _hasRespawnCache;
    private Vector3 _cachedRespawnPos;
    private Vector3 _cachedRespawnFwd;
    private float   _playerOffTrackTimer;
    private float   _playerWrongWayTimer;
    private float   _playerFallThroughTimer;
    private float   _playerWrongWayClearTimer;
    private bool    _playerWrongWayShown;
    private float   _aiOffTrackTimer;
    private Vector3 _aiStuckAnchor;
    private float   _aiStuckTimer;
    private int                     _crashWaypointSnapshot = -1;
    private RaceWaypoint[] _crashWaypointsSnapshot;

    private void Awake()
    {
        if (inputActions != null)
        {
            inputActions = Instantiate(inputActions);
            _reset = inputActions.FindActionMap("Bike", throwIfNotFound: true)
                                 .FindAction("Reset",  throwIfNotFound: true);
        }
    }

    private void OnEnable()  => _reset?.Enable();
    private void OnDisable() => _reset?.Disable();

    private void Start()
    {
        if (bikeController == null && aiController == null)
        {
            bikeController = GetComponent<BikeController>();
            aiController   = GetComponent<BikeAIController>();
        }

        if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();

        if (checkpointTracker == null)
            checkpointTracker = GetComponentInChildren<CheckpointTracker>(true)
                             ?? GetComponentInParent<CheckpointTracker>(true);

        if (hud == null) hud = FindFirstObjectByType<HUDManager>();

        if (bikeController != null)
        {
            if (checkpointTracker == null)
                Debug.LogWarning($"[ResetBike] '{name}' has no CheckpointTracker, so off-track and " +
                                 "wrong-way detection are both dead on this bike.", this);
            if (hud == null)
                Debug.LogWarning($"[ResetBike] '{name}' found no HUDManager, so its warnings have " +
                                 "nowhere to show.", this);
        }

        if (bikeController != null)
            _ragdoll = bikeController.bikeReferences.ragdollActivator;

        if (aiController != null)
            _aiRagdoll = aiController.bikeReferences.ragdollActivator;

        if (_ragdoll != null && autoResetDelay > 0f)
            _ragdoll.onRagdollActivated.AddListener(OnRagdollActivated);

        if (_aiRagdoll != null && autoResetDelay > 0f)
            _aiRagdoll.onRagdollActivated.AddListener(OnAIRagdollActivated);

        if (raceManager != null)
        {
            raceManager.OnRaceStart += OnRaceStarted;
            _resetLockedUntil = float.MaxValue; 
        }

        if (resetPromptUI != null) resetPromptUI.SetActive(false);
    }

    private void UpdateResetPrompt()
    {
        if (resetPromptUI == null) return;
        bool canReset = bikeController != null && _ragdoll != null &&
                        _ragdoll.IsRagdollActive && Time.time >= _resetLockedUntil;
        if (resetPromptUI.activeSelf != canReset) resetPromptUI.SetActive(canReset);
    }

    private void OnDestroy()
    {
        if (_ragdoll != null)
            _ragdoll.onRagdollActivated.RemoveListener(OnRagdollActivated);

        if (_aiRagdoll != null)
            _aiRagdoll.onRagdollActivated.RemoveListener(OnAIRagdollActivated);

        if (raceManager != null)
            raceManager.OnRaceStart -= OnRaceStarted;
    }

    private void OnRaceStarted()
    {
        _resetLockedUntil = Time.time + raceStartLockDuration;
        checkpointTracker?.Initialise();

        if (aiController != null)
            _aiStuckAnchor = aiController.transform.position;
        _aiStuckTimer = 0f;
        _aiOffTrackTimer = 0f;
        _playerOffTrackTimer = 0f;
        _playerWrongWayTimer = 0f;
        _playerWrongWayClearTimer = 0f;
        _playerWrongWayShown = false;
        _playerFallThroughTimer = 0f;
    }

    private void OnRagdollActivated()
    {
        _hasRespawnCache = false;

        if (checkpointTracker != null && checkpointTracker.IsReady)
        {
            _crashWaypointSnapshot  = checkpointTracker.CurrentWaypoint;
            _crashWaypointsSnapshot = checkpointTracker.CurrentWaypoints;
        }
        else
        {
            _crashWaypointSnapshot  = -1;
            _crashWaypointsSnapshot = null;
        }

        if (checkpointTracker != null) checkpointTracker.enabled = false;

        CancelPendingReset(ref _pendingPlayerReset);
        _pendingPlayerReset = ResetBikeRunner.Spawn(autoResetDelay, () =>
        {
            if (_ragdoll == null || !_ragdoll.IsRagdollActive)
            {
                Debug.LogWarning("[ResetBike] Auto reset timer fired but ragdoll is not active, skipping.", this);
                return;
            }
            ResetCurrentBike();
        });
    }

    private void OnAIRagdollActivated()
    {
        if (checkpointTracker != null) checkpointTracker.enabled = false;
        CancelPendingReset(ref _pendingAIReset);
        _pendingAIReset = ResetBikeRunner.Spawn(PlayerLeads() ? aiResetDelayWhenPlayerLeads : autoResetDelay, () =>
        {
            if (_aiRagdoll == null || !_aiRagdoll.IsRagdollActive)
            {
                Debug.LogWarning("[ResetBike] AI auto reset timer fired but ragdoll is not active, skipping.", this);
                return;
            }
            ResetAIBike();
        });
    }

    // Human entries carry no AI controller, and Standings is kept sorted by progress
    private bool PlayerLeads()
    {
        var standings = raceManager != null ? raceManager.Standings : null;
        return standings != null && standings.Count > 0 && standings[0].ai == null;
    }

    private void CancelPendingReset(ref GameObject runner)
    {
        if (runner != null) { Destroy(runner); runner = null; }
    }

    private void Update()
    {
        UpdateResetPrompt();

        if (Time.time < _resetLockedUntil) return;

        // Expire the cache once the bike has ridden well past the cached spawn point
        if (_hasRespawnCache && _ragdoll != null && !_ragdoll.IsRagdollActive &&
            bikeController != null &&
            Vector3.Distance(bikeController.transform.position, _cachedRespawnPos) > respawnAdvanceDistance * 2f)
            _hasRespawnCache = false;

        if (bikeController != null && _reset != null && WasPressedThisFrame(_reset))
        {
            if (resetPromptUI != null) resetPromptUI.SetActive(false);
            hud?.HideRespawnWarning(bikeController);
            _playerOffTrackTimer = 0f;
            ResetCurrentBike();
        }
        MonitorRecovery();
    }

    private void MonitorRecovery()
    {
        float dt = Time.deltaTime;

        // Player, off track only (with on screen countdown)
        if (bikeController != null)
        {
            bool busy = (_ragdoll != null && _ragdoll.IsRagdollActive) || _pendingPlayerReset != null;

            if (!busy && enableFallThroughRespawn && checkpointTracker != null && checkpointTracker.IsReady)
            {
                Vector3 pos = bikeController.transform.position;

                bool couldHaveFallen = !bikeController.bikeIsGrounded;

                if (couldHaveFallen &&
                    checkpointTracker.TryGetTrackHeight(pos, out float trackY) &&
                    trackY - pos.y > fallThroughDepth)
                {
                    _playerFallThroughTimer += dt;
                    if (_playerFallThroughTimer >= fallThroughDelay)
                    {
                        // Logged with the position so the hole can be found and closed
                        Debug.LogWarning($"[ResetBike] Player fell through the track at {pos} " +
                                         $"({trackY - pos.y:F1} m below the road). Respawning.", this);
                        _playerFallThroughTimer = 0f;
                        ForceRespawnPlayerOnTrack();
                        return;
                    }
                }
                else _playerFallThroughTimer = 0f;
            }

            if (!busy && enableOffTrackRespawn && checkpointTracker != null && checkpointTracker.IsReady)
            {
                float dist = checkpointTracker.HorizontalDistanceToTrack(bikeController.transform.position);
                if (dist > offTrackDistance)
                {
                    _playerOffTrackTimer += dt;
                    float remaining = playerOffTrackRespawnDelay - _playerOffTrackTimer;
                    if (remaining <= 0f)
                    {
                        hud?.HideRespawnWarning(bikeController);
                        _playerOffTrackTimer = 0f;
                        if (resetPromptUI != null) resetPromptUI.SetActive(false);
                        ForceRespawnPlayerOnTrack();
                    }
                    else
                    {
                        hud?.ShowRespawnWarning(bikeController, Mathf.CeilToInt(remaining));
                        if (resetPromptUI != null) resetPromptUI.SetActive(true);
                    }
                }
                else
                {
                    if (_playerOffTrackTimer > 0f) hud?.HideRespawnWarning(bikeController);
                    _playerOffTrackTimer = 0f;
                    if (resetPromptUI != null) resetPromptUI.SetActive(false);
                }
            }
            else if (_playerOffTrackTimer > 0f)
            {
                _playerOffTrackTimer = 0f;
                hud?.HideRespawnWarning(bikeController);
            }

            if (!busy && enableWrongWayWarning && _playerOffTrackTimer <= 0f &&
                checkpointTracker != null && checkpointTracker.IsReady)
            {
                Vector3 trackFwd = checkpointTracker.TrackForwardDirection();
                Vector3 vel      = bikeController.bikeReferences.BikeRb.linearVelocity;
                vel.y = 0f;
                float speed = vel.magnitude;

                // Below the threshold there is no meaningful heading to judge. The old code reported
                // "aligned" here, which cleared the warning the moment you slowed down or stopped,
                // without ever turning round. The state is held instead until the bike moves again.
                bool  canJudge  = trackFwd != Vector3.zero && speed > wrongWaySpeedThreshold;
                float alignment = canJudge ? Vector3.Dot(vel / speed, trackFwd) : 0f;

                if (!canJudge)
                {
                    _playerWrongWayClearTimer = 0f;
                }
                else if (alignment < wrongWayDot)
                {
                    _playerWrongWayClearTimer = 0f;
                    _playerWrongWayTimer += dt;
                    if (_playerWrongWayTimer >= wrongWayWarningDelay)
                    {
                        if (!_playerWrongWayShown)
                            Debug.LogWarning($"[ResetBike] Wrong way shown at {bikeController.transform.position:F0}: " +
                                             $"road {checkpointTracker.CurrentRoad} wp {checkpointTracker.CurrentWaypoint}/" +
                                             $"{checkpointTracker.CurrentWaypoints.Length}, track {trackFwd:F2}, " +
                                             $"travel {vel / speed:F2}, alignment {alignment:F2}", this);
                        _playerWrongWayShown = true;
                        hud?.ShowWrongWayWarning(bikeController);
                    }
                }
                else if (alignment > wrongWayClearDot)
                {
                    _playerWrongWayClearTimer += dt;
                    if (_playerWrongWayClearTimer >= wrongWayClearDelay)
                    {
                        if (_playerWrongWayShown) hud?.HideWrongWayWarning(bikeController);
                        _playerWrongWayShown = false;
                        _playerWrongWayTimer = 0f;
                    }
                }
                else
                {
                    _playerWrongWayClearTimer = 0f;
                }
            }
            else if (_playerWrongWayShown || _playerWrongWayTimer > 0f)
            {
                _playerWrongWayTimer      = 0f;
                _playerWrongWayClearTimer = 0f;
                _playerWrongWayShown      = false;
                hud?.HideWrongWayWarning(bikeController);
            }
        }

        // AI, off track and stuck
        if (aiController != null)
        {

            bool busy = (_aiRagdoll != null && _aiRagdoll.IsRagdollActive) || _pendingAIReset != null
                        || BikeAIController.DebugHoldForCombatTest;
            if (busy)
            {
                _aiOffTrackTimer = 0f;
                _aiStuckTimer    = 0f;
                _aiStuckAnchor   = aiController.transform.position;
                return;
            }

            Vector3 aiPos = aiController.transform.position;

            // Off track
            if (enableOffTrackRespawn && aiController.aiLogic != null)
            {
                float dist = aiController.aiLogic.HorizontalDistanceFromPath();
                if (dist > offTrackDistance)
                {
                    _aiOffTrackTimer += dt;
                    if (_aiOffTrackTimer >= aiOffTrackRespawnDelay)
                    {
                        ForceRespawnAIOnTrack();
                        return;
                    }
                }
                else _aiOffTrackTimer = 0f;
            }

            // Stuck, must travel more than stuckMoveThreshold within stuckTimeThreshold
            if (enableStuckRespawn)
            {
                if ((aiPos - _aiStuckAnchor).sqrMagnitude > stuckMoveThreshold * stuckMoveThreshold)
                {
                    _aiStuckAnchor = aiPos;
                    _aiStuckTimer  = 0f;
                }
                else
                {
                    _aiStuckTimer += dt;
                    if (_aiStuckTimer >= stuckTimeThreshold)
                    {
                        ForceRespawnAIOnTrack();
                        return;
                    }
                }
            }
        }
    }

    bool WasPressedThisFrame(InputAction action) =>
        InputDeviceFilter.PressedThisFrame(action, lockToKeyboard,
                                                    playerDeviceIndex, allowKeyboard);

    public void ResetCurrentBike()
    {
        if (_ragdoll == null) return;
        CancelPendingReset(ref _pendingPlayerReset);

        if (!_ragdoll.IsRagdollActive)
            Debug.LogWarning($"[ResetBike] ResetCurrentBike called with no active ragdoll " +
                             $"(hasSnapshot={_crashWaypointSnapshot >= 0} hasCache={_hasRespawnCache}). " +
                             $"Stack:\n{System.Environment.StackTrace}", this);

        bool wasRagdoll = _ragdoll.IsRagdollActive;

        Vector3 currentVelocity;
        if (wasRagdoll)
        {
            currentVelocity  = _ragdoll.CrashVelocity;
            _ragdoll.ReEnableBike();
            _hasRespawnCache = false; // Crash = pick a fresh point
        }
        else
        {
            currentVelocity = bikeController.bikeReferences.BikeRb.linearVelocity;
        }

        var rb      = bikeController.bikeReferences.BikeRb;
        var rotator = bikeController.bikeReferences.Rotator;
        var lean    = bikeController.bikeReferences.LeanTransform;

        if (_hasRespawnCache)
        {
            // Repeated R presses return to the same spot, not further down the track
            RespawnAtPoint(bikeController.transform, rb, rotator, lean,
                           Vector3.zero, _cachedRespawnPos, _cachedRespawnFwd);
        }
        else
        {
            RespawnOnWaypoints(bikeController.transform, rb, rotator, lean, currentVelocity);
            // Cache where the bike just landed so repeated presses don't advance it
            _cachedRespawnPos = rb.position;
            _cachedRespawnFwd = bikeController.transform.forward;
            _hasRespawnCache  = true;
        }

        if (checkpointTracker != null) checkpointTracker.enabled = true;
        checkpointTracker?.Initialise();

        if (!wasRagdoll) _ragdoll.ResetCameratoBike();

        StartCoroutine(InvulnerabilityTimer(_ragdoll));
    }

    public void ResetAIBike()
    {
        if (_aiRagdoll == null || !_aiRagdoll.IsRagdollActive) return;
        CancelPendingReset(ref _pendingAIReset);

        Vector3 crashVelocity = _aiRagdoll.CrashVelocity;
        _aiRagdoll.ReEnableBike();

        // The tracker dead reckons and can be well ahead of a fast bike, so taking the respawn from it
        // unsynced put AI that crashed behind the player back on the road in front of them
        aiController?.aiLogic?.ResetTracking();

        // Prefer the AI's own waypoint tracking, it knows which road segment it's on
        if (aiController?.aiLogic != null &&
            aiController.aiLogic.GetRespawnPoint(respawnAdvanceDistance, out Vector3 pos, out Vector3 fwd))
        {
            RespawnAtPoint(aiController.transform,
                           aiController.bikeReferences.BikeRb,
                           aiController.bikeReferences.Rotator,
                           aiController.bikeReferences.LeanTransform,
                           crashVelocity,
                           pos + Vector3.up * respawnHeightOffset,
                           fwd);
        }
        else
        {
            RespawnOnWaypoints(aiController.transform,
                               aiController.bikeReferences.BikeRb,
                               aiController.bikeReferences.Rotator,
                               aiController.bikeReferences.LeanTransform,
                               crashVelocity);
        }

        if (checkpointTracker != null) checkpointTracker.enabled = true;
        checkpointTracker?.Initialise();
        aiController.aiLogic?.ResetTracking();
        // Kick off the catch-up window, extra cruise speed + auto spend boost for a few seconds
        aiController.aiLogic?.BeginRecovery();
        StartCoroutine(InvulnerabilityTimer(_aiRagdoll));
    }

    public void ForceRespawnPlayerOnTrack()
    {
        if (bikeController == null) return;
        CancelPendingReset(ref _pendingPlayerReset);

        var rb      = bikeController.bikeReferences.BikeRb;
        var rotator = bikeController.bikeReferences.Rotator;
        var lean    = bikeController.bikeReferences.LeanTransform;

        bool wasRagdoll = _ragdoll != null && _ragdoll.IsRagdollActive;
        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
        if (wasRagdoll)
        {
            vel = _ragdoll.CrashVelocity;
            _ragdoll.ReEnableBike();
        }

        _hasRespawnCache = false; // Off-track = pick a fresh point on the racing line
        if (checkpointTracker != null) checkpointTracker.enabled = true;
        RespawnOnWaypoints(bikeController.transform, rb, rotator, lean, vel);
        checkpointTracker?.Initialise();

        _playerOffTrackTimer = 0f;
        hud?.HideRespawnWarning(bikeController);

        if (!wasRagdoll) _ragdoll?.ResetCameratoBike();

        if (_ragdoll != null) StartCoroutine(InvulnerabilityTimer(_ragdoll));
    }

    // Force an AI back onto the track without a crash (used by the stuck/off track recovery)
    public void ForceRespawnAIOnTrack()
    {
        if (aiController == null) return;
        CancelPendingReset(ref _pendingAIReset);

        var rb      = aiController.bikeReferences.BikeRb;
        var rotator = aiController.bikeReferences.Rotator;
        var lean    = aiController.bikeReferences.LeanTransform;

        Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
        if (_aiRagdoll != null && _aiRagdoll.IsRagdollActive)
        {
            vel = _aiRagdoll.CrashVelocity;
            _aiRagdoll.ReEnableBike();
        }

        aiController.aiLogic?.ResetTracking();
        if (aiController.aiLogic != null &&
            aiController.aiLogic.GetRespawnPoint(respawnAdvanceDistance, out Vector3 pos, out Vector3 fwd))
        {
            RespawnAtPoint(aiController.transform, rb, rotator, lean, vel,
                           pos + Vector3.up * respawnHeightOffset, fwd);
        }
        else
        {
            RespawnOnWaypoints(aiController.transform, rb, rotator, lean, vel);
        }

        if (checkpointTracker != null) checkpointTracker.enabled = true;
        checkpointTracker?.Initialise();
        aiController.aiLogic?.ResetTracking();
        // Kick off the catch up window so the recovered AI rejoins the pack
        aiController.aiLogic?.BeginRecovery();
        if (_aiRagdoll != null) StartCoroutine(InvulnerabilityTimer(_aiRagdoll));

        // Reset the recovery monitors so we don't immediately re-trigger.
        _aiStuckAnchor   = aiController.transform.position;
        _aiStuckTimer    = 0f;
        _aiOffTrackTimer = 0f;
    }

    // Advance by respawnAdvanceDistance along the road the tracker recorded, then respawn
    private void RespawnOnWaypoints(Transform bikeRoot, Rigidbody rb, Transform rotator,
                                    Transform lean, Vector3 crashVelocity)
    {
        RaceWaypoint[] waypoints;
        int startIdx;

        if (_crashWaypointsSnapshot != null && _crashWaypointSnapshot >= 0)
        {
            // Use the crash time snapshot so invisible bike drift doesn't skew the respawn point
            waypoints               = _crashWaypointsSnapshot;
            startIdx                = _crashWaypointSnapshot;
            _crashWaypointsSnapshot = null;
            _crashWaypointSnapshot  = -1;
        }
        else
        {
            if (checkpointTracker == null || !checkpointTracker.IsReady)
            {
                Debug.LogWarning("[ResetBike] CheckpointTracker not ready.", this);
                return;
            }
            waypoints = checkpointTracker.CurrentWaypoints;
            startIdx  = checkpointTracker.CurrentWaypoint;
        }

        float remaining = respawnAdvanceDistance;
        int idx = startIdx;
        for (int safety = 0; safety < waypoints.Length && remaining > 0f; safety++)
        {
            if (idx >= waypoints.Length - 1) break;
            int nextIdx = idx + 1;
            float segLen = Vector3.Distance(waypoints[idx].position, waypoints[nextIdx].position);
            if (remaining <= segLen) break;
            remaining -= segLen;
            idx = nextIdx;
        }

        int forwardIdx = idx < waypoints.Length - 1 ? idx + 1 : idx;
        float segmentLen = Vector3.Distance(waypoints[idx].position, waypoints[forwardIdx].position);
        float frac       = segmentLen > 0.001f ? Mathf.Clamp01(remaining / segmentLen) : 0f;

        Vector3 respawnPos;
        Vector3 graphFwd = Vector3.zero; // Non zero when RouteGraph supplies the forward direction

        if (forwardIdx == idx && remaining > 0f)
        {
            if (!TryRouteGraphRespawn(waypoints[idx].position, remaining, out respawnPos, out graphFwd))
            {
                // No RouteGraph, extrapolate along the last segment direction
                if (idx > 0)
                {
                    Vector3 lastSegDir = (waypoints[idx].position - waypoints[idx - 1].position).normalized;
                    lastSegDir.y = 0f;
                    if (lastSegDir.sqrMagnitude < 0.001f) lastSegDir = Vector3.forward;
                    respawnPos = waypoints[idx].position + lastSegDir * remaining;
                }
                else
                {
                    respawnPos = waypoints[idx].position;
                }
            }
        }
        else
        {
            respawnPos = Vector3.Lerp(waypoints[idx].position, waypoints[forwardIdx].position, frac);
        }

        Vector3 fwd;
        if (graphFwd.sqrMagnitude > 0.001f)
        {
            fwd = graphFwd;
        }
        else
        {
            if (forwardIdx != idx)
                fwd = waypoints[forwardIdx].position - waypoints[idx].position;
            else if (idx > 0)
                fwd = waypoints[idx].position - waypoints[idx - 1].position;
            else
                fwd = bikeRoot.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = bikeRoot.forward;
        }

        RespawnAtPoint(bikeRoot, rb, rotator, lean, crashVelocity,
                       respawnPos + Vector3.up * respawnHeightOffset,
                       fwd.normalized);
    }

    private bool TryRouteGraphRespawn(Vector3 nearPosition, float advanceDistance,
                                      out Vector3 position, out Vector3 forward)
    {
        position = nearPosition;
        forward  = Vector3.forward;

        var graph = checkpointTracker?.routeGraph;
        if (graph == null || !graph.IsBaked) return false;

        graph.FindNearestProgress(nearPosition, out int seg, out int wp, out float frac,
                                  avoidTriggerOnly: true);

        var segs     = graph.BakedSegments;
        var pts      = segs[seg].positions;
        var lens     = segs[seg].legLengths;
        int legCount = pts.Length - 1;

        float remaining = advanceDistance;
        float legRem    = lens[wp] * (1f - frac);

        if (remaining <= legRem)
        {
            float f  = frac + remaining / Mathf.Max(lens[wp], 0.001f);
            position = Vector3.Lerp(pts[wp], pts[wp + 1], f);
            Vector3 d = pts[wp + 1] - pts[wp]; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) forward = d.normalized;
            return true;
        }

        remaining -= legRem;
        wp++;

        for (int iter = 0; iter < 300 && remaining > 0.001f; iter++)
        {
            if (wp >= legCount)
            {
                int[] exits = graph.GetExits(seg);
                if (exits == null || exits.Length == 0) break;
                // Prefer the first non trigger only exit so we don't aim at a shortcut ramp
                int nextSeg = -1;
                foreach (int e in exits)
                {
                    if (e < 0 || e >= segs.Length) continue;
                    if (e < graph.segments.Length && graph.segments[e].requiresTrigger) continue;
                    nextSeg = e;
                    break;
                }
                if (nextSeg < 0) nextSeg = exits[0]; // Fallback, take first exit even if trigger
                if (nextSeg == seg) break;           // Self loop guard

                seg      = nextSeg;
                pts      = segs[seg].positions;
                lens     = segs[seg].legLengths;
                legCount = pts.Length - 1;
                wp       = 0;
            }

            float legLen = lens[wp];
            if (remaining <= legLen)
            {
                float f  = remaining / Mathf.Max(legLen, 0.001f);
                position = Vector3.Lerp(pts[wp], pts[wp + 1], f);
                Vector3 d = pts[wp + 1] - pts[wp]; d.y = 0f;
                if (d.sqrMagnitude > 0.001f) forward = d.normalized;
                return true;
            }
            remaining -= legLen;
            wp++;
        }

        // Reached end of graph walk, use endpoint
        position = pts[pts.Length - 1];
        if (pts.Length >= 2)
        {
            Vector3 d = pts[pts.Length - 1] - pts[pts.Length - 2]; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) forward = d.normalized;
        }
        return true;
    }

    private void RespawnAtPoint(Transform bikeRoot, Rigidbody rb, Transform rotator,
                                Transform lean, Vector3 crashVelocity,
                                Vector3 respawnPos, Vector3 respawnFwd)
    {
        Vector3 flatFwd = Vector3.ProjectOnPlane(respawnFwd, Vector3.up).normalized;
        if (flatFwd == Vector3.zero) flatFwd = respawnFwd.normalized;

        Quaternion respawnRot = Quaternion.LookRotation(flatFwd, Vector3.up);

        bikeRoot.SetPositionAndRotation(respawnPos, respawnRot);
        rb.position = respawnPos;
        rb.rotation = respawnRot;
        BikeTrails.Clear(bikeRoot);

        rotator.localRotation = Quaternion.identity;
        lean.localRotation    = Quaternion.identity;

        float crashSpeed = Vector3.ProjectOnPlane(crashVelocity, Vector3.up).magnitude;
        StartCoroutine(ApplyMomentum(rb, flatFwd * (crashSpeed * speedCarryOver)));
    }

    private IEnumerator ApplyMomentum(Rigidbody rb, Vector3 velocity)
    {
        yield return new WaitForFixedUpdate();

        if (rb == null) yield break;
        rb.linearVelocity  = velocity;
        rb.angularVelocity = Vector3.zero;
    }

    private IEnumerator InvulnerabilityTimer(RagdollActivator ragdoll)
    {
        ragdoll.IsInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        ragdoll.IsInvulnerable = false;
    }
}
}
