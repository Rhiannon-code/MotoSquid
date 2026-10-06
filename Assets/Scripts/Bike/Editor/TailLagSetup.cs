using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;

namespace MotoSquid.Bike
{
    public static class TailLagSetup
    {
        const string MENU = "MotoSquid/Trails/Set Up Tail Lag Trails";

        [MenuItem(MENU)]
        static void Run()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                EditorUtility.DisplayDialog("Tail Lag Setup",
                    "Open the bike prefab first (double click it so you are in Prefab Mode), then run " +
                    "this again. It only touches the prefab you have open.", "OK");
                return;
            }

            GameObject root = stage.prefabContentsRoot;
            EditorApplication.delayCall += () => Setup(root);
        }

        static void Setup(GameObject root)
        {
            try
            {
                if (root == null) return;

                Transform light = FindByName(root.transform, "Taillight");
                if (light == null)
                {
                    Debug.LogError("TailLagSetup: no 'Taillight' under this prefab, so there is nothing " +
                                   "to anchor the trails to.", root);
                    return;
                }

                SpeedTrail speed = root.GetComponentInChildren<SpeedTrail>(true);
                if (speed == null)
                {
                    Debug.LogError("TailLagSetup: no SpeedTrail on this prefab.", root);
                    return;
                }

                var existing = new System.Collections.Generic.List<TrailLag>(
                    root.GetComponentsInChildren<TrailLag>(true));

                int done = 0;
                foreach (TrailRenderer tr in root.GetComponentsInChildren<TrailRenderer>(true))
                {
                    if (tr == null || !tr.name.StartsWith("Trail")) continue;
                    if (existing.Exists(l => l != null && l.GetComponent<TrailRenderer>() == tr)) continue;

                    if (Convert(tr, light, speed, existing)) done++;
                }

                int spread = Spread(root, light);

                EditorSceneManager.MarkSceneDirty(root.scene);
                Debug.Log($"TailLagSetup: converted {done}, spread {spread} across the tail light. " +
                          "Check them in the Scene view, then Ctrl+S to save the prefab.", root);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        const float LAMP_SPREAD = 0.12f;

        static int Spread(GameObject root, Transform light)
        {
            TrailLag[] lags = root.GetComponentsInChildren<TrailLag>(true);
            System.Array.Sort(lags, (a, b) => string.CompareOrdinal(a.name, b.name));

            for (int i = 0; i < lags.Length; i++)
            {
                float t = lags.Length == 1 ? 0.5f : (float)i / (lags.Length - 1);
                Undo.RecordObject(lags[i].transform, "Tail Lag Setup");
                lags[i].transform.localPosition = new Vector3(
                    Mathf.Lerp(-LAMP_SPREAD, LAMP_SPREAD, t), light.localPosition.y, light.localPosition.z);
                EditorUtility.SetDirty(lags[i]);
            }

            return lags.Length;
        }

        static bool Convert(TrailRenderer trail, Transform light, SpeedTrail speed,
                            System.Collections.Generic.List<TrailLag> existing)
        {
            Transform anchor = trail.transform;

            Undo.RecordObject(anchor, "Tail Lag Setup");
            anchor.localPosition = new Vector3(anchor.localPosition.x, light.localPosition.y,
                                               light.localPosition.z);

            TrailLag lag = existing.Find(l => l != null && l.name == anchor.name.Replace("Trail", "TailLag"));
            if (lag == null)
            {
                var go = new GameObject(anchor.name.Replace("Trail", "TailLag"));
                Undo.RegisterCreatedObjectUndo(go, "Tail Lag Setup");
                go.transform.SetParent(anchor.parent, false);
                lag = Undo.AddComponent<TrailLag>(go);
                existing.Add(lag);
            }

            lag.transform.localPosition = anchor.localPosition;
            lag.transform.localRotation = anchor.localRotation;

            if (lag.GetComponent<TrailRenderer>() == null)
            {
                ComponentUtility.CopyComponent(trail);
                ComponentUtility.PasteComponentAsNew(lag.gameObject);
            }

            TrailRenderer moved = lag.GetComponent<TrailRenderer>();
            if (moved == null)
            {
                Debug.LogError($"TailLagSetup: could not copy the TrailRenderer onto '{lag.name}'.", lag);
                return false;
            }

            Undo.RecordObject(speed, "Tail Lag Setup");
            for (int i = 0; i < speed.trails.Length; i++)
                if (speed.trails[i] == trail) speed.trails[i] = moved;

            Undo.DestroyObjectImmediate(trail);
            return true;
        }

        static Transform FindByName(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
