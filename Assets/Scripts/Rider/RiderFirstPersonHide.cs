using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.DevTools;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoSquid.Rider
{
    [DefaultExecutionOrder(11000)]
    public class RiderFirstPersonHide : MonoBehaviour
    {
        public CameraController cameraController;
        public bool keepShadow = true;

        readonly Dictionary<Renderer, ShadowCastingMode> _restore = new Dictionary<Renderer, ShadowCastingMode>();
        Transform _rider;
        bool _hidden;

        BikeController _bike;
        RiderSwitch _switch;

        void Awake() => _bike = GetComponent<BikeController>();

        CameraController Rig()
        {
            if (cameraController != null) return cameraController;
            if (_bike == null) return null;

            foreach (var cc in FindObjectsByType<CameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cc.Bike == _bike) { cameraController = cc; break; }
            return cameraController;
        }

        void LateUpdate()
        {
            var rig = Rig();
            if (rig == null) return;

            var rider = ActiveRider();
            bool wantHidden = rig.IsFirstPersonView && rider != null;

            if (wantHidden == _hidden && rider == _rider) return;

            Restore();
            _rider = rider;
            _hidden = wantHidden;
            if (wantHidden) Hide(rider);
        }

        void OnDisable() => Restore();

        void Hide(Transform rider)
        {
            foreach (var r in rider.GetComponentsInChildren<Renderer>(true))
            {
                _restore[r] = r.shadowCastingMode;
                if (keepShadow) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                else r.enabled = false;
            }
        }

        void Restore()
        {
            foreach (var pair in _restore)
            {
                if (pair.Key == null) continue;
                pair.Key.shadowCastingMode = pair.Value;
                pair.Key.enabled = true;
            }
            _restore.Clear();
        }

        Transform ActiveRider()
        {
            if (_switch == null) _switch = GetComponentInChildren<RiderSwitch>(true);
            if (_switch != null)
            {
                var rider = _switch.ActiveRider;
                return rider != null ? rider.transform : null;
            }

            foreach (var a in GetComponentsInChildren<Animator>(true))
                if (a.avatar != null && a.avatar.isHuman && a.gameObject.activeInHierarchy)
                    return a.transform;
            return null;
        }
    }
}
