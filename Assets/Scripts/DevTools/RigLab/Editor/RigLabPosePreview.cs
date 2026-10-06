using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public class RigLabPosePreview : EditorWindow
    {
        FullPose track;
        Animator rider;
        float t;
        bool playing;
        bool silenceRig = true;
        double lastTick;

        Transform[] bound;
        FullPose boundTo;
        Animator boundRider;
        readonly List<Quaternion> restRot = new List<Quaternion>();
        readonly List<Vector3> restPos = new List<Vector3>();

        [MenuItem("Tools/Rig Lab/23. Preview Captured Pose", false, 221)]
        static void Open()
        {
            GetWindow<RigLabPosePreview>("Pose Preview").minSize = new Vector2(420, 230);
        }

        void OnEnable()
        {
            EditorApplication.update += Tick;
            if (rider == null)
            {
                var bike = Object.FindFirstObjectByType<BikeController>();
                if (bike != null) rider = bike.GetComponentInChildren<Animator>(true);
            }
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            Restore();
        }

        void Tick()
        {
            if (!playing || track == null) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastTick);
            lastTick = now;
            t = Mathf.Repeat(t + dt / Mathf.Max(0.01f, track.duration), 1f);
            Apply();
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Pose Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Plays a captured track on the rider in the scene. If this looks like the animation, " +
                "the capture is correct and anything still wrong is in how the rig applies it.",
                MessageType.None);

            EditorGUILayout.Space();
            var newTrack = (FullPose)EditorGUILayout.ObjectField("Track", track, typeof(FullPose), false);
            var newRider = (Animator)EditorGUILayout.ObjectField("Rider", rider, typeof(Animator), true);
            if (newTrack != track || newRider != rider) { Restore(); track = newTrack; rider = newRider; }

            silenceRig = EditorGUILayout.Toggle("Silence rig while previewing", silenceRig);

            if (track != null)
                EditorGUILayout.LabelField("Captured on", track.sampledOn + "   " + track.frameCount +
                                           " frames, " + track.BoneCount + " bones");

            using (new EditorGUI.DisabledScope(track == null || rider == null))
            {
                EditorGUILayout.Space();
                float nt = EditorGUILayout.Slider("Time", t, 0f, 1f);
                if (!Mathf.Approximately(nt, t)) { t = nt; Apply(); }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(playing ? "Pause" : "Play"))
                {
                    playing = !playing;
                    lastTick = EditorApplication.timeSinceStartup;
                }
                if (GUILayout.Button("Frame 0")) { t = 0f; Apply(); }
                EditorGUILayout.EndHorizontal();

                if (track != null && rider != null)
                    EditorGUILayout.LabelField("Frame", Mathf.RoundToInt(t * (track.frameCount - 1)) + " / " + (track.frameCount - 1));

                if (GUILayout.Button("Restore rider")) Restore();
            }

            if (bound != null)
            {
                int missing = bound.Count(b => b == null);
                if (missing > 0)
                    EditorGUILayout.HelpBox(missing + " of " + bound.Length +
                        " bone paths did not resolve on this rider, the track was captured on a different skeleton.",
                        MessageType.Error);
            }
        }

        void Bind()
        {
            if (track == null || rider == null) return;
            if (boundTo == track && boundRider == rider && bound != null) return;

            Restore();
            var root = rider.transform;
            bound = track.bonePaths.Select(p => root.Find(p)).ToArray();
            boundTo = track; boundRider = rider;

            restRot.Clear(); restPos.Clear();
            foreach (var b in bound)
            {
                restRot.Add(b != null ? b.localRotation : Quaternion.identity);
                restPos.Add(b != null ? b.localPosition : Vector3.zero);
            }

            if (silenceRig)
                foreach (var r in rider.GetComponentsInChildren<Rig>(true)) r.weight = 0f;
        }

        void Apply()
        {
            Bind();
            if (bound == null || track == null) return;

            for (int i = 0; i < bound.Length; i++)
            {
                if (bound[i] == null) continue;
                Quaternion r; Vector3 p;
                if (!track.Sample(i, t, out r, out p)) continue;
                bound[i].localRotation = r;
                bound[i].localPosition = p;
            }
            SceneView.RepaintAll();
        }

        void Restore()
        {
            if (bound == null) return;
            for (int i = 0; i < bound.Length && i < restRot.Count; i++)
            {
                if (bound[i] == null) continue;
                bound[i].localRotation = restRot[i];
                bound[i].localPosition = restPos[i];
            }
            bound = null; boundTo = null; boundRider = null;
            SceneView.RepaintAll();
        }
    }
}
