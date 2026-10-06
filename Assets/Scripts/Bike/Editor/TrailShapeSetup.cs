using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MotoSquid.Bike
{
    public static class TrailShapeSetup
    {
        // Pencil thin off the bike, then a hard flare at the tail, the cone silhouette
        static AnimationCurve Wake() => new AnimationCurve(
            new Keyframe(0f,    0.02f, 0f,    0.12f),
            new Keyframe(0.62f, 0.16f, 0.30f, 0.30f),
            new Keyframe(1f,    1f,    3.4f,  0f));

        // The inverse, widest at the lamp, dropping away fast, then a long point
        static AnimationCurve Lamp() => new AnimationCurve(
            new Keyframe(0f,    1f,    0f,     -1.6f),
            new Keyframe(0.45f, 0.30f, -1.1f,  -1.1f),
            new Keyframe(1f,    0f,    -0.55f,  0f));

        const float LAMP_SECONDS = 0.20f;                  // ~17 m at 300 km/h
        const float WAKE_SECONDS = LAMP_SECONDS * 2f;      // ~33 m
        const float LAMP_START   = LAMP_SECONDS * 0.3f;

        [MenuItem("MotoSquid/Trails/Apply Trail Shapes")]
        static void Run()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                EditorUtility.DisplayDialog("Trail Shapes",
                    "Open the bike prefab first (double click it so you are in Prefab Mode), then run " +
                    "this again. It only touches the prefab you have open.", "OK");
                return;
            }

            GameObject root = stage.prefabContentsRoot;
            EditorApplication.delayCall += () => Apply(root);
        }

        static void Apply(GameObject root)
        {
            try
            {
                if (root == null) return;
                var log = new System.Text.StringBuilder("Trail shapes applied:");

                foreach (var slip in root.GetComponentsInChildren<SlipstreamSystem>(true))
                {
                    Undo.RecordObject(slip, "Apply Trail Shapes");
                    slip.trailShape = Wake();
                    slip.trailTime  = WAKE_SECONDS;
                    EditorUtility.SetDirty(slip);
                    log.Append($"\n  slipstream: shape 0.02 -> 0.16 @0.62 -> 1.0, length {WAKE_SECONDS}s");
                }

                foreach (var speed in root.GetComponentsInChildren<SpeedTrail>(true))
                {
                    Undo.RecordObject(speed, "Apply Trail Shapes");
                    speed.trailShape = Lamp();
                    speed.trailTime  = LAMP_SECONDS;
                    speed.startTime  = LAMP_START;
                    log.Append($"\n  tail light: shape 1.0 -> 0.30 @0.45 -> 0, length {LAMP_SECONDS}s");
                    EditorUtility.SetDirty(speed);
                }

                EditorSceneManager.MarkSceneDirty(root.scene);
                log.Append("\n\nCtrl+S to save the prefab.");
                Debug.Log(log.ToString(), root);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
