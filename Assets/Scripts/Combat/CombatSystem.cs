using MotoSquid.Bike;
using MotoSquid.Rider;
using UnityEngine;
using UnityEngine.Events;

namespace MotoSquid.Combat
{
public class CombatSystem : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<CombatSystem> _allInstances = new();
    public static System.Collections.Generic.IReadOnlyList<CombatSystem> AllInstances => _allInstances;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearInstances() => _allInstances.Clear();

    [Header("Identity")]
    public bool isPlayer = false;

    [Header("Health")]
    public int hitsToKnockOff = 3;
    public float hitRegenDelay    = 6f;
    public float hitRegenInterval = 4f;

    [Header("Attack Settings")]
    public float punchRange = 1.8f;
    public float kickRange  = 2.0f;
    public float punchCooldown = 0.6f;
    public float kickCooldown  = 0.9f;
    public float attackSideOffset = 1.1f;
    public float attackForwardReach  = 1.6f;   // Along our own heading, front to back
    public float attackVerticalReach = 1.5f;
    // Height above the bike's pivot that a blow is considered to land at. The pivot is at ground
    // level, so a fixed small lift put every impact around the wheels
    public float impactHeight = 1.15f;
    public float strikeDelay = 0.18f;          // Used only when there is no rig driver to ask

    [Header("Engagement")]
    public float lungeReachBonus = 1.5f;       // A swing this far short still pulls us in
    public float lungeForce = 220f;
    public float hitStopOnLand = 0.05f;        // 0 disables
    [Range(0f, 1f)] public float hitStopScale = 0.35f;
    public float targetPollInterval = 0.1f;

    [Header("Hit Reaction")]
    public float knockbackForce = 600f;

    [Header("Animation (optional)")]
    public BikeAnimationController bikeAnimController;
    public Animator riderAnimator;
    public CombatRigDriver rigDriver;
    public string punchLeftTrigger  = "PunchLeft";
    public string punchRightTrigger = "PunchRight";
    public string kickLeftTrigger   = "KickLeft";
    public string kickRightTrigger  = "KickRight";
    public string hitTrigger        = "Hit";        // Generic fallback when no direction fits
    public string hitFrontTrigger   = "HitFront";   // Directional reactions, named for the way the
    public string hitBackTrigger    = "HitBack";    // Body is THROWN by the knockback impulse
    public string hitLeftTrigger    = "HitLeft";    // Picked in ReceiveHit, in Rotator space
    public string hitRightTrigger   = "HitRight";
    public float punchTorsoInfluence = 0.6f;
    public float hitTorsoInfluence = 1f;
    public bool hitReleasesHands = true;
    public float hitReactionCooldown = 0.8f;
    public int punchLeftReleaseHand  = -1;
    public int punchRightReleaseHand = -1;
    public float kickSupportLegPin = 1f;

    [Header("References")]
    public RagdollActivator ragdollActivator;
    public Rigidbody bikeRigidbody;
    public MeleeWeaponHolder weaponHolder;
    public BikeAudioController audioController;

    [Header("Events")]
    public UnityEvent onAttack;
    public UnityEvent onHitLanded;
    public UnityEvent onHitReceived;
    public UnityEvent onKnockedOff;
    public System.Action onDealtKnockoff;
    // The UnityEvents above carry no arguments, so a listener cannot know WHERE anything happened.
    // Effects need that, and adding parameters to the existing events would break their wiring
    // Carries who was struck as well as where: both bikes are doing 80 m/s, so an impact left at a
    // world point is metres behind by the next frame
    public System.Action<Transform, Vector3, Vector3> onHitLandedAt;
    // Carries the weapon itself, not a point: a whoosh parented to the prop follows the real arc.
    // Raised only for an ARMED punch - a kick is a leg, and an empty fist has nothing to trail
    public System.Action<Transform, int>   onSwungAt;
    public int  CurrentHits  { get; private set; }
    public bool IsKnockedOff => ragdollActivator != null && ragdollActivator.IsRagdollActive;
    public float PunchRange    => weaponHolder != null ? weaponHolder.Reach(punchRange) : punchRange;
    public float PunchCooldown => weaponHolder != null ? weaponHolder.SwingCooldown(punchCooldown) : punchCooldown;
    public int KickReleaseSide => _kickReleaseSide;

