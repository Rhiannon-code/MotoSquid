using MotoSquid.Controls;
using System.Collections;
using UnityEngine;

namespace MotoSquid.Bike
{
    public class LandingImpactFX : MonoBehaviour
    {
        [Header("References")]
        public BikeController bikeController;
        public ParticleSystem landingDust;
        public Transform cameraDipTarget;

        [Header("Trigger")]
        public float minAirtime = 0.4f;
        public float fullAirtime = 1.5f;

        [Header("Camera Dip")]
        public float dipDistance = 0.15f;
        public float dipDuration = 0.18f;

        [Header("Rumble")]
        public int   playerDeviceIndex = -1;
        public float rumbleLow  = 0.6f;
        public float rumbleHigh = 0.4f;
        public float rumbleDuration = 0.18f;

        float _takeoffTime = -1f;
        bool  _airborne;
        Coroutine _dip;

        void Awake()
        {
            if (bikeController == null) bikeController = GetComponentInParent<BikeController>(true);
        }

        void OnEnable()
        {
            if (bikeController == null) return;
            bikeController.bikeEvents.OnTakeOff?.AddListener(OnTakeOff);
            bikeController.bikeEvents.OnGrounded?.AddListener(OnGrounded);
        }

        void OnDisable()
        {
            if (bikeController == null) return;
            bikeController.bikeEvents.OnTakeOff?.RemoveListener(OnTakeOff);
            bikeController.bikeEvents.OnGrounded?.RemoveListener(OnGrounded);
        }

        void OnTakeOff()
        {
            _airborne    = true;
            _takeoffTime = Time.time;
        }

        void OnGrounded()
        {
            if (!_airborne) return;
            _airborne = false;

            float airtime = Time.time - _takeoffTime;
            if (_takeoffTime < 0f || airtime < minAirtime) return;

            float strength = Mathf.Clamp01(Mathf.InverseLerp(minAirtime, fullAirtime, airtime));

            if (landingDust != null) landingDust.Play();

            RumbleManager.Instance.Rumble(
                playerDeviceIndex, rumbleLow * strength, rumbleHigh * strength, rumbleDuration);

            if (cameraDipTarget != null)
            {
                if (_dip != null) StopCoroutine(_dip);
                _dip = StartCoroutine(CameraDip(dipDistance * strength));
            }
        }

        IEnumerator CameraDip(float distance)
        {
            Vector3 baseLocal = cameraDipTarget.localPosition;
            Vector3 down      = baseLocal - Vector3.up * distance;
            float half = dipDuration * 0.5f;

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                cameraDipTarget.localPosition = Vector3.Lerp(baseLocal, down, t / half);
                yield return null;
            }
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                cameraDipTarget.localPosition = Vector3.Lerp(down, baseLocal, t / half);
                yield return null;
            }
            cameraDipTarget.localPosition = baseLocal;
            _dip = null;
        }
    }
}
