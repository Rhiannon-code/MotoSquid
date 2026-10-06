using MotoSquid.Rider;
using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Combat
{
    [DefaultExecutionOrder(100)]
    public class CombatRigDriver : MonoBehaviour
    {
        public enum Action { None, MeleeLeft, MeleeRight, KickLeft, KickRight,
                             HitFront, HitBack, HitLeft, HitRight,
                             LookBackLeft, LookBackRight }
        public enum Body { ActingLimb, Whole, UpperOnly }
        public enum Phase { Idle, Winding, Held, Striking }
        public enum Limb { LeftHand, RightHand, LeftFoot, RightFoot }

        [Serializable]
        public class ActionSet
        {
            public FullPose start;   // Wind-up, null goes straight to end
            public FullPose loop;    // Charge hold
            public FullPose end;     // The action through to recovery
            public Limb acting = Limb.LeftHand;
            public Body body = Body.ActingLimb;
            [Range(0f, 1f)] public float contactAt = 0.35f;   // Where in `end` the limb arrives
        }

        [Header("Rig")]
        public BikerRigReferences rideRefs;
        public CombatRigReferences combatRefs;
        public CombatTuning tuning;

        [Header("Actions")]
        public ActionSet meleeLeft = new ActionSet { acting = Limb.LeftHand };
        public ActionSet meleeRight = new ActionSet { acting = Limb.LeftHand };
        public ActionSet kickLeft = new ActionSet { acting = Limb.LeftFoot };
        public ActionSet kickRight = new ActionSet { acting = Limb.RightFoot };

        [Header("Reactions")]
        public ActionSet hitFront = new ActionSet { body = Body.Whole };
        public ActionSet hitBack = new ActionSet { body = Body.Whole };
        public ActionSet hitLeft = new ActionSet { body = Body.Whole };
        public ActionSet hitRight = new ActionSet { body = Body.Whole };

        [Header("Look behind: hold to look, release to return")]
        public ActionSet lookBackLeft = new ActionSet { body = Body.UpperOnly };
        public ActionSet lookBackRight = new ActionSet { body = Body.UpperOnly };

        [Header("State")]
        public Action action = Action.None;
        public Phase phase = Phase.Idle;

        [Header("Diagnostics")]
        public string dbgPose = ",";
        public float dbgTime;
        public int dbgBonesMapped;
        public float dbgPoseSpread;

        float timer, blend;
        float lookBackFor;              // >0 while a game driven look-behind is holding
        float switching = 1f;           // 1 = settled, below that, crossing from switchFrom
        Quaternion[] written, switchFrom;
        readonly float[] limbWeight = { 1f, 1f, 1f, 1f };
        bool buttonDown;
        bool struck;                    // Contact reported once per action
        int[] map;                      // Override index -> pose bone index
        Quaternion[] rest;              // Each mapped bone at the clip's first frame
        FullPose mappedTo;

        public bool Busy { get { return phase != Phase.Idle || blend > 0.001f; } }

        public bool Ready
        {
            get { return rideRefs != null && tuning != null && combatRefs != null && combatRefs.IsComplete; }
        }

        void OnEnable() { Restore(); }
        void OnDisable() { Restore(); }

        public void Restore()
        {
            blend = 0f; timer = 0f; lookBackFor = 0f;
            switching = 1f; written = null; switchFrom = null;
            for (int i = 0; i < limbWeight.Length; i++) limbWeight[i] = 1f;
            phase = Phase.Idle; action = Action.None; buttonDown = false;
            dbgPose = "-"; dbgTime = 0f;
            FadeRide(1f, null, Body.ActingLimb);
            if (combatRefs != null)
            {
                if (combatRefs.rig != null) combatRefs.rig.weight = 0f;
                ApplyOverrideWeights(0f, null);
            }
            SetAnchors(null, 0f);
        }

        public void Press(Action which)
        {
            if (which == Action.None || !Ready) return;
            if (phase != Phase.Idle && !tuning.freeze && !tuning.repeat) return;
            if (Set(which) == null) return;
            BeginSwitch();
            action = which;
            timer = 0f;
            buttonDown = true;
            lookBackFor = 0f;
            struck = false;
            phase = Begin(Set(which));
            if (ActionStarted != null) ActionStarted(which);
        }

        Phase Begin(ActionSet set)
        {
            if (set.start != null) return Phase.Winding;
            if (set.loop != null && buttonDown) return Phase.Held;
            return Phase.Striking;
        }

        public void Release()
        {
            buttonDown = false;
            if (phase == Phase.Held) { phase = Phase.Striking; timer = 0f; }
        }

        public void Trigger(Action which) { Press(which); buttonDown = false; }
        public event System.Action<Action> ActionStarted;

        // Fires the frame the acting limb reaches the target, so a game side hit test can run then
        // rather than on the button press a wind up earlier
        public event System.Action<Action> Struck;

        public void LookBack(bool left, float seconds)
        {
            if (phase != Phase.Idle) return;
            Press(left ? Action.LookBackLeft : Action.LookBackRight);
            lookBackFor = Mathf.Max(0.01f, seconds);
        }

        public void Hit(Action which)
        {
            if (!Ready || which == Action.None) return;
            if (Set(which) == null) return;
            BeginSwitch();
            action = which;
            timer = 0f;
            buttonDown = false;
            lookBackFor = 0f;
            struck = false;
            phase = Begin(Set(which));
            if (ActionStarted != null) ActionStarted(which);
        }

        public void HitFrom(Vector3 direction)
        {
            Vector3 v = transform.InverseTransformDirection(direction);
            if (Mathf.Abs(v.z) >= Mathf.Abs(v.x)) Hit(v.z >= 0f ? Action.HitFront : Action.HitBack);
            else Hit(v.x >= 0f ? Action.HitRight : Action.HitLeft);
        }

        void BeginSwitch()
        {
            if (blend <= 0.001f || written == null) { switching = 1f; return; }
            if (switchFrom == null || switchFrom.Length != written.Length)
                switchFrom = new Quaternion[written.Length];
            Array.Copy(written, switchFrom, written.Length);
            switching = 0f;
        }

        ActionSet Set(Action a)
        {
            switch (a)
            {
                case Action.MeleeLeft: return meleeLeft;
                case Action.MeleeRight: return meleeRight;
                case Action.KickLeft: return kickLeft;
                case Action.KickRight: return kickRight;
                case Action.HitFront: return hitFront;
                case Action.HitBack: return hitBack;
                case Action.HitLeft: return hitLeft;
                case Action.HitRight: return hitRight;
                case Action.LookBackLeft: return lookBackLeft;
                case Action.LookBackRight: return lookBackRight;
                default: return null;
            }
        }

        float Speed(Action a)
        {
            return a == Action.KickLeft || a == Action.KickRight ? tuning.kickSpeed : tuning.meleeSpeed;
        }

        void Update()
        {
            if (!Ready) return;

            Advance();

            float want = Wanted();
            float rate = want > blend ? tuning.blendIn : tuning.blendOut;
            blend = Mathf.MoveTowards(blend, want, Time.deltaTime / Mathf.Max(0.0001f, rate));
            float shaped = Mathf.Clamp01(tuning.blendShape.Evaluate(blend));

            if (switching < 1f)
                switching = Mathf.MoveTowards(switching, 1f,
                                              Time.deltaTime / Mathf.Max(0.0001f, tuning.switchBlend));

            ActionSet set = Busy ? Set(action) : null;
            FadeRide(1f - shaped, set != null ? (Limb?)set.acting : null,
                     set != null ? set.body : Body.ActingLimb);
            combatRefs.rig.weight = shaped;

            if (shaped <= 0.001f || set == null)
            {
                ApplyOverrideWeights(0f, null);
                SetAnchors(null, 0f);
                dbgPose = "-"; dbgTime = 0f;
                return;
            }

            FullPose pose; float t;
            Current(set, out pose, out t);
            if (pose == null) return;

            ApplyPose(pose, t, shaped, set.acting, set.body);
            ApplyOverrideWeights(1f, set.acting, set.body);
            SetAnchors(set.body == Body.ActingLimb ? (Limb?)set.acting : null, 1f);
        }

        float Wanted()
        {
            if (phase == Phase.Idle) return 0f;
            if (phase != Phase.Striking || tuning.freeze) return 1f;

            var set = Set(action);
            var end = set != null ? set.end : null;
            if (end == null || tuning.releaseAt >= 1f) return 1f;
            return timer >= end.duration * tuning.releaseAt ? 0f : 1f;
        }

        void FadeRide(float w, Limb? acting, Body body)
        {
            if (rideRefs == null) return;
            bool clipTakesIdle = body == Body.Whole ||
                                 (body == Body.ActingLimb && tuning != null && tuning.anchoredLimbPose > 0f);

            if (rideRefs.hipRig != null) rideRefs.hipRig.weight = 1f;
            if (rideRefs.spineRootRig != null) rideRefs.spineRootRig.weight = w;
            if (rideRefs.spineTipRig != null) rideRefs.spineTipRig.weight = w;
            if (rideRefs.headRig != null) rideRefs.headRig.weight = w;

            float rate = Time.deltaTime / Mathf.Max(0.0001f, tuning != null ? tuning.switchBlend : 0.09f);
            SetLimbRig(0, rideRefs.LeftHandRig, Limb.LeftHand, acting, w, clipTakesIdle, rate);
            SetLimbRig(1, rideRefs.RightHandRig, Limb.RightHand, acting, w, clipTakesIdle, rate);
            SetLimbRig(2, rideRefs.LeftLegRig, Limb.LeftFoot, acting, w, clipTakesIdle, rate);
            SetLimbRig(3, rideRefs.RightLegRig, Limb.RightFoot, acting, w, clipTakesIdle, rate);
        }

        void SetLimbRig(int slot, IRigConstraint rig, Limb which, Limb? acting, float w,
                        bool clipTakesIdle, float rate)
        {
            if (rig == null) return;
            float target = which == acting || clipTakesIdle ? w : 1f;
            limbWeight[slot] = Mathf.MoveTowards(limbWeight[slot], target, rate);
            rig.weight = limbWeight[slot];
        }

        void Advance()
        {
            if (phase == Phase.Idle) return;

            if (lookBackFor > 0f)
            {
                lookBackFor -= Time.deltaTime;
                if (lookBackFor <= 0f) Release();
            }

            if (phase == Phase.Striking && tuning.freeze) return;

            var set = Set(action);
            if (set == null) { phase = Phase.Idle; return; }
            timer += Time.deltaTime * Mathf.Max(0.01f, Speed(action));

            if (phase == Phase.Striking && !struck)
            {
                float contact = set.end != null ? set.end.duration * Mathf.Clamp01(set.contactAt) : 0f;
                if (timer >= contact)
                {
                    struck = true;
                    if (Struck != null) Struck(action);
                }
            }

            switch (phase)
            {
                case Phase.Winding:
                    if (set.start == null || timer >= set.start.duration)
                    {
                        if (buttonDown && set.loop != null) { phase = Phase.Held; timer = 0f; }
                        else { phase = Phase.Striking; timer = 0f; }
                    }
                    break;
                case Phase.Held:
                    if (!buttonDown) { phase = Phase.Striking; timer = 0f; }
                    break;
                case Phase.Striking:
                    if (set.end != null && timer < set.end.duration) break;
                    if (tuning.repeat) { phase = Begin(set); timer = 0f; }
                    // action is kept: the pose has to stay readable while blend fades it out
                    else { phase = Phase.Idle; buttonDown = false; }
                    break;
            }
        }

        void Current(ActionSet set, out FullPose pose, out float t)
        {
            switch (phase)
            {
                case Phase.Winding:
                    pose = set.start;
                    t = pose == null ? 0f : Mathf.Clamp01(timer / Mathf.Max(0.0001f, pose.duration));
                    return;
                case Phase.Held:
                    pose = set.loop != null ? set.loop : set.start;
                    t = pose == null ? 0f : Mathf.Repeat(timer / Mathf.Max(0.0001f, pose.duration), 1f);
                    return;
                default:
                    pose = set.end != null ? set.end : set.start;
                    if (pose == null) { t = 0f; return; }
                    t = tuning.freeze ? Mathf.Clamp01(tuning.freezeAt)
                                      : Mathf.Clamp01(timer / Mathf.Max(0.0001f, pose.duration));
                    return;
            }
        }

        void ApplyPose(FullPose pose, float t, float blendNow, Limb? acting, Body body)
        {
            if (mappedTo != pose) Remap(pose);

            var ovr = combatRefs.overrides;
            if (written == null || written.Length != ovr.Length)
            {
                written = new Quaternion[ovr.Length];
                switching = 1f;
            }

            for (int i = 0; i < ovr.Length; i++)
            {
                int b = map[i];
                if (b < 0 || ovr[i].constraint == null) continue;
                Quaternion r; Vector3 p;
                if (!pose.Sample(b, t, out r, out p)) continue;

                float amt = tuning.Amount(ovr[i].group) * blendNow;
                Limb? limb = LimbOf(ovr[i].group);
                if (body == Body.ActingLimb && limb != null && limb != acting) amt *= tuning.anchoredLimbPose;
                if (amt < 0.999f) r = Quaternion.Slerp(rest[i], r, amt);
                if (switching < 1f && switchFrom != null) r = Quaternion.Slerp(switchFrom[i], r, switching);

                written[i] = r;
                ovr[i].constraint.data.rotation = r.eulerAngles;
            }

            dbgPose = pose.name;
            dbgTime = t;
        }

        void Remap(FullPose pose)
        {
            var ovr = combatRefs.overrides;
            if (map == null || map.Length != ovr.Length)
            {
                map = new int[ovr.Length];
                rest = new Quaternion[ovr.Length];
            }
            int mapped = 0;

            for (int i = 0; i < ovr.Length; i++)
            {
                map[i] = pose.IndexOf(ovr[i].bonePath);
                rest[i] = Quaternion.identity;
                if (map[i] < 0) continue;
                mapped++;
                Vector3 rp;
                pose.Sample(map[i], 0f, out rest[i], out rp);
            }

            mappedTo = pose;
            dbgBonesMapped = mapped;

            float spread = 0f;
            for (int i = 0; i < ovr.Length; i++)
            {
                if (map[i] < 0) continue;
                Quaternion a, b; Vector3 pa, pb;
                if (pose.Sample(map[i], 0f, out a, out pa) && pose.Sample(map[i], 0.5f, out b, out pb))
                    spread = Mathf.Max(spread, Quaternion.Angle(a, b));
            }
            dbgPoseSpread = spread;
        }

        void ApplyOverrideWeights(float w, Limb? acting, Body body = Body.ActingLimb)
        {
            if (combatRefs == null) return;
            var ovr = combatRefs.overrides;

            for (int i = 0; i < ovr.Length; i++)
            {
                if (ovr[i].constraint == null) continue;
                if (tuning == null) { ovr[i].constraint.weight = w; continue; }

                Limb? limb = LimbOf(ovr[i].group);
                bool release = limb != null &&
                               (body == Body.UpperOnly ||
                                (body == Body.ActingLimb && limb != acting && tuning.anchoredLimbPose <= 0f));
                ovr[i].constraint.weight = release ? 0f : w * tuning.Weight(ovr[i].group);
            }
        }

        static Limb? LimbOf(CombatTuning.Group g)
        {
            switch (g)
            {
                case CombatTuning.Group.LeftArm: return Limb.LeftHand;
                case CombatTuning.Group.RightArm: return Limb.RightHand;
                case CombatTuning.Group.LeftLeg: return Limb.LeftFoot;
                case CombatTuning.Group.RightLeg: return Limb.RightFoot;
                default: return null;
            }
        }

        void SetAnchors(Limb? acting, float scale)
        {
            var c = combatRefs;
            if (c == null) return;

            float hand = tuning != null ? tuning.handHold : 1f;
            float foot = tuning != null ? tuning.footHold : 1f;
            var r = rideRefs;

            Anchor(c.leftHand, Limb.LeftHand, acting, scale * hand,
                   r != null ? r.leftHandTarget : null, r != null ? r.leftHandHint : null);
            Anchor(c.rightHand, Limb.RightHand, acting, scale * hand,
                   r != null ? r.rightHandTarget : null, r != null ? r.rightHandHint : null);
            Anchor(c.leftFoot, Limb.LeftFoot, acting, scale * foot,
                   r != null ? r.leftLegTarget : null, r != null ? r.leftLegHint : null);
            Anchor(c.rightFoot, Limb.RightFoot, acting, scale * foot,
                   r != null ? r.rightLegTarget : null, r != null ? r.rightLegHint : null);
        }

        void Anchor(TwoBoneIKConstraint ik, Limb limb, Limb? acting, float scale,
                    Transform rideTarget, Transform rideHint)
        {
            if (ik == null) return;
            if (acting == null || limb == acting || rideTarget == null || ik.data.tip == null)
            { ik.weight = 0f; return; }

            if (ik.data.target != null)
                ik.data.target.SetPositionAndRotation(rideTarget.position, rideTarget.rotation);

            if (ik.data.hint != null && rideHint != null)
                ik.data.hint.position = rideHint.position;

            ik.weight = scale;

            ik.data.targetRotationWeight = 0f; 
        }
    }
}
