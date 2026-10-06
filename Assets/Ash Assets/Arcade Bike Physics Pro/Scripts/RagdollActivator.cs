using UnityEngine;
using UnityEngine.Events;


namespace ArcadeBP_Pro
{
    public class RagdollActivator : MonoBehaviour
    {
        [Tooltip("Reference to the ArcadeBikeControllerPro script.")]
        public ArcadeBikeControllerPro bikeController;

        [Tooltip("Reference to the CameraController script.")]
        public CameraController cameraController;

        [Tooltip("Prefab for the Dummy bike.")]
        public GameObject dummyBikePrefab;

        [Tooltip("Prefab for the character ragdoll.")]
        public GameObject characterRagdollPrefab;

        [Tooltip("Animator component of the animated character.")]
        public Animator characterAnimator;

        [Tooltip("Threshold of impact force to activate ragdoll.")]
        public float impactThreshold = 10f;

        [Tooltip("Ignore collisions with the bottom part of the bike collider.")]
        public bool IgnoreBottomCollision = true;

        [Tooltip("Event triggered when the ragdoll is activated.")]
        public UnityEvent onRagdollActivated;

        [Tooltip("Event triggered when the bike is re-enabled.")]
        public UnityEvent onBikeReEnabled;


        private Rigidbody bikeRigidbody;
        private bool isRagdollActivated = false;
        private GameObject bikeRagdollInstance;
        public GameObject characterRagdollInstance { get; private set; }
        private Transform hipTransform;
        private Collider bikeCollider;

        private void Start()
        {
            if (!bikeController)
            {
                TryGetComponent(out bikeController);
            }

            if (!bikeController || bikeController.bikeReferences == null)
            {
                enabled = false;
                return;
            }

            TryGetComponent(out bikeRigidbody);
            bikeCollider = bikeController.bikeReferences.collider;

            if (!cameraController)
            {
                cameraController = bikeController.bikeReferences.cameraController;
            }

            if (onRagdollActivated == null)
            {
                onRagdollActivated = new UnityEvent();
            }

            if (onBikeReEnabled == null)
            {
                onBikeReEnabled = new UnityEvent();
            }


            onRagdollActivated.AddListener(setCameraTargetToRagdoll);
            onBikeReEnabled.AddListener(resetCameratoBike);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (isRagdollActivated) return;
            if (!bikeRigidbody || !bikeCollider || !bikeController || bikeController.bikeReferences == null || !bikeController.bikeReferences.LeanTransform) return;
            if (collision.contactCount == 0) return;

            Vector3 localContactPoint = bikeController.bikeReferences.LeanTransform.InverseTransformPoint(collision.contacts[0].point);
            Vector3 bikeCenter = bikeController.bikeReferences.LeanTransform.InverseTransformPoint(bikeCollider.bounds.center);

            if (IgnoreBottomCollision)
            {
                if (localContactPoint.y > bikeCenter.y)
                {
                    if (GetImpactForce(collision) > impactThreshold)
                    {
                        ActivateRagdoll();
                    }
                }
            }
            else
            {
                if (GetImpactForce(collision) > impactThreshold)
                {
                    ActivateRagdoll();
                }
            }
        }

        private float GetImpactForce(Collision collision)
        {
            return collision.impulse.magnitude / Mathf.Max(0.01f, bikeRigidbody.mass);
        }

        void ActivateRagdoll()
        {
            if (!CanActivateRagdoll())
            {
                return;
            }

            isRagdollActivated = true;

            Transform bikeTransform = bikeController.bikeReferences.LeanTransform;

            bikeRagdollInstance = Instantiate(dummyBikePrefab, bikeTransform.position, bikeTransform.rotation);
            characterRagdollInstance = Instantiate(characterRagdollPrefab, bikeTransform.position, bikeTransform.rotation);

            Animator ragdollAnimator = characterRagdollInstance.GetComponent<Animator>();
            if (ragdollAnimator && characterAnimator)
            {
                foreach (HumanBodyBones bone in (HumanBodyBones[])System.Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (bone == HumanBodyBones.LastBone) continue;

                    Transform characterBoneTransform = characterAnimator.GetBoneTransform(bone);
                    Transform ragdollBoneTransform = ragdollAnimator.GetBoneTransform(bone);

                    if (characterBoneTransform != null && ragdollBoneTransform != null)
                    {
                        ragdollBoneTransform.rotation = characterBoneTransform.rotation;
                    }
                }
            }

            Rigidbody[] bikeRagdollRigidbodies = bikeRagdollInstance.GetComponentsInChildren<Rigidbody>();
            Rigidbody[] characterRagdollRigidbodies = characterRagdollInstance.GetComponentsInChildren<Rigidbody>();

            Vector3 bikeVelocity = bikeRigidbody.linearVelocity;
            Vector3 bikeAngularVelocity = bikeRigidbody.angularVelocity;

            foreach (Rigidbody rb in bikeRagdollRigidbodies)
            {
                rb.linearVelocity = bikeVelocity;
                rb.angularVelocity = bikeAngularVelocity;
            }

            foreach (Rigidbody rb in characterRagdollRigidbodies)
            {
                rb.linearVelocity = bikeVelocity;
                rb.angularVelocity = bikeAngularVelocity;
            }

            gameObject.SetActive(false);

            onRagdollActivated.Invoke();
        }

        private bool CanActivateRagdoll()
        {
            return bikeController
                   && bikeController.bikeReferences != null
                   && bikeController.bikeReferences.LeanTransform
                   && bikeRigidbody
                   && dummyBikePrefab
                   && characterRagdollPrefab;
        }

        public void ReEnableBike()
        {
            if (!isRagdollActivated) return;

            if (bikeRagdollInstance)
            {
                Destroy(bikeRagdollInstance);
            }

            if (characterRagdollInstance)
            {
                Destroy(characterRagdollInstance);
            }

            gameObject.SetActive(true);
            if (bikeController)
            {
                bikeController.canAccelerate = true;
                if (bikeController.bikeAudio != null && bikeController.bikeAudio.engineSound)
                {
                    bikeController.bikeAudio.engineSound.pitch = bikeController.bikeAudio.minPitch;
                }

                if (bikeController.bikeReferences != null && bikeController.bikeReferences.LeanTransform)
                {
                    bikeController.bikeReferences.LeanTransform.localRotation = Quaternion.identity;
                }
            }

            if (bikeRigidbody)
            {
                bikeRigidbody.linearVelocity = Vector3.zero;
                bikeRigidbody.angularVelocity = Vector3.zero;
            }

            isRagdollActivated = false;

            onBikeReEnabled.Invoke();
        }

        public void setCameraTargetToRagdoll()
        {
            if (!characterRagdollInstance || !cameraController)
            {
                return;
            }

            Animator ragdollAnimator = characterRagdollInstance.GetComponent<Animator>();
            if (!ragdollAnimator)
            {
                return;
            }

            hipTransform = ragdollAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipTransform)
            {
                cameraController.SetCameratarget(hipTransform, hipTransform);
            }
        }

        public void resetCameratoBike()
        {
            if (cameraController)
            {
                cameraController.resetCameratarget();
            }
        }

        public void ForceActivateRagdoll()
        {
            if (!isRagdollActivated)
            {
                ActivateRagdoll();
            }
        }

        public void ResetBike()
        {
            if (isRagdollActivated)
            {
                ReEnableBike();
            }
        }
    }

}