    // Who a swing would reach right now, either side. Refreshed on targetPollInterval for HUD use
    public CombatSystem TargetInRange { get; private set; }
    private CombatSystem _lastShovedBy;
    private float                 _lastShoveTime = -999f;

    private float _punchTimer;
    private float _kickTimer;
    private float _timeSinceLastHit;   // Drives health regen
    private UnityEngine.Animations.Rigging.Rig _riderRig;
    private BikerRigReferences _rigRefs;
    private Transform _hipBone;
    private Vector3 _riderBaseLocalPos;
    private float _combatRelax;      // Full body kicks: 0 = rig in charge, 1 = clip fully showing
    private int _upperReleaseSide;   // Which hand leaves the bar during a punch, -1 left, +1 right, 0 neither (hits)
    private int _kickReleaseSide;    // Which leg leaves its peg during a kick, -1 left, +1 right
    private float _footRotWeightL = 1f, _footRotWeightR = 1f;   // The rig's authored ankle rotation weights
    private float _lastHitAnimTime = -999f;   // Gates hit reaction replays (see hitReactionCooldown)
    private UnityEngine.Animations.Rigging.TwoBoneIKConstraint _pinHandL, _pinHandR;
    private float _pinHandBaseL = 1f, _pinHandBaseR = 1f;
    private Transform _facing;

    private struct Swing { public int dir, hits; public float reach, knockback; }
    private Swing _swing;
    private float _swingTimer;
    private bool  _swingPending;
    private float _pollTimer;

    void OnEnable()  => _allInstances.Add(this);
    void OnDisable() => _allInstances.Remove(this);

    void Start()
    {
        if (ragdollActivator != null)
            ragdollActivator.onBikeReEnabled.AddListener(ResetHits);

        if (audioController == null) audioController = GetComponentInChildren<BikeAudioController>(true);
        if (weaponHolder == null)    weaponHolder    = GetComponentInChildren<MeleeWeaponHolder>(true);

        var player = GetComponent<BikeController>();
        var ai     = GetComponent<BikeAIController>();
        _facing = player != null && player.bikeReferences != null && player.bikeReferences.Rotator != null
            ? player.bikeReferences.Rotator
            : ai != null && ai.bikeReferences != null && ai.bikeReferences.Rotator != null
                ? ai.bikeReferences.Rotator
                : transform;

        if (bikeRigidbody == null)
            bikeRigidbody = player != null && player.bikeReferences != null ? player.bikeReferences.BikeRb
                          : ai     != null && ai.bikeReferences     != null ? ai.bikeReferences.BikeRb
                          : GetComponent<Rigidbody>();

        BindRider(riderAnimator, rigDriver);
    }

    public void BindRider(Animator rider, CombatRigDriver driver)
    {
        riderAnimator = rider;
        if (rigDriver != null) rigDriver.Struck -= OnRigStruck;
        if (driver != null) rigDriver = driver;
        if (rigDriver != null) rigDriver.Struck += OnRigStruck;
        if (riderAnimator == null) return;

            var rigBuilder = riderAnimator.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            _riderRig = rigBuilder != null && rigBuilder.layers.Count > 0
                ? rigBuilder.layers[0].rig
                : riderAnimator.GetComponentInChildren<UnityEngine.Animations.Rigging.Rig>();
            _rigRefs = riderAnimator.GetComponentInChildren<BikerRigReferences>();
            _hipBone = riderAnimator.isHuman ? riderAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;
            _riderBaseLocalPos = riderAnimator.transform.localPosition;
            if (_rigRefs != null)
            {
                if (_rigRefs.LeftLegRig  != null) _footRotWeightL = _rigRefs.LeftLegRig.data.targetRotationWeight;
                if (_rigRefs.RightLegRig != null) _footRotWeightR = _rigRefs.RightLegRig.data.targetRotationWeight;
            }
            else
            {
                // Authored path, locate the hand pins of the builder's Limb IK Rig
                foreach (var c in riderAnimator.GetComponentsInChildren<UnityEngine.Animations.Rigging.TwoBoneIKConstraint>(true))
                {
                    if (c.name == "Left Hand IK")  { _pinHandL = c; _pinHandBaseL = c.weight; }
                    if (c.name == "Right Hand IK") { _pinHandR = c; _pinHandBaseR = c.weight;     }

            }
        }
    }

