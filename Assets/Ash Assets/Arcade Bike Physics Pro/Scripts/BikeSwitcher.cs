using UnityEngine;
using UnityEngine.UI;


namespace ArcadeBP_Pro
{
    public class BikeSwitcher : MonoBehaviour
    {
        public ArcadeBikeControllerPro[] bikes;
        public Button nextBikeButton;
        public Button previousBikeButton;
        private int currentBikeIndex = 0;

        void Awake()
        {
            // Disable all bikes at the start, then enable the first one
            foreach (var bike in bikes)
            {
                bike.gameObject.SetActive(false);
                if (bike.bikeReferences.cameraController != null)
                {
                    bike.bikeReferences.cameraController.gameObject.SetActive(false);
                }
            }

            if (bikes.Length > 0)
            {
                EnableBike(currentBikeIndex);
            }

            // Add listeners to the buttons
            if (nextBikeButton != null)
            {
                nextBikeButton.onClick.AddListener(SwitchToNextBike);
            }

            if (previousBikeButton != null)
            {
                previousBikeButton.onClick.AddListener(SwitchToPreviousBike);
            }
        }

        public void SwitchToNextBike()
        {
            DisableBike(currentBikeIndex);
            currentBikeIndex = (currentBikeIndex + 1) % bikes.Length;
            EnableBike(currentBikeIndex);
        }

        public void SwitchToPreviousBike()
        {
            DisableBike(currentBikeIndex);
            currentBikeIndex = (currentBikeIndex - 1 + bikes.Length) % bikes.Length;
            EnableBike(currentBikeIndex);
        }

        public ArcadeBikeControllerPro GetCurrentBike()
        {
            if (bikes.Length > 0 && currentBikeIndex >= 0 && currentBikeIndex < bikes.Length)
            {
                return bikes[currentBikeIndex];
            }
            return null;
        }

        private void EnableBike(int index)
        {
            if (index >= 0 && index < bikes.Length)
            {
                bikes[index].gameObject.SetActive(true);
                bikes[index].bikeReferences.ragdollActivator.ReEnableBike();
                if (bikes[index].bikeReferences.cameraController != null)
                {
                    bikes[index].bikeReferences.cameraController.gameObject.SetActive(true);
                    bikes[index].bikeReferences.cameraController.transform.name = "CameraController_" + RemovePrefix(bikes[index].transform.name, "ABP_Pro");
                }
            }
        }

        private void DisableBike(int index)
        {
            if (index >= 0 && index < bikes.Length)
            {
                bikes[index].gameObject.SetActive(false);
                if (bikes[index].bikeReferences.cameraController != null)
                {
                    bikes[index].bikeReferences.cameraController.gameObject.SetActive(false);
                }
            }
        }

        private string RemovePrefix(string originalName, string prefix)
        {
            if (originalName.StartsWith(prefix))
            {
                return originalName.Substring(prefix.Length);
            }
            return originalName;
        }
    }

}
