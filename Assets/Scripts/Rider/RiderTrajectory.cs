using System;
using UnityEngine;

namespace MotoSquid.Rider
{
    [Serializable]
    public struct RiderBoneTrack
    {
        public HumanBodyBones bone;
        public AnimationCurve posX, posY, posZ;

        public Vector3 Evaluate(float t) =>
            new Vector3(posX.Evaluate(t), posY.Evaluate(t), posZ.Evaluate(t));
    }

    [CreateAssetMenu(menuName = "MotoSquid/Rider Trajectory", fileName = "Trajectory")]
    public class RiderTrajectory : ScriptableObject
    {
        public string clipName;
        public string style;
        public float length;
        public float frameRate;
        public bool loop;

        [Header("Source proportions the contact deltas were measured on")]
        public float sourceArmLength;
        public float sourceLegLength;
        public float sourceHipHeight;

        [Header("Humanoid body pose")]
        public AnimationCurve[] muscles = Array.Empty<AnimationCurve>();
        public AnimationCurve bodyPosX, bodyPosY, bodyPosZ;
        public AnimationCurve bodyRotX, bodyRotY, bodyRotZ, bodyRotW;

        [Header("Contact deltas for the IK targets and hints")]
        public RiderBoneTrack[] tracks = Array.Empty<RiderBoneTrack>();

        public void SampleMuscles(float t, float[] into)
        {
            int n = Mathf.Min(into.Length, muscles.Length);
            for (int i = 0; i < n; i++)
                if (muscles[i] != null) into[i] = muscles[i].Evaluate(t);
        }

        public Vector3 SampleBodyPosition(float t) =>
            new Vector3(bodyPosX.Evaluate(t), bodyPosY.Evaluate(t), bodyPosZ.Evaluate(t));

        public Quaternion SampleBodyRotation(float t)
        {
            var q = new Quaternion(bodyRotX.Evaluate(t), bodyRotY.Evaluate(t),
                                   bodyRotZ.Evaluate(t), bodyRotW.Evaluate(t));
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m < 1e-6f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        public bool TryGetTrack(HumanBodyBones bone, out RiderBoneTrack track)
        {
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i].bone != bone) continue;
                track = tracks[i];
                return true;
            }

            track = default;
            return false;
        }
    }
}