    void Update()
    {
        if (riderAnimator == null) return;
        if (rigDriver != null) return;

        float kick    = MaxTaggedWeight("Combat");
        float punch   = MaxTaggedWeight("CombatUpper");
        float hitAnim = MaxTaggedWeight("CombatHit");
        _combatRelax = kick;   // LateUpdate re seats the rider by this amount (old rig path only)

        if (_rigRefs == null)
        {
            if (_pinHandL != null || _pinHandR != null)
            {
                float handKeepL = _upperReleaseSide < 0 ? 1f - punch : 1f;
                float handKeepR = _upperReleaseSide > 0 ? 1f - punch : 1f;
                if (hitReleasesHands)
                {
                    handKeepL = Mathf.Min(handKeepL, 1f - hitAnim);
                    handKeepR = Mathf.Min(handKeepR, 1f - hitAnim);
                }
                if (_pinHandL != null) _pinHandL.weight = _pinHandBaseL * handKeepL;
                if (_pinHandR != null) _pinHandR.weight = _pinHandBaseR * handKeepR;
            }
            else if (_riderRig != null)
            {
                _riderRig.weight = 1f - Mathf.Max(kick, Mathf.Max(punch, hitAnim));
            }
            return;
        }
        if (_riderRig != null) _riderRig.weight = 1f;   // Per constraint control below

        float body  = 1f - kick;
        float spine = 1f - Mathf.Max(kick, Mathf.Max(punch * punchTorsoInfluence, hitAnim * hitTorsoInfluence));
        float head  = 1f - Mathf.Max(kick, Mathf.Max(punch, hitAnim));
        SetConstraintWeight(_rigRefs.hipRig, body);

        float supportPin = Mathf.Lerp(body, 1f, kickSupportLegPin);
        bool leftKicks = _kickReleaseSide < 0;
        SetConstraintWeight(_rigRefs.LeftLegRig,  leftKicks ? body : supportPin);
        SetConstraintWeight(_rigRefs.RightLegRig, !leftKicks && _kickReleaseSide != 0 ? body : supportPin);
        SetFootRotationWeight(_rigRefs.LeftLegRig,  Mathf.Lerp(_footRotWeightL, 0f, leftKicks ? 0f : kick));
        SetFootRotationWeight(_rigRefs.RightLegRig, Mathf.Lerp(_footRotWeightR, 0f, !leftKicks && _kickReleaseSide != 0 ? 0f : kick));
        SetConstraintWeight(_rigRefs.spineRootRig, spine);
        SetConstraintWeight(_rigRefs.spineTipRig, spine);
        SetConstraintWeight(_rigRefs.headRig, head);
        float leftHand  = _upperReleaseSide < 0 ? 1f - punch : 1f;
        float rightHand = _upperReleaseSide > 0 ? 1f - punch : 1f;
        if (hitReleasesHands)
        {
            leftHand  = Mathf.Min(leftHand,  1f - hitAnim);
            rightHand = Mathf.Min(rightHand, 1f - hitAnim);
        }
        SetConstraintWeight(_rigRefs.LeftHandRig,  leftHand);
        SetConstraintWeight(_rigRefs.RightHandRig, rightHand);
    }

    private float MaxTaggedWeight(string tag)
    {
        float best = 0f;
        for (int layer = 0; layer < riderAnimator.layerCount; layer++)
        {
            float w = TaggedWeight(layer, tag);
            if (w > best) best = w;
        }
        return best;
    }

    private float TaggedWeight(int layer, string tag)
    {
        var current = riderAnimator.GetCurrentAnimatorStateInfo(layer);
        if (!riderAnimator.IsInTransition(layer)) return current.IsTag(tag) ? 1f : 0f;
        float t    = Mathf.Clamp01(riderAnimator.GetAnimatorTransitionInfo(layer).normalizedTime);
        float from = current.IsTag(tag) ? 1f : 0f;
        float to   = riderAnimator.GetNextAnimatorStateInfo(layer).IsTag(tag) ? 1f : 0f;
        return Mathf.Lerp(from, to, t);
    }

    private static void SetConstraintWeight(UnityEngine.Animations.Rigging.IRigConstraint c, float w)
    {
        if (c != null) c.weight = w;
    }

