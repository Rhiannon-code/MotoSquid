using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MotoSquid.DevTools
{
    public class RigReview : MonoBehaviour
    {
        public Transform[] bikes = new Transform[0];
        public string[] labels = new string[0];
        public Camera reviewCamera;

        public Vector3 cameraOffset = new Vector3(0f, 2.2f, -6f);
        public float cameraLerp = 6f;
        public bool soloActiveBike = true;

        int index;

        void Start()
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, bikes.Length - 1));
            ApplySolo();
            PlaceCamera(1f);
        }

        void Update()
        {
            if (bikes.Length == 0) return;

            if (PressedNext()) Step(1);
            else if (PressedPrev()) Step(-1);

            PlaceCamera(Time.deltaTime * cameraLerp);
        }

        void Step(int dir)
        {
            index = (index + dir + bikes.Length) % bikes.Length;
            ApplySolo();
        }

        void ApplySolo()
        {
            if (!soloActiveBike) return;
            for (int i = 0; i < bikes.Length; i++)
                if (bikes[i] != null) bikes[i].gameObject.SetActive(i == index);
        }

        void PlaceCamera(float t)
        {
            if (reviewCamera == null || bikes.Length == 0) return;
            var target = bikes[index];
            if (target == null) return;

            Vector3 want = target.TransformPoint(cameraOffset);
            reviewCamera.transform.position = Vector3.Lerp(reviewCamera.transform.position, want, Mathf.Clamp01(t));
            reviewCamera.transform.rotation = Quaternion.Slerp(reviewCamera.transform.rotation,
                Quaternion.LookRotation(target.position + Vector3.up * 0.9f - reviewCamera.transform.position),
                Mathf.Clamp01(t));
        }

        [Tooltip("Debug overlay: which bike is showing.")]
        public bool showReadout;

        void OnGUI()
        {
            if (showReadout) Draw();
        }

        void Draw()
        {
            if (bikes.Length == 0) return;
            string name = (index < labels.Length && !string.IsNullOrEmpty(labels[index])) ? labels[index] : "?";
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(14, 10, 900, 28), string.Format("[{0}/{1}]  {2}", index + 1, bikes.Length, name), style);
            GUI.Label(new Rect(14, 36, 900, 22), "[ / ] cycle bike     W/S throttle   A/D steer   Space handbrake   LeftShift wheelie");
        }

        static bool PressedNext()
        {
            if (!Application.isEditor) return false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current.rightBracketKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.RightBracket);
#else
            return false;
#endif
        }

        static bool PressedPrev()
        {
            if (!Application.isEditor) return false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current.leftBracketKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.LeftBracket);
#else
            return false;
#endif
        }
    }
}
