using System;
using UnityEngine;

namespace MotoSquid.Rider
{
    [CreateAssetMenu(fileName = "PoseTrack", menuName = "Rig Lab/Pose Track")]
    public class PoseTrack : ScriptableObject
    {
        [Serializable]
        public struct Key
        {
            public float t;          // 0..1 across the whole action
            public Vector3 offset;   // Limb offset from rest, bike space
            public Vector3 euler;    // Limb rotation, bike space
            public Vector3 hint;     // Elbow/knee offset from rest
            public Vector3 hip;      // Hip offset from rest
            public Vector3 chest;    // Chest rotation from rest, bike space
                                     // A swing read as a swing rather than an arm wave
        }

        public string sourceClips;
        public float duration = 1f;
        public float holdAt = 0.5f;
        public float strikeAt = 0.7f;
        public Key[] keys = new Key[0];

        [Serializable]
        public class BoneChannel
        {
            public HumanBodyBones bone;
            public Quaternion[] local = new Quaternion[0];
            public Quaternion[] root = new Quaternion[0];
        }

        public BoneChannel[] channels = new BoneChannel[0];

        [Serializable]
        public class ChainChannel
        {
            public string name;
            public Vector3[] points = new Vector3[0];
            public Quaternion[] rotations = new Quaternion[0];
        }

        public ChainChannel[] chains = new ChainChannel[0];

        public Quaternion EvaluateChainRotation(ChainChannel c, float t) { return Sample(c == null ? null : c.rotations, t); }

        public void EvaluateChain(ChainChannel c, float t, out Vector3 tip, out Vector3 hint)
        {
            tip = hint = Vector3.zero;
            int n = c == null ? 0 : c.points.Length / 2;
            if (n == 0) return;
            if (n == 1) { tip = c.points[0]; hint = c.points[1]; return; }

            float f = Mathf.Clamp01(t) * (n - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), n - 2);
            float k = f - i;
            tip = Vector3.Lerp(c.points[i * 2], c.points[(i + 1) * 2], k);
            hint = Vector3.Lerp(c.points[i * 2 + 1], c.points[(i + 1) * 2 + 1], k);
        }

        public Quaternion EvaluateChannel(BoneChannel c, float t) { return Sample(c == null ? null : c.local, t); }

        public Quaternion EvaluateChannelRoot(BoneChannel c, float t) { return Sample(c == null ? null : c.root, t); }

        static Quaternion Sample(Quaternion[] arr, float t)
        {
            if (arr == null || arr.Length == 0) return Quaternion.identity;
            if (arr.Length == 1) return arr[0];

            float f = Mathf.Clamp01(t) * (arr.Length - 1);
            int i = Mathf.FloorToInt(f);
            if (i >= arr.Length - 1) return arr[arr.Length - 1];

            return Quaternion.Slerp(arr[i], arr[i + 1], f - i);
        }

        public Key Evaluate(float t)
        {
            if (keys.Length == 0) return default(Key);
            if (keys.Length == 1 || t <= keys[0].t) return keys[0];
            if (t >= keys[keys.Length - 1].t) return keys[keys.Length - 1];

            for (int i = 1; i < keys.Length; i++)
            {
                if (keys[i].t < t) continue;
                var a = keys[i - 1];
                var b = keys[i];
                float span = b.t - a.t;
                float k = span <= 1e-6f ? 0f : (t - a.t) / span;
                return new Key
                {
                    t = t,
                    offset = Vector3.Lerp(a.offset, b.offset, k),
                    euler = new Vector3(
                        Mathf.LerpAngle(a.euler.x, b.euler.x, k),
                        Mathf.LerpAngle(a.euler.y, b.euler.y, k),
                        Mathf.LerpAngle(a.euler.z, b.euler.z, k)),
                    hint = Vector3.Lerp(a.hint, b.hint, k),
                    hip = Vector3.Lerp(a.hip, b.hip, k),
                    chest = Vector3.Lerp(a.chest, b.chest, k)
                };
            }
            return keys[keys.Length - 1];
        }
    }
}
