using MotoSquid.Bike;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace MotoSquid.Art
{
    // Art Test scene only: F1 cycles the moods, F2 toggles the world ink outline, F3 keeps the boost meter full
    public class ArtTestControls : MonoBehaviour
    {
        public GameObject[] moods;
        public CustomPassVolume inkOutline;
        public BoostSystem boost;
        public bool infiniteBoost = true;

        int m_Mood;

        void Start() => ShowMood(m_Mood);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) NextMood();
                if (kb.f2Key.wasPressedThisFrame && inkOutline != null) inkOutline.enabled = !inkOutline.enabled;
                if (kb.f3Key.wasPressedThisFrame) infiniteBoost = !infiniteBoost;
            }
            if (infiniteBoost && boost != null) boost.boostMeter = 1f;
        }

        [ContextMenu("Next Mood")]
        public void NextMood()
        {
            for (int i = 0; i < moods.Length; i++)
                if (moods[i].activeSelf) m_Mood = i;
            ShowMood((m_Mood + 1) % moods.Length);
        }

        public void ShowMood(int index)
        {
            m_Mood = index;
            for (int i = 0; i < moods.Length; i++) moods[i].SetActive(i == index);
        }

        void OnGUI()
        {
            string ink = inkOutline != null && inkOutline.enabled ? "on" : "off";
            GUI.Label(new Rect(10f, 10f, 700f, 24f),
                $"[Art Test] F1 mood: {moods[m_Mood].name}   F2 world ink: {ink}   F3 infinite boost: {(infiniteBoost ? "on" : "off")}   Boost: Caps Lock / A");
        }
    }
}
