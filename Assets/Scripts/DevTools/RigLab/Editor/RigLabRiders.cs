using MotoSquid.Bike;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public class RigLabRiders : EditorWindow
    {
        GameObject choice;
        bool selectedOnly;
        bool keepExisting;
        Vector2 scroll;

        [MenuItem("Tools/Rig Lab/2. Swap Riders", false, 1)]
        static void Open()
        {
            GetWindow<RigLabRiders>("Swap Riders").minSize = new Vector2(420, 300);
        }

        internal static BikeAnimationController[] AllOn(BikeController bike) =>
            AllOn(bike != null ? bike.gameObject : null);

        internal static BikeAnimationController[] AllOn(GameObject bikeRoot)
        {
            var model = RigLabScope.BikeModelOf(bikeRoot);
            return model == null
                ? new BikeAnimationController[0]
                : model.GetComponentsInChildren<BikeAnimationController>(true);
        }

        internal static BikeAnimationController ActiveOn(BikeController bike) =>
            ActiveOn(bike != null ? bike.gameObject : null);

        internal static BikeAnimationController ActiveOn(GameObject bikeRoot)
        {
            var all = AllOn(bikeRoot);
            foreach (var c in all) if (c.gameObject.activeInHierarchy) return c;
            return all.Length > 0 ? all[0] : null;
        }

        internal static List<GameObject> Characters()
        {
            return All().Where(m => Rig(m.Key) == ModelImporterAnimationType.Human)
                        .Select(m => m.Value).ToList();
        }

        static Dictionary<string, GameObject> All()
        {
            var found = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", RigLab.ModelFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Replace('\\', '/').Contains("/Weapons/")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) found[path] = go;
            }
            return found;
        }

        static ModelImporterAnimationType Rig(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            return mi == null ? ModelImporterAnimationType.None : mi.animationType;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Swap Riders", EditorStyles.boldLabel);

            bool inPrefab = PrefabStageUtility.GetCurrentPrefabStage() != null;
            EditorGUILayout.HelpBox(
                inPrefab
                    ? "Writing to THE OPEN PREFAB. This is what you want."
                    : "Writing to THE OPEN SCENE. Bikes that are prefab instances will be SKIPPED, " +
                      "open the bike prefab from the Project window first.",
                inPrefab ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.HelpBox(
                "Replaces the rider on each bike and re-runs the vendor rig builder.\n" +
                "Afterwards run 15 (base pose), 13 (combat rig) and 0 (check) swapping rebuilds " +
                "the rider, so anything built on the old one is gone.", MessageType.None);

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var kv in All().OrderBy(k => k.Key))
            {
                var type = Rig(kv.Key);
                bool ok = type == ModelImporterAnimationType.Human;

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!ok))
                        if (GUILayout.Toggle(choice == kv.Value, Path.GetFileNameWithoutExtension(kv.Key),
                                             EditorStyles.radioButton, GUILayout.Width(220)) && ok)
                            choice = kv.Value;

                    if (ok) EditorGUILayout.LabelField("Humanoid", EditorStyles.miniLabel);
                    else if (GUILayout.Button(type + " , fix rig", EditorStyles.miniButton))
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(kv.Key);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "A model listed as Generic cannot be a rider. Select it, then in the Inspector set " +
                "Rig > Animation Type = Humanoid and Avatar Definition = Create From This Model, and Apply.",
                MessageType.Info);

            int selected = RigLabScope.Bikes(true)
                                 .Count(b => IsSelected(b.gameObject));
            selectedOnly = EditorGUILayout.Toggle("Selected bikes only (" + selected + ")", selectedOnly);
            keepExisting = EditorGUILayout.Toggle("Keep riders already there", keepExisting);
            if (keepExisting)
                EditorGUILayout.HelpBox("Adds this character alongside the ones already on the bike and " +
                    "deactivates the others. Run 15 and 13 afterwards, they cover every rider, then " +
                    "cycle characters in Play with the , and . keys.", MessageType.None);
            if (selectedOnly && selected == 0)
                EditorGUILayout.HelpBox("Select one or more bikes in the Hierarchy.", MessageType.Warning);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(choice == null))
            {
                if (GUILayout.Button("Swap in " + (choice == null ? "-" : choice.name), GUILayout.Height(26)))
                    Swap();
                if (GUILayout.Button("Remove " + (choice == null ? "-" : choice.name) + " from the bikes"))
                    Remove();
            }
        }

        void Remove()
        {
            int riders = 0, sets = 0;
            foreach (var bike in RigLabScope.Bikes(true))
            {
                if (selectedOnly && !IsSelected(bike.gameObject)) continue;

                foreach (var ctrl in AllOn(bike))
                {
                    if (ctrl.gameObject.name != choice.name) continue;

                    var donor = bike.bikeReferences != null ? bike.bikeReferences.bikeAnimationTargets : null;
                    var parent = donor != null ? donor.transform.parent : null;
                    if (parent != null)
                    {
                        var set = parent.Find(RiderSwitch.SetPrefix + ctrl.gameObject.name);
                        if (set != null) { Undo.DestroyObjectImmediate(set.gameObject); sets++; }
                    }

                    Undo.DestroyObjectImmediate(ctrl.gameObject);
                    riders++;
                }
            }

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: removed " + riders + " " + choice.name + " rider(s) and " + sets +
                      " target set(s).\n" +
                      "Run 13 to refresh the character switcher, then 0 to confirm.");
        }

        static bool IsSelected(GameObject go) => RigLabScope.IsSelected(go);

        void Swap()
        {
            RigLabScope.ClearSelectionForEdit();
            var bikes = RigLabScope.Bikes(true);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in " + RigLabScope.Where() + "."); return; }

            int done = 0, skipped = 0;
            foreach (var bike in bikes)
            {
                if (selectedOnly && !IsSelected(bike.gameObject)) continue;
                if (RigLab.SwapRiderOn(bike, choice, keepExisting)) done++; else skipped++;
            }

            RigLabRiderTargets.Split();

            RigLabScope.MarkDirty();
            Debug.Log("Rig Lab: " + choice.name + " onto " + done + " bike(s), skipped " + skipped +
                      ", targets re-split. Now run 15, then 13, then 0.");
        }
    }
}
