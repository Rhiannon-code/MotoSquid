using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.Cameras
{
    [DefaultExecutionOrder(32000)]
    [RequireComponent(typeof(Camera))]
    public class IntroOcclusionGuard : MonoBehaviour
    {
        // Held for at least this long: the Brain reports no blend on the first frames, before the intro
        // camera has taken over, so restoring on IsBlending alone would switch culling back on instantly
        public float minDisabledSeconds = 1.5f;

        Camera _cam;
        CinemachineBrain _brain;
        bool  _wasEnabled;
        bool  _restored;
        float _elapsed;

        void Awake()
        {
            _cam   = GetComponent<Camera>();
            _brain = GetComponent<CinemachineBrain>();

            if (_cam == null) return;
            _wasEnabled = _cam.useOcclusionCulling;
            _cam.useOcclusionCulling = false;
        }

        void LateUpdate()
        {
            if (_restored || _cam == null) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < minDisabledSeconds) return;
            if (_brain != null && _brain.IsBlending) return;

            _cam.useOcclusionCulling = _wasEnabled;
            _restored = true;

            Debug.Log($"[IntroOcclusionGuard] '{name}' intro blend finished, occlusion culling restored " +
                      $"to {_wasEnabled}.", this);
        }

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
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.GetComponent<IntroOcclusionGuard>() == null)
                    cam.gameObject.AddComponent<IntroOcclusionGuard>();
        }
    }
}