    private static void SetFootRotationWeight(UnityEngine.Animations.Rigging.TwoBoneIKConstraint c, float w)
    {
        if (c == null) return;
        var d = c.data;
        if (Mathf.Approximately(d.targetRotationWeight, w)) return;
        d.targetRotationWeight = w;
        c.data = d;
    }

    void LateUpdate()
    {
        if (riderAnimator == null) return;
        var root = riderAnimator.transform;
        root.localPosition = _riderBaseLocalPos;   // Undo last frame's re-seat shift
        if (_combatRelax <= 0f || _hipBone == null || _rigRefs == null || _rigRefs.hipTarget == null)
            return;

        Vector3 delta = _rigRefs.hipTarget.position - _hipBone.position;
        root.position += delta * _combatRelax;
    }

    void OnDestroy()
    {
        if (ragdollActivator != null)
            ragdollActivator.onBikeReEnabled.RemoveListener(ResetHits);
        if (rigDriver != null) rigDriver.Struck -= OnRigStruck;
    }
    void FixedUpdate()
    {
        _punchTimer = Mathf.Max(0f, _punchTimer - Time.fixedDeltaTime);
        _kickTimer  = Mathf.Max(0f, _kickTimer  - Time.fixedDeltaTime);

        if (_swingPending && _swingTimer >= 0f)
        {
            _swingTimer -= Time.fixedDeltaTime;
            if (_swingTimer <= 0f) ResolveSwing();
        }

        _pollTimer -= Time.fixedDeltaTime;
        if (_pollTimer <= 0f)
        {
            _pollTimer    = Mathf.Max(0.02f, targetPollInterval);
            TargetInRange = IsKnockedOff
                ? null
                : FindTarget(0, PunchRange + lungeReachBonus);
        }

        if (CurrentHits > 0 && !IsKnockedOff)
        {
            _timeSinceLastHit += Time.fixedDeltaTime;
            if (_timeSinceLastHit >= hitRegenDelay)
            {
                CurrentHits--;
                // Subsequent recoveries wait hitRegenInterval (not the full initial delay)
                _timeSinceLastHit = hitRegenDelay - Mathf.Max(0f, hitRegenInterval);
            }
        }
    }

    public bool TryPunch(int dir)
    {
        if (_punchTimer > 0f || IsKnockedOff) return false;
        _punchTimer = PunchCooldown;

        // Authored clip when the controller has the state, procedural IK swing as the fallback
        if (TriggerAnim(dir < 0 ? punchLeftTrigger : punchRightTrigger))
        {
            int bare = dir < 0 ? punchLeftReleaseHand : punchRightReleaseHand;
            _upperReleaseSide = weaponHolder != null ? weaponHolder.ReleaseSide(bare) : bare;
        }

        var whoosh = weaponHolder != null ? weaponHolder.SwingWhoosh : null;
        if (whoosh != null) audioController?.PlaySwingWhoosh(whoosh);
        else                audioController?.PlaySwingWhoosh();

        ArmSwing(dir, PunchRange,
                 weaponHolder != null ? weaponHolder.HitsPerSwing : 1,
                 weaponHolder != null ? weaponHolder.KnockbackMultiplier : 1f);

        if (weaponHolder != null && weaponHolder.HasWeapon && weaponHolder.PropTransform != null)
            onSwungAt?.Invoke(weaponHolder.PropTransform, dir);

        onAttack?.Invoke();
        return true;
    }

    public bool TryKick(int dir)
    {
        if (_kickTimer > 0f || IsKnockedOff) return false;
        _kickTimer = kickCooldown;

        // Authored clip when the controller has the state, procedural IK swing as the fallback
        if (TriggerAnim(dir < 0 ? kickLeftTrigger : kickRightTrigger))
            _kickReleaseSide = dir;   // Only the kicking leg leaves its peg

        audioController?.PlaySwingWhoosh();

        ArmSwing(dir, kickRange, 1, 1f);
        onAttack?.Invoke();
        return true;
    }

    public void ReceiveHit(Vector3 impulse) => ReceiveHit(impulse, null, 1);

    public void ReceiveHit(Vector3 impulse, CombatSystem instigator) =>
        ReceiveHit(impulse, instigator, 1);

