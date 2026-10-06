using UnityEngine;
using Unity.Cinemachine;


namespace ArcadeBP_Pro
{
    public class CameraController : MonoBehaviour
    {
        [Tooltip("Array of Cinemachine virtual cameras.")]
        public CinemachineCamera[] cameras;

        [Tooltip("Reference to the bike controller.")]
        public ArcadeBikeControllerPro bikeController;

        [Tooltip("Speed threshold above which the camera shake effect starts.")]
        public float speedThresholdForShake = 50f;

        [Tooltip("Amplitude of the camera shake effect.")]
        public float shakeAmplitude = 1.2f;

        [Tooltip("Frequency of the camera shake effect.")]
        public float shakeFrequency = 2.0f;

        [Tooltip("Key to switch between different cameras.")]
        public KeyCode switchCameraKey = KeyCode.C;

        [Tooltip("Button to switch between different cameras.")]
        public UiButton_ABP_Pro switchCameraButton;

        [Tooltip("Minimum camera FOV value.")]
        public float minFOV = 60f;

        [Tooltip("Maximum camera FOV value.")]
        public float maxFOV = 80f;

        [Tooltip("Smoothing factor for the camera FOV value.")]
        public float FOV_smoother = 5f;


        private CinemachineBasicMultiChannelPerlin[] cameraNoise;
        private int currentCameraIndex = 0;
        private bool isShaking = false;

        private Transform[] initialCameraFollowTargets;
        private Transform[] initialCameraLookAtTargets;
        private float smoothFOV = 60f;

        private void Awake()
        {
            transform.parent = null;
            smoothFOV = 60f;

            if (!bikeController)
            {
                enabled = false;
                return;
            }

            transform.name = "CameraController_" + RemovePrefix(bikeController.transform.name, "ABP_Pro");
        }

        void Start()
        {
            if (cameras == null || cameras.Length == 0)
            {
                enabled = false;
                return;
            }

            cameraNoise = new CinemachineBasicMultiChannelPerlin[cameras.Length];

            initialCameraFollowTargets = new Transform[cameras.Length];
            initialCameraLookAtTargets = new Transform[cameras.Length];

            for (int i = 0; i < cameras.Length; i++)
            {
                if (!cameras[i])
                {
                    continue;
                }

                cameras[i].gameObject.SetActive(i == currentCameraIndex);
                cameraNoise[i] = cameras[i].GetComponent<CinemachineBasicMultiChannelPerlin>();

                initialCameraFollowTargets[i] = cameras[i].Follow;
                initialCameraLookAtTargets[i] = cameras[i].LookAt;
            }
        }

        void Update()
        {
            if (!bikeController || cameras == null || cameras.Length == 0 || !cameras[currentCameraIndex])
            {
                return;
            }

            bool switchCamera = switchCameraButton?.isPressed == true;
#if ENABLE_LEGACY_INPUT_MANAGER
            switchCamera |= Input.GetKeyDown(switchCameraKey);
#endif
            if (switchCamera)
            {
                SwitchCamera();
            }

            float bikeSpeed = bikeController.localBikeVelocity.magnitude;

            if (bikeSpeed > speedThresholdForShake && bikeController.isActiveAndEnabled)
            {
                UpdateShake(bikeSpeed);
            }
            else if (isShaking)
            {
                StopShake();
            }

            UpdateFOV(bikeSpeed);
        }

        void SwitchCamera()
        {
            if (cameras == null || cameras.Length == 0)
            {
                return;
            }

            if (cameras[currentCameraIndex])
            {
                cameras[currentCameraIndex].gameObject.SetActive(false);
            }

            int nextCameraIndex = GetNextValidCameraIndex();
            if (nextCameraIndex < 0)
            {
                return;
            }

            currentCameraIndex = nextCameraIndex;
            cameras[currentCameraIndex].gameObject.SetActive(true);

            StopShake();
        }

        private int GetNextValidCameraIndex()
        {
            for (int i = 1; i <= cameras.Length; i++)
            {
                int candidateIndex = (currentCameraIndex + i) % cameras.Length;
                if (cameras[candidateIndex])
                {
                    return candidateIndex;
                }
            }

            return -1;
        }

        void UpdateShake(float bikeSpeed)
        {
            isShaking = true;
            if (cameraNoise[currentCameraIndex] != null)
            {
                float t = Mathf.InverseLerp(speedThresholdForShake, bikeController.bikeSettings.maxSpeed, bikeSpeed);
                cameraNoise[currentCameraIndex].AmplitudeGain = Mathf.Lerp(0, shakeAmplitude, t);
                cameraNoise[currentCameraIndex].FrequencyGain = Mathf.Lerp(0, shakeFrequency, t);
            }
        }

        void StopShake()
        {
            isShaking = false;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameraNoise != null && cameraNoise[i] != null)
                {
                    cameraNoise[i].AmplitudeGain = 0f;
                    cameraNoise[i].FrequencyGain = 0f;
                }
            }
        }


        void UpdateFOV(float bikeSpeed)
        {
            float t = Mathf.InverseLerp(0, Mathf.Max(1f, bikeController.bikeSettings.maxSpeed), bikeSpeed);
            float newFOV = Mathf.Lerp(minFOV, maxFOV, t);

            smoothFOV = Mathf.Lerp(smoothFOV, newFOV, Time.deltaTime * FOV_smoother);

            cameras[currentCameraIndex].Lens.FieldOfView = smoothFOV;
        }


        public void SetCameratarget(Transform followTarget, Transform lookAtTarget)
        {
            if (cameras == null)
            {
                return;
            }

            foreach (var camera in cameras)
            {
                if (!camera)
                {
                    continue;
                }

                camera.LookAt = lookAtTarget;
                camera.Follow = followTarget;
            }

            StopShake();
        }

        public void resetCameratarget()
        {
            if (cameras == null || initialCameraFollowTargets == null || initialCameraLookAtTargets == null)
            {
                return;
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                if (!cameras[i])
                {
                    continue;
                }

                cameras[i].Follow = initialCameraFollowTargets[i];
                cameras[i].LookAt = initialCameraLookAtTargets[i];
            }

            StopShake();
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
