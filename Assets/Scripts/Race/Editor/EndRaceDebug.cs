using MotoSquid.UI;
using UnityEngine;
using UnityEditor;

namespace MotoSquid.Race
{
    public class EndRaceDebug : EditorWindow
    {
        float m_FinalTime = 92.4f;

        [MenuItem("MotoSquid/Debug/End Race Now")]
        static void Open() => GetWindow<EndRaceDebug>("End Race").minSize = new Vector2(320f, 190f);

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode and get racing (the player has to exist), then trigger the ending.\n\n" +
                "The manager ends only once per play session, so re-enter Play to try again.",
                MessageType.Info);

            m_FinalTime = EditorGUILayout.FloatField("Final Time (s)", m_FinalTime);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Trigger Victory"))  EditorApplication.delayCall += () => End(true);
                if (GUILayout.Button("Trigger Time Up")) EditorApplication.delayCall += () => End(false);
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play mode first.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Heads up: the manager tallies every ending into the save file (totalRaces, and winCount " +
                "on a victory) before anything else runs, and nothing can opt out of that. Debug endings " +
                "will inflate those stats.",
                MessageType.Warning);
        }

        void End(bool victory)
        {
            try
            {
                var manager = Object.FindFirstObjectByType<EndGameManager>();
                if (manager == null)
                {
                    Debug.LogWarning("End Race: no EndGameManager in the loaded scene.");
                    return;
                }

                manager.StartEndSequence(m_FinalTime, victory, false);
                Debug.Log($"End Race: triggered {(victory ? "VICTORY" : "TIME UP")} at {m_FinalTime:F1}s. " +
                          "If nothing happened, check the Console for the manager's own PlayerCam error.",
                          manager);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
