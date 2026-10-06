using UnityEngine;

namespace MotoSquid.Combat
{
    [DefaultExecutionOrder(150)]
    public class CombatWeapon : MonoBehaviour
    {
        public CombatRigDriver driver;
        public WeaponGrips grips;
        public GameObject model;
        public Transform hand;

        GameObject instance;

        public bool Ready { get { return driver != null && model != null && hand != null; } }

        void OnDisable() { Show(false); }

        void LateUpdate()
        {
            bool want = Ready && Wanted();
            if (want && instance == null) instance = Instantiate(model, hand);
            Show(want);
            if (want) Place();
        }

        bool Wanted()
        {
            if (driver.phase == CombatRigDriver.Phase.Idle) return false;
            return driver.action == CombatRigDriver.Action.MeleeLeft
                || driver.action == CombatRigDriver.Action.MeleeRight;
        }

        void Show(bool on)
        {
            if (instance != null && instance.activeSelf != on) instance.SetActive(on);
        }

        void Place()
        {
            var g = grips != null ? grips.For(model) : null;
            if (g == null) return;
            instance.transform.localPosition = g.position;
            instance.transform.localRotation = Quaternion.Euler(g.euler);
            instance.transform.localScale = Vector3.one * Mathf.Max(0.0001f, g.scale);
        }
    }
}
