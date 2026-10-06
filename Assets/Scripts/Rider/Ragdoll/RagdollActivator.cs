using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace MotoSquid.Rider
{
public class RagdollActivator : MonoBehaviour
{
    public BikeController   bikeController;
    public BikeAIController bikeAIRhiannon;

    public CameraController cameraControllerRhiannon;
    public GameObject dummyBikePrefab;
    public GameObject characterRagdollPrefab;
    public Animator characterAnimator;
    public float impactThreshold = 10f;

    // A crash at racing speed hands the rider's 15 jointed bodies 100+ m/s in a single step. The
    // solver cannot satisfy the joints against that, so they stretch and the skinned mesh warps with
    // them. None of the speed past this reads on screen, it only tears the rig apart
    public float maxRagdollSpeed         = 35f;
    public int   ragdollSolverIterations = 20;
    public float jointProjectionDistance = 0.05f;
    public float jointProjectionAngle    = 15f;
    public float crashSpeedThreshold = 15f;
    public float forcedCrashWindow = 2f;
    public LayerMask forcedCrashLayers;
    public bool IgnoreBottomCollision = true;
    public LayerMask crashIgnoreLayers;
    public UnityEvent onRagdollActivated;
    public UnityEvent onBikeReEnabled;

    private GameObject RiderRagdollPrefab
    {
        get
        {
            if (characterAnimator == null) return characterRagdollPrefab;
            var rider = characterAnimator.GetComponentInParent<RiderRagdoll>(true);
            return rider != null && rider.ragdollPrefab != null
                ? rider.ragdollPrefab
                : characterRagdollPrefab;
        }
    }

    private Transform LeanTransform =>
        bikeController != null
            ? bikeController.bikeReferences.LeanTransform
            : bikeAIRhiannon.bikeReferences.LeanTransform;

    private Collider BikeCollider =>
        bikeController != null
            ? bikeController.bikeReferences.collider
            : bikeAIRhiannon.bikeReferences.collider;

    private Rigidbody BikeRb =>
        bikeController != null
            ? bikeController.bikeReferences.BikeRb
            : bikeAIRhiannon.bikeReferences.BikeRb;

    private bool isRagdollActivated = false;
    private GameObject bikeRagdollInstance;
    public GameObject characterRagdollInstance { get; private set; }
    private Transform hipTransform;
    private Vector3 _preCollisionVelocity;

    private Renderer[] _cachedRenderers;

    public Vector3 CrashVelocity { get; private set; }

    public bool IsInvulnerable { get; set; }

    public bool IsRagdollActive => isRagdollActivated;

    void FixedUpdate()
    {

        if (!isRagdollActivated && BikeRb != null)
            _preCollisionVelocity = BikeRb.linearVelocity;
    }

    void Start()
    {
        if (bikeController == null && bikeAIRhiannon == null)
        {
            enabled = false;
            return;
        }

        if (onRagdollActivated == null) onRagdollActivated = new UnityEvent();
        if (onBikeReEnabled    == null) onBikeReEnabled    = new UnityEvent();

        onRagdollActivated.AddListener(SetCameraTargetToRagdoll);
        onBikeReEnabled.AddListener(ResetCameratoBike);

    }

    void OnCollisionEnter(Collision collision)
    {
        if (isRagdollActivated) return;
        if (IsInvulnerable) return;
        if (collision.contactCount == 0) return;
        if (crashIgnoreLayers != 0 && ((1 << collision.gameObject.layer) & crashIgnoreLayers) != 0)
            return;

        if (collision.gameObject.GetComponentInParent<RagdollMarker>() != null)
            return;

        Vector3 bikeUp   = LeanTransform.up;
        var     contacts = collision.contacts;

        if (IgnoreBottomCollision && !HasSideImpact(contacts, bikeUp))
            return;

        if (crashSpeedThreshold > 0f && collision.relativeVelocity.magnitude < crashSpeedThreshold)
            return;

        // Horizontal only impulse: strip the world up component
        Vector3 lateralImpulse = Vector3.ProjectOnPlane(collision.impulse, Vector3.up);
        if (lateralImpulse.magnitude / BikeRb.mass > impactThreshold)
        {
            // If another racer shoved us into this crash within the window, credit them (boost).
            CreditForcedCrashInstigator(collision.gameObject);
            Debug.Log($"[RagdollActivator] Crash triggered by '{collision.gameObject.name}' " +
                      $"(layer {LayerMask.LayerToName(collision.gameObject.layer)}) " +
                      $"lateral Δv={lateralImpulse.magnitude / BikeRb.mass:F1} m/s " +
                      $"threshold={impactThreshold}" +
                      (bikeAIRhiannon != null && bikeAIRhiannon.aiLogic != null
                          ? $" | {bikeAIRhiannon.aiLogic.PlannerDebug(collision.collider)}" : ""),
                      collision.gameObject);
            ActivateRagdoll();
        }
    }

    // Ground and kerbs push up from under the bike. A car or wall pushes from beside it, but leaning
    // tilts bikeUp far enough that a purely sideways normal used to read as "from below" and the
    // whole crash was thrown away, so the contact has to be under the bike as well, and one real
    // side impact is enough even when it arrives alongside ground contacts
    bool HasSideImpact(ContactPoint[] contacts, Vector3 bikeUp)
    {
        Vector3 centre = BikeCollider.bounds.center;
        for (int i = 0; i < contacts.Length; i++)
        {
            float up = Mathf.Max(Vector3.Dot(contacts[i].normal, bikeUp),
                                 Vector3.Dot(contacts[i].normal, Vector3.up));
            if (up <= 0.3f || Vector3.Dot(contacts[i].point - centre, Vector3.up) >= 0f)
                return true;
        }
        return false;
    }

    private CombatSystem _combat;

    void CreditForcedCrashInstigator(GameObject hitObject)
    {
        // Only count crashes into traffic (not walls/ground/other racers)
        int mask = forcedCrashLayers.value;
        if (mask == 0)
        {
            int trafficLayer = LayerMask.NameToLayer("Traffic");
            if (trafficLayer >= 0) mask = 1 << trafficLayer;
        }
        if (mask != 0 && ((1 << hitObject.layer) & mask) == 0) return;

        if (_combat == null)
            _combat = GetComponent<CombatSystem>()
                   ?? GetComponentInParent<CombatSystem>()
                   ?? GetComponentInChildren<CombatSystem>();
        if (_combat == null) return;

        if (_combat.TryConsumeForcedCrash(forcedCrashWindow, out var instigator) && instigator != null)
            instigator.ReportForcedCrash();
    }

    void ActivateRagdoll()
    {
        var ragdollPrefab = RiderRagdollPrefab;
        if (dummyBikePrefab == null || ragdollPrefab == null)
        {
            Debug.LogError("[RagdollActivator] dummyBikePrefab is unassigned, or the active rider has " +
                           "no RiderRagdoll prefab and characterRagdollPrefab is empty.", this);
            return;
        }

        isRagdollActivated = true;

        Debug.Log($"[RagdollActivator] ActivateRagdoll on '{name}'", this);

        CrashVelocity = _preCollisionVelocity;

        bikeRagdollInstance      = Instantiate(dummyBikePrefab,         LeanTransform.position, LeanTransform.rotation);
        characterRagdollInstance = Instantiate(ragdollPrefab,            LeanTransform.position, LeanTransform.rotation);

        // Mark both instances so other bikes' OnCollisionEnter skips them
        bikeRagdollInstance.AddComponent<RagdollMarker>();
        characterRagdollInstance.AddComponent<RagdollMarker>();

        // Match ragdoll bone rotations to animated character
        Animator ragdollAnimator = characterRagdollInstance.GetComponent<Animator>();
        if (ragdollAnimator != null && characterAnimator != null)
        {
            foreach (HumanBodyBones bone in (HumanBodyBones[])System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                Transform characterBone = characterAnimator.GetBoneTransform(bone);
                Transform ragdollBone   = ragdollAnimator.GetBoneTransform(bone);
                if (characterBone != null && ragdollBone != null)
                    ragdollBone.rotation = characterBone.rotation;
            }
        }

        // Both spawn at the same position and rotation, so the rider's 15 colliders start deep
        // inside the bike's. Nothing in the project's collision matrix excludes them, and PhysX
        // resolves that overlap by firing them apart, which is the rider "shooting off"
        IgnoreCollisionsBetween(bikeRagdollInstance, characterRagdollInstance);

        StabiliseRagdoll(characterRagdollInstance);

        // Inherit velocity
        Vector3 vel    = BikeRb.linearVelocity;
        Vector3 angVel = BikeRb.angularVelocity;

        foreach (Rigidbody rb in bikeRagdollInstance.GetComponentsInChildren<Rigidbody>())
        { rb.linearVelocity = vel; rb.angularVelocity = angVel; }

        // Linear only: the rider was travelling with the bike, not spinning. Copying the bike's
        // angular velocity onto all 15 jointed bodies makes each spin about its own centre of mass
        // and tear against its joints
        Vector3 riderVel = Vector3.ClampMagnitude(vel, maxRagdollSpeed);
        foreach (Rigidbody rb in characterRagdollInstance.GetComponentsInChildren<Rigidbody>())
            rb.linearVelocity = riderVel;

        // Stop the ghost body from drifting away from the crash site while the ragdoll plays
        BikeRb.linearVelocity  = Vector3.zero;
        BikeRb.angularVelocity = Vector3.zero;

        if (bikeController != null)
            bikeController.canAccelerate = false;
        else if (bikeAIRhiannon != null)
            bikeAIRhiannon.canAccelerate = false;

        // Taken now, not in Start, switching rider or equipping a weapon destroys and respawns
        // renderers, and a Start time list goes stale. Destroyed entries still compare != null
        // only until the end of that frame, so the null check has to stay too.
        _cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (var r in _cachedRenderers) if (r != null) r.enabled = false;
        BikeCollider.enabled = false;

        onRagdollActivated.Invoke();
    }

    // Projection is what pulls a separated joint back together; without it a large impulse simply
    // stretches the chain and the skinned mesh follows. The iteration count is what lets the solver
    // converge on 15 bodies at once, and maxDepenetrationVelocity stops overlapping colliders
    // firing apart, the rider and bike ragdolls spawn inside each other
    void StabiliseRagdoll(GameObject ragdoll)
    {
        if (ragdoll == null) return;

        foreach (CharacterJoint joint in ragdoll.GetComponentsInChildren<CharacterJoint>())
        {
            joint.enableProjection   = true;
            joint.projectionDistance = jointProjectionDistance;
            joint.projectionAngle    = jointProjectionAngle;
        }

        foreach (Rigidbody rb in ragdoll.GetComponentsInChildren<Rigidbody>())
        {
            rb.solverIterations         = ragdollSolverIterations;
            rb.solverVelocityIterations = ragdollSolverIterations;
            rb.maxDepenetrationVelocity = maxRagdollSpeed;
        }
    }

    static void IgnoreCollisionsBetween(GameObject a, GameObject b)
    {
        var aCols = a.GetComponentsInChildren<Collider>();
        var bCols = b.GetComponentsInChildren<Collider>();
        foreach (var ac in aCols)
        {
            if (ac == null || !ac.enabled) continue;
            foreach (var bc in bCols)
            {
                if (bc == null || !bc.enabled) continue;
                Physics.IgnoreCollision(ac, bc);
            }
        }
    }

    public void ReEnableBike()
    {
        if (!isRagdollActivated) return;

        Destroy(bikeRagdollInstance);
        Destroy(characterRagdollInstance);

        if (_cachedRenderers != null)
            foreach (var r in _cachedRenderers) if (r != null) r.enabled = true;
        BikeCollider.enabled = true;
        BikeTrails.Clear(transform);

        if (bikeController != null)
        {
            bikeController.canAccelerate = true;
            var playerBikeAudio = bikeController.GetComponentInChildren<BikeAudioController>();
            if (playerBikeAudio != null) playerBikeAudio.SilenceEngine();
            bikeController.bikeReferences.LeanTransform.localRotation = Quaternion.identity;

            var rb = bikeController.bikeReferences.BikeRb;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            bikeAIRhiannon.canAccelerate = true;
            var aiBikeAudio = bikeAIRhiannon.GetComponentInChildren<BikeAudioController>();
            if (aiBikeAudio != null) aiBikeAudio.SilenceEngine();
            bikeAIRhiannon.bikeReferences.LeanTransform.localRotation = Quaternion.identity;

            var rb = bikeAIRhiannon.bikeReferences.BikeRb;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            bikeAIRhiannon.aiLogic?.ResetTracking();
        }

        isRagdollActivated = false;
        onBikeReEnabled.Invoke();
    }

    public void SetCameraTargetToRagdoll()
    {
        if (cameraControllerRhiannon == null) return;
        if (characterRagdollInstance == null) return;
        if (!characterRagdollInstance.TryGetComponent<Animator>(out var ragdollAnimator)) return;
        hipTransform = ragdollAnimator.GetBoneTransform(HumanBodyBones.Hips);
        cameraControllerRhiannon.SetCameraTarget(hipTransform, hipTransform);
    }

    public void ResetCameratoBike()
    {
        if (cameraControllerRhiannon == null) return;
        cameraControllerRhiannon.ResetCameraTarget();
    }

    public void ForceActivateRagdoll()
    {
        if (!isRagdollActivated)
        {
            Debug.Log($"[RagdollActivator] ForceActivateRagdoll called on '{name}'.", this);
            ActivateRagdoll();
        }
    }

    void OnDestroy()
    {
        onRagdollActivated?.RemoveListener(SetCameraTargetToRagdoll);
        onBikeReEnabled?.RemoveListener(ResetCameratoBike);
    }
}
}
