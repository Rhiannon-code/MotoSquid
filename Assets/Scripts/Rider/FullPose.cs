using System;
using UnityEngine;

namespace MotoSquid.Rider
{
    [CreateAssetMenu(fileName = "FullPose", menuName = "Rig Lab/Full Pose")]
    public class FullPose : ScriptableObject
    {
        public string sourceClip;
        public string sampledOn;
        public int frameCount;
        public float duration = 1f;
        public float frameRate = 30f;
        public string[] bonePaths = new string[0];
        public Quaternion[] rotations = new Quaternion[0];
        public Vector3[] positions = new Vector3[0];

        public int BoneCount { get { return bonePaths.Length; } }

        // Exporters disagree on what the skeleton root transform is called, these poses were sampled
        // on a rig rooted at "root", while Bliss and Mace export "RL_BoneRoot" which shifts every
        // path below it. CC4 bone names are unique, so fall back to the leaf when it is unambiguous
        public int IndexOf(string bonePath)
        {
            if (string.IsNullOrEmpty(bonePath)) return -1;

            int exact = Array.IndexOf(bonePaths, bonePath);
            if (exact >= 0) return exact;

            string leaf = Leaf(bonePath);
            int found = -1;
            for (int i = 0; i < bonePaths.Length; i++)
            {
                if (Leaf(bonePaths[i]) != leaf) continue;
                if (found >= 0) return -1;
                found = i;
            }
            return found;
        }

        static string Leaf(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        public bool Sample(int bone, float t, out Quaternion rot, out Vector3 pos)
        {
            rot = Quaternion.identity; pos = Vector3.zero;
            int n = frameCount;
            if (n == 0 || bone < 0 || bone >= bonePaths.Length) return false;
            if (n == 1) { rot = rotations[bone]; pos = positions[bone]; return true; }

            float f = Mathf.Clamp01(t) * (n - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), n - 2);
            float k = f - i;

            int a = i * bonePaths.Length + bone;
            int b = (i + 1) * bonePaths.Length + bone;
            rot = Quaternion.Slerp(rotations[a], rotations[b], k);
            pos = Vector3.Lerp(positions[a], positions[b], k);
            return true;
        }
    }
}