    public void ReceiveHit(Vector3 impulse, CombatSystem instigator, int hits)
    {
        if (IsKnockedOff) return;
        if (ragdollActivator != null && ragdollActivator.IsInvulnerable) return;

        CurrentHits += Mathf.Max(1, hits);
        _timeSinceLastHit = 0f;   // Restart the regen clock on every hit taken

        var weaponImpact = instigator != null && instigator.weaponHolder != null
            ? instigator.weaponHolder.Impact : null;
        float strength = hitsToKnockOff > 0 ? (float)CurrentHits / hitsToKnockOff : 1f;
        if (weaponImpact != null) audioController?.PlayHitImpact(strength, weaponImpact);
        else                      audioController?.PlayHitImpact(strength);

        if (Time.time - _lastHitAnimTime >= hitReactionCooldown)
        {
            Vector3 localShove = _facing != null ? _facing.InverseTransformDirection(impulse)
                                                 : transform.InverseTransformDirection(impulse);
            string directional = Mathf.Abs(localShove.x) >= Mathf.Abs(localShove.z)
                ? (localShove.x > 0f ? hitRightTrigger : hitLeftTrigger)
                : (localShove.z > 0f ? hitFrontTrigger : hitBackTrigger);
            if (TriggerAnim(directional) || TriggerAnim(hitTrigger))
            {
                _lastHitAnimTime = Time.time;
            }
        }
        onHitReceived?.Invoke();
        Vector3 lateral = Vector3.ProjectOnPlane(impulse, transform.forward);
        lateral.y = 0f;
        bikeRigidbody?.AddForce(lateral, ForceMode.Impulse);

        // Remember who shoved us, so a resulting crash (into traffic/wall) can credit them
        if (instigator != null)
        {
            _lastShovedBy  = instigator;
            _lastShoveTime = Time.time;
        }

        if (CurrentHits >= hitsToKnockOff)
        {
            CurrentHits = 0;
            audioController?.PlayKnockOff();
            onKnockedOff?.Invoke();
            ragdollActivator?.ForceActivateRagdoll();
        }
    }

    public bool TryConsumeForcedCrash(float window, out CombatSystem instigator)
    {
        instigator = null;
        if (_lastShovedBy == null || Time.time - _lastShoveTime > window) return false;
        instigator     = _lastShovedBy;
        _lastShovedBy  = null;   // consume so it credits once
        return true;
    }

    public void ReportForcedCrash() => onDealtKnockoff?.Invoke();


    public void ResetHits() => CurrentHits = 0;

    public Transform Facing => _facing != null ? _facing : transform;

    // The one definition of what a swing can reach, so the AI cannot pick a target the swing itself
    // would then miss. dir is -1 left, +1 right, 0 either side. Walks the racer list rather than
    // running a physics query: six racers is cheaper than a broadphase sweep, and a sphere sitting
    // on a road could fill its collider buffer with scenery before it ever saw the opponent.
    public CombatSystem FindTarget(int dir, float reach)
    {
        Transform facing = Facing;
        Vector3   origin = transform.position;
        float     maxSide = attackSideOffset + reach;

        CombatSystem nearest = null;
        float nearestSq = float.MaxValue;

        for (int i = 0; i < _allInstances.Count; i++)
        {
            CombatSystem c = _allInstances[i];
            if (c == null || c == this || c.IsKnockedOff) continue;

            Vector3 to   = c.transform.position - origin;
            float   side = Vector3.Dot(to, facing.right);
            if (dir != 0 && side * dir <= 0f) continue;
            if (Mathf.Abs(side) > maxSide) continue;
            if (Mathf.Abs(Vector3.Dot(to, facing.forward)) > attackForwardReach) continue;
            if (Mathf.Abs(to.y) > attackVerticalReach) continue;

            float sq = to.sqrMagnitude;
            if (sq < nearestSq) { nearestSq = sq; nearest = c; }
        }
        return nearest;
    }

    // The swing is only armed here. Racers close metres during a wind up, so who it lands on is
    // decided at the contact frame instead (OnRigStruck, or strikeDelay with no rig driver).
    private void ArmSwing(int dir, float reach, int hits, float knockbackScale)
    {
        _swing = new Swing { dir = dir, hits = hits, reach = reach, knockback = knockbackScale };
        _swingPending = true;
        _swingTimer   = rigDriver != null ? -1f : strikeDelay;
        Lunge(dir, reach);
    }

