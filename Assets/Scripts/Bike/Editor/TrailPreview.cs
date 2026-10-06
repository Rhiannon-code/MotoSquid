using UnityEngine;
using UnityEditor;

namespace MotoSquid.Bike
{
    public class TrailPreview : EditorWindow
    {
        GameObject m_Bike;
        float m_SpeedKmh     = 300f;
        float m_DraftStrength = 1f;
        bool  m_ShowSlipstream = true;
        bool  m_ShowTailTrails = true;
        bool  m_FollowView     = true;

        bool    m_Running;
        Vector3 m_HomePosition;
        Vector3 m_HomePivot;
        bool    m_HadPivot;
        double  m_LastTime;
        float   m_Travelled;

        [MenuItem("MotoSquid/Trails/Preview Trails")]
        static void Open() => GetWindow<TrailPreview>("Trail Preview").minSize = new Vector2(320f, 260f);

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "A trail only draws where its emitter has been, so the bike has to move. Follow View " +
                "keeps the Scene camera on it, so it stays framed while the world streams past. The " +
                "scene is left dirty while running, press Stop before saving, and it puts the bike back.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(m_Running))
                m_Bike = (GameObject)EditorGUILayout.ObjectField("Bike", m_Bike, typeof(GameObject), true);

            if (!m_Running && GUILayout.Button("Use Selection") && Selection.activeGameObject != null)
                m_Bike = Selection.activeGameObject;

            EditorGUILayout.Space();
            m_SpeedKmh       = EditorGUILayout.Slider("Speed (km/h)", m_SpeedKmh, 0f, 500f);
            m_DraftStrength  = EditorGUILayout.Slider("Draft Strength", m_DraftStrength, 0f, 1f);
            m_ShowSlipstream = EditorGUILayout.Toggle("Slipstream", m_ShowSlipstream);
            m_ShowTailTrails = EditorGUILayout.Toggle("Tail Trails (boosting)", m_ShowTailTrails);
            m_FollowView     = EditorGUILayout.Toggle("Follow View", m_FollowView);

            EditorGUILayout.Space();
            if (m_Running)
            {
                if (GUILayout.Button("Stop")) EditorApplication.delayCall += Stop;
            }
            else if (GUILayout.Button("Start"))
            {
                EditorApplication.delayCall += Start;
            }

            if (m_Running)
                EditorGUILayout.LabelField("Travelled", $"{m_Travelled:F0} m");
        }

        void Start()
        {
            if (m_Bike == null)
            {
                Debug.LogWarning("TrailPreview: pick a bike in the scene first.");
                return;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(m_Bike))
            {
                Debug.LogWarning("TrailPreview: that is the prefab asset. Drag it into the " +
                                 "scene, or open it in Prefab Mode, and preview that instead.");
                return;
            }

            m_HomePosition = m_Bike.transform.position;

            SceneView sv = SceneView.lastActiveSceneView;
            m_HadPivot = sv != null;
            if (m_HadPivot) m_HomePivot = sv.pivot;

            m_Travelled    = 0f;
            m_LastTime     = EditorApplication.timeSinceStartup;
            m_Running      = true;
            EditorApplication.update += Tick;
        }

        void Stop()
        {
            EditorApplication.update -= Tick;
            m_Running = false;

            if (m_Bike == null) return;

            m_Bike.transform.position = m_HomePosition;

            SceneView sv = SceneView.lastActiveSceneView;
            if (m_HadPivot && sv != null) sv.pivot = m_HomePivot;

            foreach (var slip in m_Bike.GetComponentsInChildren<SlipstreamSystem>(true))
                slip.EditorPreviewEnd();

            foreach (var trail in m_Bike.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.emitting = false;
                trail.Clear();
            }

            SceneView.RepaintAll();
        }

        void Tick()
        {
            try
            {
                if (m_Bike == null) { Stop(); return; }

                double now = EditorApplication.timeSinceStartup;
                float  dt  = Mathf.Clamp((float)(now - m_LastTime), 0f, 0.1f);
                m_LastTime = now;

                float step = m_SpeedKmh / 3.6f * dt;
                m_Travelled += step;

                m_Bike.transform.position += m_Bike.transform.forward * step;

                foreach (var slip in m_Bike.GetComponentsInChildren<SlipstreamSystem>(true))
                    slip.EditorPreview(m_ShowSlipstream ? m_DraftStrength : 0f);

                foreach (var speed in m_Bike.GetComponentsInChildren<SpeedTrail>(true))
                    speed.EditorPreview(m_ShowTailTrails ? 1f : 0f);

                SceneView sv = SceneView.lastActiveSceneView;
                if (m_FollowView && sv != null) sv.pivot = m_Bike.transform.position;

                SceneView.RepaintAll();
                Repaint();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Stop();
            }
        }

        void OnDisable()
        {
            if (m_Running) Stop();
        }
    }
}
