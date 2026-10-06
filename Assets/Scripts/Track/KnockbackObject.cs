using MotoSquid.Audio;
using MotoSquid.Bike;
using MotoSquid.Controls;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Track
{
    public class KnockbackObject : MonoBehaviour
    {
        public enum ObjectType { Light, Medium, Heavy }

        [SerializeField] private ObjectType objectType = ObjectType.Light;
        [SerializeField] private float launchForce = 12f;
        [SerializeField] private float launchUpward = 3f;
        [SerializeField] private float playerSlowMultiplier = 0.75f; // 1 = no change, 0 = full stop
        [SerializeField] private bool startKinematic = true; // Keeps object still until hit

        [Header("Impact audio")]
        [SerializeField] private AudioClip[] impactClips;   // Picked from at random
        [SerializeField, Range(0f, 1.5f)] private float impactVolume = 1f;
        [SerializeField] private AudioMixerGroup impactOutput;   // World SFX
        [SerializeField] private float impactMinDistance = 3f;
        [SerializeField] private float impactMaxDistance = 45f;

        private Rigidbody rb;
        private bool hasBeenHit;
        private AudioSource impactSource;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) 
            {
                Debug.LogError("[KnockbackObject] Requires a Rigidbody component, disabling.", this);
                enabled = false;
                return;
            }
            if (startKinematic)
                rb.isKinematic = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasBeenHit) return;

            BikeInput playerInput = collision.gameObject.GetComponentInParent<BikeInput>();
            if (playerInput != null)
            {
                BikeController ctrl = playerInput.bikeControllerRhiannon;
                if (ctrl == null) return;

                hasBeenHit = true;
                LaunchSelf(ctrl.bikeReferences.BikeRb);
                ctrl.bikeReferences.BikeRb.linearVelocity *= playerSlowMultiplier;
                return;
            }

            BikeAIController aiCtrl = collision.gameObject.GetComponentInParent<BikeAIController>();
            if (aiCtrl != null)
            {
                hasBeenHit = true;
                LaunchSelf(aiCtrl.bikeReferences.BikeRb);
                // Slow the AI on impact too, so props are fair to everyone (not just the player).
                aiCtrl.bikeReferences.BikeRb.linearVelocity *= playerSlowMultiplier;
            }
        }

        private void LaunchSelf(Rigidbody bikeRb)
        {
            PlayImpact(bikeRb);

            if (startKinematic)
                rb.isKinematic = false;

            Vector3 dir = bikeRb.linearVelocity.normalized;
            if (dir == Vector3.zero)
                dir = transform.forward;

            rb.AddForce(dir * launchForce + Vector3.up * launchUpward, ForceMode.Impulse);
        }

        private void PlayImpact(Rigidbody bikeRb)
        {
            if (impactClips == null || impactClips.Length == 0) return;

            if (impactSource == null)
            {
                impactSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                impactSource.playOnAwake  = false;
                impactSource.loop         = false;
                impactSource.volume       = 1f;   // PlayOneShot scales by this, keep it at unity
                impactSource.spatialBlend = 1f;
                impactSource.minDistance  = impactMinDistance;
                impactSource.maxDistance  = impactMaxDistance;
                impactSource.rolloffMode  = AudioRolloffMode.Logarithmic;
                if (impactOutput != null) impactSource.outputAudioMixerGroup = impactOutput;
            }

            float speed = bikeRb != null ? bikeRb.linearVelocity.magnitude : 0f;
            float force = Mathf.Clamp01(Mathf.InverseLerp(4f, 25f, speed));   // m/s
            var clip = impactClips[Random.Range(0, impactClips.Length)];
            if (clip != null) AudioBurstLog.Note($"knockback impact ({clip.name})", this);
            if (clip != null) impactSource.PlayOneShot(clip, impactVolume * Mathf.Lerp(0.45f, 1f, force));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (objectType)
            {
                case ObjectType.Light:
                    launchForce = 12f; launchUpward = 3f; playerSlowMultiplier = 0.75f; break;
                case ObjectType.Medium:
                    launchForce = 7f;  launchUpward = 2f; playerSlowMultiplier = 0.65f; break;
                case ObjectType.Heavy:
                    launchForce = 3f;  launchUpward = 1f; playerSlowMultiplier = 0.55f; break;
            }
        }
#endif
    }
}
