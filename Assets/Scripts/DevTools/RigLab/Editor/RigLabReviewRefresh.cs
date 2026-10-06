using MotoSquid.Bike;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabReviewRefresh
    {
        static Camera ReviewCamera(List<Transform> bikes)
        {
            if (Camera.main != null && !IsOnABike(Camera.main, bikes)) return Camera.main;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!IsOnABike(c, bikes)) return c;
            return null;
        }

        static bool IsOnABike(Camera cam, List<Transform> bikes)
        {
            if (cam == null) return false;
            foreach (var b in bikes) if (b != null && cam.transform.IsChildOf(b)) return true;
            return false;
        }

        [MenuItem("Tools/Rig Lab/1b. Refresh Review Bike List", false, 1)]
        static void Refresh()
        {
            var review = Object.FindFirstObjectByType<RigReview>();
            if (review == null)
            {
                EditorUtility.DisplayDialog("Rig Lab",
                    "No Rig Review in the open scene. Run '1. Build Review Scene' first.", "OK");
                return;
            }

            var found = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                              .Select(b => b.transform).ToList();
            if (found.Count == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }

            var ordered = new List<Transform>();
            foreach (var t in review.bikes) if (t != null && found.Contains(t)) ordered.Add(t);
            foreach (var t in found) if (!ordered.Contains(t)) ordered.Add(t);

            Undo.RecordObject(review, "Refresh review list");
            for (int i = 0; i < ordered.Count; i++)
            {
                Undo.RecordObject(ordered[i], "Refresh review list");
                ordered[i].position = new Vector3(i * RigLab.Spacing, ordered[i].position.y, 0f);
            }

            int added = ordered.Count - review.bikes.Count(t => t != null);
            review.bikes = ordered.ToArray();
            review.labels = ordered.Select(t => t.name).ToArray();
            if (review.reviewCamera == null || IsOnABike(review.reviewCamera, ordered))
                review.reviewCamera = ReviewCamera(ordered);

            EditorUtility.SetDirty(review);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Rig Lab: review list now holds " + ordered.Count + " bike(s)" +
                      (added > 0 ? ", " + added + " newly added" : "") + ":\n   " +
                      string.Join("\n   ", review.labels));
        }
    }
}
