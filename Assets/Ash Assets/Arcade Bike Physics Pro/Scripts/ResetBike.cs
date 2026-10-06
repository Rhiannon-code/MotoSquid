using UnityEngine;
using UnityEngine.UI;


namespace ArcadeBP_Pro
{
    public class ResetBike : MonoBehaviour
    {
        public BikeSwitcher bikeSwitcher;
        public Button UnRagdollBikeButton;
        public Button resetBikeButton;

        private void Start()
        {
            if (UnRagdollBikeButton != null)
            {
                UnRagdollBikeButton.onClick.AddListener(UnRagdollBike);
            }

            if (resetBikeButton != null)
            {
                resetBikeButton.onClick.AddListener(ResetCurrentBike);
            }
        }

        private void OnDestroy()
        {
            if (UnRagdollBikeButton != null)
            {
                UnRagdollBikeButton.onClick.RemoveListener(UnRagdollBike);
            }

            if (resetBikeButton != null)
            {
                resetBikeButton.onClick.RemoveListener(ResetCurrentBike);
            }
        }

        private void UnRagdollBike()
        {
            if (!bikeSwitcher)
            {
                return;
            }

            ArcadeBikeControllerPro currentBike = bikeSwitcher.GetCurrentBike();
            if (currentBike != null)
            {
                RagdollActivator ragdollActivator = currentBike.bikeReferences.ragdollActivator;
                if (ragdollActivator != null)
                {
                    ragdollActivator.ReEnableBike();
                }
            }
        }

        private void ResetCurrentBike()
        {
            if (!bikeSwitcher)
            {
                return;
            }

            ArcadeBikeControllerPro currentBike = bikeSwitcher.GetCurrentBike();
            if (currentBike != null && currentBike.bikeReferences != null)
            {
                if (currentBike.bikeReferences.BikeRb)
                {
                    currentBike.bikeReferences.BikeRb.linearVelocity = Vector3.zero;
                    currentBike.bikeReferences.BikeRb.angularVelocity = Vector3.zero;
                }

                if (currentBike.bikeReferences.Rotator)
                {
                    currentBike.bikeReferences.Rotator.transform.localRotation = Quaternion.identity;
                }

                currentBike.transform.localPosition = Vector3.zero;

                RagdollActivator ragdollActivator = currentBike.bikeReferences.ragdollActivator;
                if (ragdollActivator != null)
                {
                    ragdollActivator.ReEnableBike();
                }
            }
        }
    }

}