    private void ResolveSwing()
    {
        if (!_swingPending) return;
        _swingPending = false;

        CombatSystem target = FindTarget(_swing.dir, _swing.reach);
        if (target == null) return;

        Vector3 knock = Facing.right * _swing.dir * knockbackForce * _swing.knockback;
        target.ReceiveHit(knock, this, _swing.hits);
        onHitLanded?.Invoke();
        // Between the two riders rather than between the two pivots, and lifted to rider height
        Vector3 here  = Facing != null ? Facing.position : transform.position;
        Vector3 there = target.Facing != null ? target.Facing.position : target.transform.position;
        onHitLandedAt?.Invoke(target.Facing != null ? target.Facing : target.transform,
                              Vector3.Lerp(here, there, 0.5f) + Vector3.up * impactHeight,
                              knock.normalized);
        if (hitStopOnLand > 0f && HitStopController.Instance != null)
            HitStopController.Instance.Freeze(hitStopOnLand, hitStopScale);
        if (target.IsKnockedOff) onDealtKnockoff?.Invoke();
    }

    // A swing that would just fall short pulls the bike in, so trading blows does not depend on
    // holding an exact gap at 200 km/h
    private void Lunge(int dir, float reach)
    {
        if (lungeForce <= 0f || lungeReachBonus <= 0f || bikeRigidbody == null) return;
        if (FindTarget(dir, reach) != null) return;

        CombatSystem near = FindTarget(dir, reach + lungeReachBonus);
        if (near == null) return;

        Vector3 to = near.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        bikeRigidbody.AddForce(to.normalized * lungeForce, ForceMode.Impulse);
    }

    private void OnRigStruck(CombatRigDriver.Action a)
    {
        if (IsAttack(a)) ResolveSwing();
    }

    static bool IsAttack(CombatRigDriver.Action a)
    {
        return a == CombatRigDriver.Action.MeleeLeft
            || a == CombatRigDriver.Action.MeleeRight
            || a == CombatRigDriver.Action.KickLeft
            || a == CombatRigDriver.Action.KickRight;
    }

    static bool IsReaction(CombatRigDriver.Action a)
    {
        return a == CombatRigDriver.Action.HitFront
            || a == CombatRigDriver.Action.HitBack
            || a == CombatRigDriver.Action.HitLeft
            || a == CombatRigDriver.Action.HitRight;
    }

    CombatRigDriver.Action RigActionFor(string trigger)
    {
        if (trigger == punchLeftTrigger)  return CombatRigDriver.Action.MeleeLeft;
        if (trigger == punchRightTrigger) return CombatRigDriver.Action.MeleeRight;
        if (trigger == kickLeftTrigger)   return CombatRigDriver.Action.KickLeft;
        if (trigger == kickRightTrigger)  return CombatRigDriver.Action.KickRight;
        if (trigger == hitFrontTrigger)   return CombatRigDriver.Action.HitFront;
        if (trigger == hitBackTrigger)    return CombatRigDriver.Action.HitBack;
        if (trigger == hitLeftTrigger)    return CombatRigDriver.Action.HitLeft;
        if (trigger == hitRightTrigger)   return CombatRigDriver.Action.HitRight;
        return CombatRigDriver.Action.None;
    }

    private bool TriggerAnim(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName)) return false;

        if (rigDriver != null)
        {
            var action = RigActionFor(triggerName);
            if (action != CombatRigDriver.Action.None)
            {
                // A reaction interrupts whatever is playing; an attack waits its turn
                if (IsReaction(action)) rigDriver.Hit(action);
                else rigDriver.Trigger(action);
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform facing = Facing;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, facing.rotation, Vector3.one);

        DrawReach(punchRange, new Color(1f, 0.5f, 0f, 0.35f));
        DrawReach(kickRange,  new Color(0f, 0.5f, 1f, 0.25f));
        DrawReach(Mathf.Max(punchRange, kickRange) + lungeReachBonus,
                  new Color(1f, 1f, 1f, 0.12f));
        Gizmos.matrix = Matrix4x4.identity;
    }

    void DrawReach(float reach, Color c)
    {
        Gizmos.color = c;
        var size = new Vector3((attackSideOffset + reach) * 2f,
                               attackVerticalReach * 2f,
                               attackForwardReach * 2f);
        Gizmos.DrawCube(Vector3.zero, size);
    }
#endif
}
}
