using UnityEngine;

namespace MotoSquid.Combat
{
    public enum WeaponHand { Left = -1, Right = 1 }
    [CreateAssetMenu(fileName = "MeleeWeapon", menuName = "MotoSquid/Melee Weapon")]
    public class MeleeWeapon : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Pipe";
        [TextArea] public string description;

        [Header("Prop")]
        public GameObject prop;
        public WeaponHand heldIn = WeaponHand.Right;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEuler    = Vector3.zero;
        public Vector3 localScale    = Vector3.one;

        [Header("Reach & Damage")]
        public float reach = 2.4f;
        public int   hitsPerSwing = 1;
        public float knockbackMultiplier = 1f;
        public float swingCooldown = 0.6f;
        public float swingSpeed = 1f;

        [Header("Bespoke swing clips (optional)")]
        public AnimationClip swingLeft;
        public AnimationClip swingRight;
        public AnimationClip hold;

        [Header("Audio (optional)")]
        public AudioClip[] swingWhoosh;
        public AudioClip[] impact;
    }
}
