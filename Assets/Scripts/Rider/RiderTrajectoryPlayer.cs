using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Rider
{
    [DefaultExecutionOrder(-50)]
    public class RiderTrajectoryPlayer : MonoBehaviour
    {
        [Serializable]
        public struct Contact
        {
            public HumanBodyBones tip;
            public HumanBodyBones mid;
            public Transform anchor;
            public TwoBoneIKConstraint constraint;
            public bool isArm;
            public Vector3 offset;
        }

        public Animator animator;
        public RiderTrajectory trajectory;
        public Contact[] contacts = Array.Empty<Contact>();

        [Range(0f, 1f)] public float weight = 1f;
        [Range(0f, 1f)] public float contactWeight = 0.75f;
        public float speed = 1f;
        public bool playing = true;

        [Header("Knees splay outward so they clear the tank; elbows keep the posed bend plane")]
        public float kneeOutward = 0.18f;
        public bool logReach;

        public float Time01 => trajectory != null && trajectory.length > 0f ? _time / trajectory.length : 0f;
        public bool Finished { get; private set; }

        HumanPoseHandler _handler;
        HumanPose _pose;
        HumanPose _current;
        float[] _sampled;
        float _time;
        float _armScale = 1f, _legScale = 1f;
        RiderTrajectory _bound;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogError($"{name}: RiderTrajectoryPlayer needs a Humanoid Animator; disabling.");
                enabled = false;
                return;
            }

            _handler = new HumanPoseHandler(animator.avatar, animator.transform);
            _handler.GetHumanPose(ref _pose);
            _handler.GetHumanPose(ref _current);
            _sampled = new float[HumanTrait.MuscleCount];
        }

        void OnDestroy() => _handler?.Dispose();

        public void Play(RiderTrajectory next, float startTime = 0f)
        {
            trajectory = next;
            _time = startTime;
            Finished = false;
            playing = true;
        }

        void Update()
        {
            if (trajectory == null || _handler == null) return;

            if (!ReferenceEquals(_bound, trajectory))
            {
                Rebind();
                _bound = trajectory;
            }

            if (playing && trajectory.length > 0f)
            {
                _time += UnityEngine.Time.deltaTime * speed;
                if (_time >= trajectory.length)
                {
                    if (trajectory.loop) _time %= trajectory.length;
                    else { _time = trajectory.length; Finished = true; }
                }
            }

            ApplyPose();
            ApplyContacts();
        }

        void Rebind()
        {
            _armScale = Ratio(trajectory.sourceArmLength,
                Span(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm) +
                Span(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand));

            _legScale = Ratio(trajectory.sourceLegLength,
                Span(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg) +
                Span(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot));
        }

        static float Ratio(float source, float target) =>
            source > 1e-4f && target > 1e-4f ? target / source : 1f;

        float Span(HumanBodyBones a, HumanBodyBones b)
        {
            var ta = animator.GetBoneTransform(a);
            var tb = animator.GetBoneTransform(b);
            return ta != null && tb != null ? Vector3.Distance(ta.position, tb.position) : 0f;
        }

        void ApplyPose()
        {
            if (weight <= 0f) return;

            trajectory.SampleMuscles(_time, _sampled);
            _handler.GetHumanPose(ref _current);

            for (int i = 0; i < _pose.muscles.Length && i < _sampled.Length; i++)
                _pose.muscles[i] = weight >= 1f
                    ? _sampled[i]
                    : Mathf.Lerp(_current.muscles[i], _sampled[i], weight);

            _pose.bodyPosition = _current.bodyPosition;
            _pose.bodyRotation = Quaternion.Slerp(_current.bodyRotation, trajectory.SampleBodyRotation(_time), weight);
            _handler.SetHumanPose(ref _pose);
        }

        static Vector3 PushOut(Vector3 rootP, Vector3 midP, Vector3 tipP)
        {
            var chord = tipP - rootP;
            float chordLen = chord.magnitude;
            if (chordLen < 1e-5f) return midP;

            var onChord = rootP + Vector3.Project(midP - rootP, chord / chordLen);
            var bend = midP - onChord;
            return bend.sqrMagnitude < 1e-8f ? midP : midP + bend.normalized * chordLen * 0.5f;
        }

        void ApplyContacts()
        {
            var rig = animator.transform;

            foreach (var contact in contacts)
            {
                if (contact.constraint == null || contact.anchor == null) continue;

                var data = contact.constraint.data;
                if (data.target == null) continue;

                float scale = contact.isArm ? _armScale : _legScale;
                var delta = trajectory.TryGetTrack(contact.tip, out var track)
                    ? track.Evaluate(_time) * scale
                    : Vector3.zero;

                data.target.position = contact.anchor.position
                                     + rig.TransformVector(delta)
                                     + contact.anchor.TransformVector(contact.offset);

                var tip = animator.GetBoneTransform(contact.tip);
                if (tip != null) data.target.rotation = tip.rotation;

                var root = animator.GetBoneTransform(contact.isArm
                    ? (contact.tip == HumanBodyBones.LeftHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm)
                    : (contact.tip == HumanBodyBones.LeftFoot ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg));
                var mid = animator.GetBoneTransform(contact.mid);
                var tipT = animator.GetBoneTransform(contact.tip);

                if (data.hint != null && mid != null && root != null && tipT != null)
                {
                    var hint = PushOut(root.position, mid.position, tipT.position);
                    if (!contact.isArm && kneeOutward > 0f)
                    {
                        float side = contact.tip == HumanBodyBones.LeftFoot ? -1f : 1f;
                        hint += animator.transform.right * side * kneeOutward;
                    }

                    data.hint.position = hint;
                }

                contact.constraint.weight = contactWeight;

                if (logReach && root != null)
                {
                    float reach = Vector3.Distance(root.position, mid.position)
                                + Vector3.Distance(mid.position, tipT.position);
                    float need = Vector3.Distance(root.position, data.target.position);
                    Debug.Log($"{name} {contact.tip}: needs {need * 100f:0.0} cm of {reach * 100f:0.0} cm" +
                              (need > reach ? $" SHORT BY {(need - reach) * 100f:0.0} cm" : " (reachable)"));
                }
            }
        }
    }
}
