using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace MotoSquid.Traffic
{
    public class SplineMover : MonoBehaviour
    {
        public bool  IsPlaying  { get; private set; }
        public float MaxSpeed   { get; set; }
        private SplineContainer _container;
        private float           _cachedLength;
        private float           _t;
        private bool            _loop;
        private GeneratedTrafficLane _laneInfo;

        private SplineContainer _toContainer;
        private float           _toLength;
        private float           _toT;
        private float           _blend;
        private float           _changeDuration;
        private bool            _changing;

        public SplineContainer CurrentContainer => _container;
        public float           NormalizedT      => _t;
        public bool            IsChangingLane   => _changing;
        public Vector3 Velocity => _velocity;
        private Vector3 _velocity;
        private Vector3 _lastVelPos;
        private bool    _hasVelSample;

        // Traffic rides on a kinematic Rigidbody. Writing transform.position would teleport it, which
        // leaves PhysX no swept motion to collide against and no velocity to report in a Collision, so
        // it is driven through MovePosition from FixedUpdate instead. Off puts it back on the frame
        // clock, which is what the spawner's relevance band was originally written against.
        public bool driveThroughPhysics = true;
        private Rigidbody _body;

        public void Setup(SplineContainer container, float startT, float speed, bool loop)
        {
            _container    = container;
            _laneInfo     = container != null ? container.GetComponent<GeneratedTrafficLane>() : null;
            _cachedLength = container != null ? container.Spline.GetLength() : 1f;
            _t            = startT;
            MaxSpeed      = speed;
            _loop         = loop;
            IsPlaying     = true;
            enabled       = true;
            _changing    = false;
            _toContainer = null;
            _blend       = 0f;
            _velocity     = Vector3.zero;
            _hasVelSample = false;
            CacheBody();
        }

        // Every moving vehicle - spawned traffic, pre-placed cars and trams - so the AI can plan against
        // where they all are instead of what a physics scan happens to catch
        static readonly System.Collections.Generic.List<SplineMover> s_active =
            new System.Collections.Generic.List<SplineMover>();
        public static System.Collections.Generic.IReadOnlyList<SplineMover> Active => s_active;

        void OnEnable()
        {
            CacheBody();
            s_active.Add(this);
        }

        void OnDisable() => s_active.Remove(this);

        // x = half width, y = half length, taken once from the collider since pooled vehicles never resize
        public Vector2 HalfSize
        {
            get
            {
                if (_halfSize == Vector2.zero)
                {
                    var box = GetComponentInChildren<BoxCollider>();
                    Vector3 size = box != null
                        ? Vector3.Scale(box.size, box.transform.lossyScale)
                        : GetComponentInChildren<Renderer>() is Renderer r ? r.localBounds.size : new Vector3(2f, 2f, 5f);
                    _halfSize = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.z)) * 0.5f;
                }
                return _halfSize;
            }
        }
        Vector2 _halfSize;

        void CacheBody()
        {
            _body = driveThroughPhysics ? GetComponent<Rigidbody>() : null;
            if (_body != null && !_body.isKinematic) _body = null;
        }

        public void ResetForSpawn(SplineContainer container, float startT, float speed, bool loop)
        {
            Setup(container, startT, speed, loop);
        }

        public void StartLaneChange(SplineContainer target, float duration)
        {
            if (_changing || target == null || _container == null || target == _container) return;

            _toLength = target.Spline.GetLength();
            if (_toLength < 1f) return;

            Vector3 localPos = target.transform.InverseTransformPoint(transform.position);
            SplineUtility.GetNearestPoint(target.Spline, (float3)localPos, out _, out _toT);

            _toContainer    = target;
            _changeDuration = Mathf.Max(0.05f, duration);
            _blend          = 0f;
            _changing       = true;
        }

        // Stays on the frame clock: the spawner's relevance band measures progress in wall time, and
        // moving this to the fixed clock made traffic fall behind the band whenever the frame rate
        // dipped and physics stopped keeping up. Only the pose WRITE goes through physics.
        void Update()
        {
            Step(Time.deltaTime);
        }

        void Step(float dt)
        {
            if (!IsPlaying || _container == null) return;

            _t += MaxSpeed * dt / _cachedLength;

            if (_t >= 1f)
            {
                if (_loop)
                {
                    _t = Mathf.Repeat(_t, 1f);
                }
                else if (!ContinueOntoNextLane())
                {
                    _t        = 1f;
                    IsPlaying = false;
                    enabled   = false;
                }
            }

            _container.Spline.Evaluate(_t, out float3 localPos, out float3 localTan, out float3 localUp);
            Vector3 worldPos = _container.transform.TransformPoint((Vector3)localPos);
            Vector3 worldFwd = _container.transform.TransformDirection((Vector3)localTan);
            Vector3 worldUp  = _container.transform.TransformDirection((Vector3)localUp);

            if (_changing && _toContainer != null)
            {
                _toT  += MaxSpeed * dt / _toLength;
                _toT   = Mathf.Clamp01(_toT);
                _blend += dt / _changeDuration;

                _toContainer.Spline.Evaluate(_toT, out float3 toLocalPos, out float3 toLocalTan, out float3 toLocalUp);
                Vector3 toWorldPos = _toContainer.transform.TransformPoint((Vector3)toLocalPos);
                Vector3 toWorldFwd = _toContainer.transform.TransformDirection((Vector3)toLocalTan);
                Vector3 toWorldUp  = _toContainer.transform.TransformDirection((Vector3)toLocalUp);

                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blend));
                worldPos = Vector3.Lerp(worldPos, toWorldPos, e);
                worldFwd = Vector3.Slerp(worldFwd, toWorldFwd, e);
                worldUp  = Vector3.Slerp(worldUp,  toWorldUp,  e);

                if (_blend >= 1f)
                {
                    _container    = _toContainer;
                    _cachedLength = _toLength;
                    _t            = _toT;
                    _toContainer  = null;
                    _changing     = false;
                }
            }

            if (_hasVelSample && dt > 0f)
                _velocity = (worldPos - _lastVelPos) / dt;
            _lastVelPos   = worldPos;
            _hasVelSample = true;

            if (worldUp.sqrMagnitude < 0.001f) worldUp = Vector3.up;
            bool haveFwd = worldFwd.sqrMagnitude > 0.001f;
            Quaternion worldRot = haveFwd ? Quaternion.LookRotation(worldFwd, worldUp) : transform.rotation;

            // MovePosition takes an absolute target, so several calls between two physics steps simply
            // supersede each other - the sweep still covers the whole gap, which is what stops a racer
            // stepping through a car. Writing the transform instead would teleport the body: no swept
            // contact, and a velocity of zero reported to whatever it hits.
            if (_body != null)
            {
                _body.MovePosition(worldPos);
                if (haveFwd) _body.MoveRotation(worldRot);
            }
            else if (haveFwd) transform.SetPositionAndRotation(worldPos, worldRot);
            else transform.position = worldPos;
        }

        bool ContinueOntoNextLane()
        {
            SplineContainer next = _laneInfo != null ? _laneInfo.next : null;
            if (next == null) return false;

            var nextInfo = next.GetComponent<GeneratedTrafficLane>();
            if (nextInfo != null && nextInfo.trafficLocked) return false;

            float nextLength = next.Spline.GetLength();
            if (nextLength < 1f) return false;

            float overshoot = (_t - 1f) * _cachedLength;

            _container    = next;
            _laneInfo     = next.GetComponent<GeneratedTrafficLane>();
            _cachedLength = nextLength;
            _t            = Mathf.Clamp01(overshoot / nextLength);

            // A lane change in flight targets a spline on the section just left behind
            _changing    = false;
            _toContainer = null;
            _blend       = 0f;

            // The seam is a small positional discontinuity, do not let it read as a velocity spike
            _hasVelSample = false;
            return true;
        }

        public void Play()
        {
            IsPlaying = true;
            enabled   = true;
        }

        public void Stop()
        {
            IsPlaying = false;
            enabled   = false;
        }
    }
}
