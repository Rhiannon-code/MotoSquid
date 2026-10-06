using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.Cameras
{
    [ExecuteAlways]
    [DefaultExecutionOrder(32000)]
    [RequireComponent(typeof(Camera))]
    public class CameraFarClipLock : MonoBehaviour
    {
        public float farClipPlane  = 10000f;
        public bool  lockNearClip  = false;
        public float nearClipPlane = 0.1f;
        public bool  reportFirstCorrection = true;

        Camera _cam;
        CinemachineBrain _brain;
        bool _reported;

        void Awake()
        {
            _cam   = GetComponent<Camera>();
            _brain = GetComponent<CinemachineBrain>();
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            if (!Mathf.Approximately(_cam.farClipPlane, farClipPlane))
            {
                if (reportFirstCorrection && !_reported)
                {
                    _reported = true;
                    string live = _brain != null && _brain.ActiveVirtualCamera != null
                        ? _brain.ActiveVirtualCamera.Name : "<no live vcam>";
                    Debug.Log($"[CameraFarClipLock] '{name}' far clip was {_cam.farClipPlane}, forcing " +
                              $"{farClipPlane}. Live vcam at that moment: {live}", this);
                }
                _cam.farClipPlane = farClipPlane;
            }

            if (lockNearClip && !Mathf.Approximately(_cam.nearClipPlane, nearClipPlane))
                _cam.nearClipPlane = nearClipPlane;
        }

        // Adding this by hand kept being missed, and a camera without it silently reverts. Every camera in
        // every loaded scene gets one, including cameras on inactive objects and ones spawned with a bike.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            InstallAll();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InstallAll();

        static void InstallAll()
        {
            var cams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cams)
                if (cam.GetComponent<CameraFarClipLock>() == null)
                    cam.gameObject.AddComponent<CameraFarClipLock>();
        }
    }
}
