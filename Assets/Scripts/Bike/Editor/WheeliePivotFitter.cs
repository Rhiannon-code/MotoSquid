using MotoSquid.DevTools;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Bike
{
    public static class WheeliePivotFitter
    {
        const string Tag = "[WheeliePivot]";
        const string Pivot = "Wheelie Transform";

        static readonly string[] RearWheelNames = { "Back_Wheel", "Rear_Wheel" };

        static readonly string[] Folders =
        {
            "Assets/Prefabs",
            "Assets/Prefabs/Characters",
        };

        [MenuItem("MotoSquid/Characters/Fit Wheelie Pivots (Donors + All Characters)")]
        public static void Fit()
        {
            if (!EditorUtility.DisplayDialog("Fit wheelie pivots",
                    "Moves '" + Pivot + "' onto the rear wheel's contact patch on every bike prefab, " +
                    "counter moving its children so nothing shifts at rest.\n\n" +
                    "Touches the bike rig on donors and every built character. Commit first. Continue?",
                    "Fit", "Cancel"))
            {
                return;
            }

            var paths = Folders.Where(AssetDatabase.IsValidFolder)
                .SelectMany(f => AssetDatabase.FindAssets("t:Prefab", new[] { f }))
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .OrderByDescending(p => p.EndsWith("/Player1.prefab") || p.EndsWith("/Voodoo.prefab"))
                .ToList();

            int moved = 0, fine = 0;
            foreach (var path in paths)
            {
                switch (FitOne(path))
                {
                    case 1: moved++; break;
                    case 0: fine++; break;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Moved {moved} pivot(s), {fine} already correct. " +
                      "Rebuild characters so built prefabs inherit the donor fix.");
        }

        // 1 moved, 0 already correct, -1 not applicable
        static int FitOne(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return -1;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pivot = FindDeep(root.transform, Pivot);
                var wheel = RearWheelNames.Select(n => FindDeep(root.transform, n)).FirstOrDefault(t => t != null);
                if (pivot == null || wheel == null) return -1;

                var local = pivot.InverseTransformPoint(wheel.position);
                var correction = new Vector3(local.x, 0f, local.z);
                if (correction.magnitude < 0.005f) return 0;

                var children = pivot.Cast<Transform>().ToArray();
                var keep = children.Select(c => c.position).ToArray();
                var keepRot = children.Select(c => c.rotation).ToArray();

                pivot.position = pivot.TransformPoint(correction);

                for (int i = 0; i < children.Length; i++)
                {
                    children[i].position = keep[i];
                    children[i].rotation = keepRot[i];
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"{Tag} {System.IO.Path.GetFileName(path)}: pivot moved {correction.magnitude * 100f:0.0} cm " +
                          $"({correction.x * 100f:0.0}, {correction.z * 100f:0.0}) onto the rear contact patch; " +
                          $"{children.Length} child(ren) held in place.");
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }

            return null;
        }
    }
}
