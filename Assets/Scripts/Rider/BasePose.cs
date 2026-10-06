using System;
using UnityEngine;

namespace MotoSquid.Rider
{
    public class BasePose : MonoBehaviour
    {
        [Serializable]
        public struct BoneRotation
        {
            public Transform bone;
            public Quaternion localRotation;
            public Vector3 localPosition;
        }

        public BoneRotation[] bones = new BoneRotation[0];
        public bool applyPositions;
        public bool applyEveryFrame;

        void OnEnable() { Apply(); }

        void LateUpdate()
        {
            if (applyEveryFrame) Apply();
        }

        public void Apply()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b.bone == null) continue;
                b.bone.localRotation = b.localRotation;
                if (applyPositions) b.bone.localPosition = b.localPosition;
            }
        }
    }
}
