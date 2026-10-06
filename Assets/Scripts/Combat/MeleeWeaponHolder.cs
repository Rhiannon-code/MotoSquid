using MotoSquid.Rider;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Combat
{
    public class MeleeWeaponHolder : MonoBehaviour
    {
        [Header("Loadout")]
        public MeleeWeapon startingWeapon;

        [Header("References")]
        public Animator riderAnimator;
        [SerializeField] private WeaponSocket[] sockets;

        public bool drawOnlyForMelee = true;

        static readonly string SwingLeftClip  = "AS_Long_Weapon_Left";
        static readonly string SwingRightClip = "AS_Long_Weapon_Right";
        static readonly string HoldClip       = "AS_Long_Weapon_Hold";

        RuntimeAnimatorController _baseController;   
        GameObject _prop;

        public MeleeWeapon Weapon { get; private set; }
        public bool HasWeapon => Weapon != null;
        public Transform PropTransform
        {
            get
            {
                if (_prop != null) return _prop.transform;

                if (_rigWeapon == null && riderAnimator != null)
                    _rigWeapon = riderAnimator.GetComponent<CombatWeapon>();

                return _rigWeapon != null ? _rigWeapon.hand : null;
            }
        }

        CombatWeapon _rigWeapon;
        public float Reach(float bare)               => Weapon != null ? Weapon.reach : bare;
        public float SwingCooldown(float bare)       => Weapon != null ? Weapon.swingCooldown : bare;
        public float KnockbackMultiplier             => Weapon != null ? Weapon.knockbackMultiplier : 1f;
        public int   HitsPerSwing                    => Weapon != null ? Mathf.Max(1, Weapon.hitsPerSwing) : 1;
        public int   ReleaseSide(int bare)           => Weapon != null ? (int)Weapon.heldIn : bare;
        public AudioClip[] SwingWhoosh => Weapon != null && Weapon.swingWhoosh != null && Weapon.swingWhoosh.Length > 0
            ? Weapon.swingWhoosh : null;
        public AudioClip[] Impact => Weapon != null && Weapon.impact != null && Weapon.impact.Length > 0
            ? Weapon.impact : null;

        void Awake()
        {
            if (riderAnimator == null) riderAnimator = FindRider();
            if (sockets == null || sockets.Length == 0)
                sockets = GetComponentsInChildren<WeaponSocket>(true);

            if (riderAnimator != null)
                _baseController = riderAnimator.runtimeAnimatorController;

            if (startingWeapon != null) Equip(startingWeapon);
        }

        public void BindRider(Animator rider)
        {
            if (rider == null || rider == riderAnimator) return;

            var held = Weapon;
            Equip(null);

            riderAnimator = rider;
            _driver = null;
            sockets = rider.GetComponentsInChildren<WeaponSocket>(true);
            _baseController = rider.runtimeAnimatorController;

            if (held != null) Equip(held);
        }

        public Animator FindRider()
        {
            Animator fallback = null;
            foreach (var a in GetComponentsInChildren<Animator>(true))
            {
                if (!a.isHuman) continue;
                if (a.gameObject.activeInHierarchy) return a;
                if (fallback == null) fallback = a;
            }
            return fallback;
        }

        public void Equip(MeleeWeapon weapon)
        {
            Weapon = weapon;
            SpawnProp();
            ApplyClipOverrides();
        }

        public void Unequip() => Equip(null);

        void SpawnProp()
        {
            if (_prop != null)
            {
                if (Application.isPlaying) Destroy(_prop); else DestroyImmediate(_prop);
                _prop = null;
            }
            if (Weapon == null || Weapon.prop == null) return;

            if (RiderDrawsItsOwnWeapon()) return;

            var socket = FindSocket(Weapon.heldIn);
            if (socket == null)
            {
                Debug.LogWarning($"{name}: no WeaponSocket for the {Weapon.heldIn} hand, " +
                                 $"'{Weapon.displayName}' equipped without a prop. Rebuild the character " +
                                 "(MotoSquid > Characters) to add the sockets.");
                return;
            }

            _prop = Instantiate(Weapon.prop, socket.transform);
            _prop.name = Weapon.prop.name;
            _prop.transform.localPosition = Weapon.localPosition + socket.localPositionOffset;
            _prop.transform.localRotation = Quaternion.Euler(Weapon.localEuler + socket.localEulerOffset);
            _prop.transform.localScale    = Weapon.localScale * Mathf.Max(0.01f, socket.scaleMultiplier);

            foreach (var c in _prop.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in _prop.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;

            _propRenderers = _prop.GetComponentsInChildren<Renderer>(true);
            if (drawOnlyForMelee) ShowProp(false);
        }

        bool RiderDrawsItsOwnWeapon()
        {
            if (riderAnimator == null) return false;
            var rigWeapon = riderAnimator.GetComponent<CombatWeapon>();
            return rigWeapon != null && rigWeapon.Ready;
        }

        Renderer[] _propRenderers;
        CombatRigDriver _driver;

        void LateUpdate()
        {
            if (!drawOnlyForMelee || _propRenderers == null || _propRenderers.Length == 0) return;

            if (_driver == null)
            {
                var combat = GetComponentInParent<CombatSystem>() ??
                             GetComponentInChildren<CombatSystem>(true);
                _driver = combat != null ? combat.rigDriver : null;
                if (_driver == null) return;
            }

            bool swinging = _driver.phase != CombatRigDriver.Phase.Idle &&
                            (_driver.action == CombatRigDriver.Action.MeleeLeft ||
                             _driver.action == CombatRigDriver.Action.MeleeRight);
            ShowProp(swinging);
        }

        void ShowProp(bool visible)
        {
            if (_propRenderers == null) return;
            foreach (var r in _propRenderers) if (r != null) r.enabled = visible;
        }

        WeaponSocket FindSocket(WeaponHand hand)
        {
            if (sockets == null) return null;
            foreach (var s in sockets)
                if (s != null && s.hand == hand) return s;
            return null;
        }

        void ApplyClipOverrides()
        {
            if (riderAnimator == null || _baseController == null) return;

            bool wantsOverride = Weapon != null &&
                                 (Weapon.swingLeft != null || Weapon.swingRight != null || Weapon.hold != null);

            if (!wantsOverride)
            {
                if (riderAnimator.runtimeAnimatorController != _baseController)
                    SwapController(_baseController);
                return;
            }

            var aoc = new AnimatorOverrideController(_baseController) { name = $"{_baseController.name} ({Weapon.displayName})" };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
            aoc.GetOverrides(pairs);

            int applied = 0;
            for (int i = 0; i < pairs.Count; i++)
            {
                var original = pairs[i].Key;
                if (original == null) continue;

                AnimationClip replacement = null;
                if (Weapon.swingLeft  != null && original.name.StartsWith(SwingLeftClip))  replacement = Weapon.swingLeft;
                else if (Weapon.swingRight != null && original.name.StartsWith(SwingRightClip)) replacement = Weapon.swingRight;
                else if (Weapon.hold  != null && original.name.StartsWith(HoldClip))       replacement = Weapon.hold;

                if (replacement == null) continue;
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                applied++;
            }

            if (applied == 0)
            {
                Debug.LogWarning($"{name}: '{Weapon.displayName}' has bespoke swing clips but the rider " +
                                 $"controller '{_baseController.name}' plays no AS_Long_Weapon_* clip to " +
                                 "replace. Run MotoSquid > Animation > Add Combat Clips To Biker Controller.");
                return;
            }

            aoc.ApplyOverrides(pairs);
            SwapController(aoc);
        }

        void SwapController(RuntimeAnimatorController controller)
        {
            int layers = riderAnimator.layerCount;
            var hashes = new int[layers];
            var times  = new float[layers];
            bool restore = riderAnimator.isInitialized && Application.isPlaying;
            if (restore)
            {
                for (int i = 0; i < layers; i++)
                {
                    var st = riderAnimator.GetCurrentAnimatorStateInfo(i);
                    hashes[i] = st.fullPathHash;
                    times[i]  = st.normalizedTime;
                }
            }

            riderAnimator.runtimeAnimatorController = controller;

            if (!restore) return;
            for (int i = 0; i < riderAnimator.layerCount && i < layers; i++)
                if (hashes[i] != 0) riderAnimator.Play(hashes[i], i, times[i]);
        }
    }
}
