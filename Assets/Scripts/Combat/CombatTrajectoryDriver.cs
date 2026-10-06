using MotoSquid.Rider;
using System;
using UnityEngine;

namespace MotoSquid.Combat
{
    [DefaultExecutionOrder(-60)]
    public class CombatTrajectoryDriver : MonoBehaviour
    {
        public enum Limb { PunchLeft, PunchRight, KickLeft, KickRight }

        public Animator riderAnimator;

        [Header("Authored source, sampled per limb")]
        public RiderTrajectory punchLeft;
        public RiderTrajectory punchRight;
        public RiderTrajectory kickLeft;
        public RiderTrajectory kickRight;
        public float arcStrength = 1f;
        public float duration = 0.45f;

        readonly float[] _clock = new float[4];
        readonly bool[] _running = new bool[4];
        float _armScale = 1f, _legScale = 1f;
        bool _measured;

        public void Trigger(Limb limb)
        {
            _clock[(int)limb] = 0f;
            _running[(int)limb] = true;
        }

        void Update()
        {
            for (int i = 0; i < _clock.Length; i++)
            {
                if (!_running[i]) continue;
                _clock[i] += Time.deltaTime;
                if (_clock[i] >= duration) _running[i] = false;
            }
        }

        public Vector3 ArcOffset(Limb limb)
        {
            var trajectory = For(limb);
            if (trajectory == null || arcStrength <= 0f || !_running[(int)limb]) return Vector3.zero;
            if (!trajectory.TryGetTrack(Bone(limb), out var track)) return Vector3.zero;

            Measure();

            float t01 = Mathf.Clamp01(_clock[(int)limb] / Mathf.Max(duration, 0.0001f));
            float t = t01 * trajectory.length;

            var start = track.Evaluate(0f);
            var end = track.Evaluate(trajectory.length);
            var chord = Vector3.Lerp(start, end, t01);

            float scale = (limb == Limb.PunchLeft || limb == Limb.PunchRight) ? _armScale : _legScale;

            // Ease the deviation out at both ends so it cannot pop when the motion starts or stops
            float fade = Mathf.Sin(t01 * Mathf.PI);
            return (track.Evaluate(t) - chord) * (scale * arcStrength * fade);
        }

        RiderTrajectory For(Limb limb) => limb switch
        {
            Limb.PunchLeft => punchLeft,
            Limb.PunchRight => punchRight,
            Limb.KickLeft => kickLeft,
            _ => kickRight,
        };

        static HumanBodyBones Bone(Limb limb) => limb switch
        {
            Limb.PunchLeft => HumanBodyBones.LeftHand,
            Limb.PunchRight => HumanBodyBones.RightHand,
            Limb.KickLeft => HumanBodyBones.LeftFoot,
            _ => HumanBodyBones.RightFoot,
        };

        void Measure()
        {
            if (_measured) return;
            _measured = true;

            if (riderAnimator == null) riderAnimator = GetComponentInChildren<Animator>();
            if (riderAnimator == null || riderAnimator.avatar == null || !riderAnimator.avatar.isHuman) return;

            var source = punchLeft ?? punchRight ?? kickLeft ?? kickRight;
            if (source == null) return;

            _armScale = Ratio(source.sourceArmLength,
                Span(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm) +
                Span(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand));

            _legScale = Ratio(source.sourceLegLength,
                Span(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg) +
                Span(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot));
        }

        static float Ratio(float source, float target) =>
            source > 1e-4f && target > 1e-4f ? target / source : 1f;

        float Span(HumanBodyBones a, HumanBodyBones b)
        {
            var ta = riderAnimator.GetBoneTransform(a);
            var tb = riderAnimator.GetBoneTransform(b);
            return ta != null && tb != null ? Vector3.Distance(ta.position, tb.position) : 0f;
        }
    }
}
