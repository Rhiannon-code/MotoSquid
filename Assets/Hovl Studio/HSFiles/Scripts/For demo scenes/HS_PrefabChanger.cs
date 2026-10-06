using UnityEngine;
using UnityEngine.InputSystem;

namespace Hovl
{
    public class HS_PrefabChanger : MonoBehaviour
    {
        public GameObject[] prefabs;

        int currentIndex = 0;

        void Start()
        {
            if (prefabs == null || prefabs.Length == 0) return;

            // Ensure all prefabs are disabled, then enable the first one
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                    prefabs[i].SetActive(false);
            }

            currentIndex = 0;
            if (prefabs[0] != null)
                prefabs[0].SetActive(true);
        }

        private void Update()
        {
            bool nextPressed = false;
            bool previousPressed = false;

#if ENABLE_INPUT_SYSTEM
            // New Input System
            if (Keyboard.current != null)
            {
                nextPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame;
                previousPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            // Legacy Input Manager
            nextPressed = Input.GetKeyDown(KeyCode.RightArrow);
            previousPressed = Input.GetKeyDown(KeyCode.LeftArrow);
#endif

            if (nextPressed)
            {
                NextPrefab();
            }
            else if (previousPressed)
            {
                PrevPrefab();
            }
        }

        // Call from UI buttons if needed
        public void NextPrefab()
        {
            if (prefabs == null || prefabs.Length == 0) return;

            // Disable current
            if (prefabs[currentIndex] != null)
                prefabs[currentIndex].SetActive(false);

            // Advance and wrap
            currentIndex = (currentIndex + 1) % prefabs.Length;

            // Enable new
            if (prefabs[currentIndex] != null)
                prefabs[currentIndex].SetActive(true);
        }

        public void PrevPrefab()
        {
            if (prefabs == null || prefabs.Length == 0) return;

            // Disable current
            if (prefabs[currentIndex] != null)
                prefabs[currentIndex].SetActive(false);

            // Step back and wrap
            currentIndex = (currentIndex - 1 + prefabs.Length) % prefabs.Length;

            // Enable new
            if (prefabs[currentIndex] != null)
                prefabs[currentIndex].SetActive(true);
        }
    }
}