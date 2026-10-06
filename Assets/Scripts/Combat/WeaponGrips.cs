using System;
using UnityEngine;

namespace MotoSquid.Combat
{
    [CreateAssetMenu(fileName = "WeaponGrips", menuName = "Rig Lab/Weapon Grips")]
    public class WeaponGrips : ScriptableObject
    {
        [Serializable]
        public class Grip
        {
            public GameObject model;
            public Vector3 position;
            public Vector3 euler;
            public float scale = 1f;
        }

        public Grip[] grips = new Grip[0];

        public Grip For(GameObject model)
        {
            if (model == null) return null;
            foreach (var g in grips)
                if (g != null && g.model == model) return g;
            return null;
        }
    }
}
