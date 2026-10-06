using MotoSquid.Bike;
using MotoSquid.Combat;
using UnityEngine;

namespace MotoSquid.Rider
{
    public class BikeRiderAnimator : MonoBehaviour
    {
        public Animator animator;
        public float airborneDebounce = 0.2f;
        public float steerDampTime = 0.15f;
        public float speedDampTime = 0.08f;

        [SerializeField] MonoBehaviour bikeSource;

        IBikeAudioState _bike;
        float _airTime;
        UnityEngine.Animations.Rigging.TwoBoneIKConstraint _footL, _footR;
        float _footBaseL = 1f, _footBaseR = 1f;
        public bool mountedPlantedFootIsLeft = true;
        CombatSystem _combat;
        int _leanLayer = -1;
        float _leanBase;
        public float leanFullAtSpeed = 0.15f;
        // How fast the forward tuck fades in once speed asks for it. Lower reads smoother.
        public float leanBlendSpeed = 2f;
        static readonly int SpeedHash    = Animator.StringToHash("Speed");
        static readonly int RidingHash   = Animator.StringToHash("Riding");
        static readonly int SteerHash    = Animator.StringToHash("Steer");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int WheelieHash  = Animator.StringToHash("Wheelie");
        static readonly int BrakingHash  = Animator.StringToHash("Braking");
        static readonly int ReverseHash  = Animator.StringToHash("Reverse");

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (bikeSource == null)
                bikeSource = GetComponentInParent<IBikeAudioState>() as MonoBehaviour;
            _bike = bikeSource as IBikeAudioState;

            if (_bike == null)
                Debug.LogWarning($"{name}: BikeRiderAnimator found no IBikeAudioState on a parent; rider will idle.");

            foreach (var c in GetComponentsInChildren<UnityEngine.Animations.Rigging.TwoBoneIKConstraint>(true))
            {
                if (c.name == "Left Foot IK")  { _footL = c; _footBaseL = c.weight; }
                if (c.name == "Right Foot IK") { _footR = c; _footBaseR = c.weight; }
            }
            _combat = GetComponentInParent<CombatSystem>();

            for (int i = 0; i < animator.layerCount; i++)
                if (animator.GetLayerName(i) == "Upper Lean (Additive)") { _leanLayer = i; break; }
            _leanBase = _leanLayer >= 0 ? animator.GetLayerWeight(_leanLayer) : 0f;
        }

        void Update()
        {
            if (animator == null || _bike == null) return;

            float topMs = Mathf.Max(1f, _bike.MaxSpeedKmh / 3.6f);
            float speed01 = Mathf.Clamp01(Mathf.Abs(_bike.SpeedMs) / topMs);
            animator.SetFloat(SpeedHash, speed01, speedDampTime, Time.deltaTime);
            animator.SetBool(RidingHash, _bike.CanMove);
            animator.SetFloat(SteerHash, _bike.SteerAmount, steerDampTime, Time.deltaTime);

            _airTime = _bike.IsGrounded ? 0f : _airTime + Time.deltaTime;
            animator.SetBool(GroundedHash, _airTime < airborneDebounce);

            animator.SetBool(WheelieHash, _bike.IsDoingWheelie);
            animator.SetBool(BrakingHash, _bike.IsBraking && speed01 > 0.05f);
            animator.SetBool(ReverseHash, _bike.ReverseInput > 0.1f && _bike.SpeedMs < 0.1f);

            if (_leanLayer >= 0)
            {
                float target = _leanBase * Mathf.Clamp01(speed01 / Mathf.Max(0.01f, leanFullAtSpeed));
                float w = Mathf.MoveTowards(animator.GetLayerWeight(_leanLayer), target, Time.deltaTime * leanBlendSpeed);
                animator.SetLayerWeight(_leanLayer, w);
            }

            UpdateFootPins();
        }

        void UpdateFootPins()
        {
            if (_footL == null && _footR == null) return;

            float mounted = TaggedWeight("Mounted");
            // Only the planted foot releases in the mounted idle; the other holds its peg (else it sinks in)
            float mountedL = mountedPlantedFootIsLeft ? mounted : 0f;
            float mountedR = mountedPlantedFootIsLeft ? 0f : mounted;
            float kick    = _combat != null ? TaggedWeight("Combat") : 0f;
            float releaseL = Mathf.Max(mountedL, _combat != null && _combat.KickReleaseSide < 0 ? kick : 0f);
            float releaseR = Mathf.Max(mountedR, _combat != null && _combat.KickReleaseSide > 0 ? kick : 0f);
            if (_footL != null) _footL.weight = _footBaseL * (1f - releaseL);
            if (_footR != null) _footR.weight = _footBaseR * (1f - releaseR);
        }

        // 0..1: how much a state with this tag drives ANY layer right now, tracking crossfades
        float TaggedWeight(string tag)
        {
            float best = 0f;
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                var cur = animator.GetCurrentAnimatorStateInfo(layer);
                float w;
                if (!animator.IsInTransition(layer))
                {
                    w = cur.IsTag(tag) ? 1f : 0f;
                }
                else
                {
                    float t    = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
                    float from = cur.IsTag(tag) ? 1f : 0f;
                    float to   = animator.GetNextAnimatorStateInfo(layer).IsTag(tag) ? 1f : 0f;
                    w = Mathf.Lerp(from, to, t);
                }
                if (w > best) best = w;
            }
            return best;
        }
    }
}
