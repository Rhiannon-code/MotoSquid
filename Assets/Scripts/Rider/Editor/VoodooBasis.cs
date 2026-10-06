using MotoSquid.Bike;
using MotoSquid.Characters;
using MotoSquid.Combat;
using MotoSquid.DevTools;
using MotoSquid.Race;
using MotoSquid.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotoSquid.Rider
{
    public class VoodooBasis : EditorWindow
    {
        const string DefDir     = "Assets/MotoSquid/Data/Characters";
        const string CharDir    = "Assets/Prefabs/Characters";
        const string RiderPath  = CharDir + "/VoodooRigged.prefab";
        const string BikeSSPath = CharDir + "/VoodooBike_R.prefab";
        const string BikeUPPath = CharDir + "/Futuristic_Bike.prefab";

        class Slot
        {
            public string Name;
            public GameObject Bike;
            public RidingStyle Style;
            public CharacterDefinition Def;
        }

        readonly List<Slot> _slots = new List<Slot>();
        GameObject _rider;
        Vector2    _scroll;
        string     _status = "";

        [MenuItem("MotoSquid/Characters/Voodoo Test Basis")]
        static void Open()
        {
            var w = GetWindow<VoodooBasis>("Voodoo Basis");
            w.minSize = new Vector2(470, 430);
            w.Reset();
        }

        void Reset()
        {
            _rider = AssetDatabase.LoadAssetAtPath<GameObject>(RiderPath);
            var ss = AssetDatabase.LoadAssetAtPath<GameObject>(BikeSSPath);
            var up = AssetDatabase.LoadAssetAtPath<GameObject>(BikeUPPath);

            string[] names = { "Voodoo", "Torq", "Bliss", "Mace", "Slick", "Soul" };
            _slots.Clear();
            for (int i = 0; i < names.Length; i++)
            {
                bool superSport = i % 2 == 0;
                _slots.Add(new Slot
                {
                    Name  = names[i],
                    Bike  = superSport ? ss : up,
                    Style = superSport ? RidingStyle.SuperSport : RidingStyle.Upright,
                    Def   = AssetDatabase.LoadAssetAtPath<CharacterDefinition>($"{DefDir}/CharacterDefinition_{names[i]}.asset")
                });
            }
            Repaint();
        }

        // Definitions
        void CreateDefinitions()
        {
            if (!AssetDatabase.IsValidFolder(DefDir))
                AssetDatabase.CreateFolder("Assets/MotoSquid/Data", "Characters");

            int made = 0, updated = 0;
            foreach (var s in _slots)
            {
                string path = $"{DefDir}/CharacterDefinition_{s.Name}.asset";
                var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                bool isNew = def == null;
                if (isNew)
                {
                    def = ScriptableObject.CreateInstance<CharacterDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }

                def.characterName = s.Name;
                def.riderPrefab   = _rider;
                def.bikePrefab    = s.Bike;
                def.style         = s.Style;
                def.playerDonor   = null;
                def.aiDonor       = null;
                def.outputFolder  = CharDir;

                EditorUtility.SetDirty(def);
                s.Def = def;
                if (isNew) made++; else updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _status = $"Definitions: {made} created, {updated} updated, in {DefDir}";
            Repaint();
        }

        // Build
        void BuildAll()
        {
            var log = new StringBuilder();
            int built = 0, skipped = 0;

            foreach (var s in _slots)
            {
                if (s.Def == null) { log.AppendLine($"x {s.Name}: no definition, create them first"); skipped++; continue; }

                var problems = CharacterBuilder.Validate(s.Def);
                if (CharacterBuilder.HasBlockingProblems(problems))
                {
                    log.AppendLine($"x {s.Name}: {string.Join("; ", problems)}");
                    skipped++;
                    continue;
                }

                if (CharacterBuilder.BuildBoth(s.Def)) { built++; log.AppendLine($"+ {s.Name}"); }
                else { skipped++; log.AppendLine($"x {s.Name}: build returned false, see Console"); }
            }

            AssetDatabase.SaveAssets();
            _status = $"Built {built}, skipped {skipped}";
            EditorUtility.DisplayDialog("Voodoo Test Basis", $"Built {built}, skipped {skipped}.\n\n{log}", "OK");
            Reset();
        }

        // Scene swap
        void SwapScene(CharacterDefinition playerDef, CharacterDefinition aiDef)
        {
            var race = Object.FindFirstObjectByType<RaceManager>();
            if (race == null) { _status = "No RaceManager in the open scene."; return; }
            if (playerDef == null || playerDef.builtPlayerPrefab == null) { _status = "Player definition has no built prefab, build first."; return; }

            var log = new StringBuilder();
            var oldPlayer = race.playerTransform;

            var spawned = (GameObject)PrefabUtility.InstantiatePrefab(playerDef.builtPlayerPrefab);
            Undo.RegisterCreatedObjectUndo(spawned, "Swap player bike");
            if (oldPlayer != null)
            {
                spawned.transform.SetPositionAndRotation(oldPlayer.position, oldPlayer.rotation);
                spawned.transform.localScale = oldPlayer.localScale;
                Undo.RecordObject(oldPlayer.gameObject, "Disable old player");
                oldPlayer.gameObject.SetActive(false);
                log.AppendLine($"• disabled {oldPlayer.name}, spawned {spawned.name} at its transform");
            }
            else log.AppendLine($"• spawned {spawned.name} (no previous playerTransform to match)");

            Undo.RecordObject(race, "Repoint RaceManager");
            race.playerTransform = spawned.transform;
            if (aiDef != null && aiDef.builtAIPrefab != null)
            {
                race.aiPrefab = aiDef.builtAIPrefab;
                log.AppendLine($"• RaceManager.aiPrefab -> {aiDef.builtAIPrefab.name}");
            }
            EditorUtility.SetDirty(race);
            log.AppendLine("• RaceManager.playerTransform repointed");

            var hud = Object.FindFirstObjectByType<HUDManager>();
            if (hud == null) log.AppendLine("! no HUDManager in scene, p1Bike/p1BoostSystem not repointed");
            else
            {
                var so    = new SerializedObject(hud);
                var bike  = spawned.GetComponentInChildren<BikeController>(true);
                var boost = spawned.GetComponentInChildren<BoostSystem>(true);

                if (bike != null)  { so.FindProperty("p1Bike").objectReferenceValue = bike; log.AppendLine("• HUD p1Bike repointed"); }
                else log.AppendLine("! new bike has no BikeController, HUD p1Bike left alone");

                if (boost != null) { so.FindProperty("p1BoostSystem").objectReferenceValue = boost; log.AppendLine("• HUD p1BoostSystem repointed"); }
                else log.AppendLine("! new bike has no BoostSystem, HUD p1BoostSystem left alone");

                so.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(race.gameObject.scene);
            _status = "Scene swapped, review, then Ctrl+S. Ctrl+Z reverts.";
            EditorUtility.DisplayDialog("Voodoo Test Basis",
                log + "\nNothing was saved. Check the scene, then Ctrl+S to keep it or Ctrl+Z to undo.", "OK");
        }

        void RunDeferred(string what, System.Action action)
        {
            EditorApplication.delayCall += () =>
            {
                try { action(); }
                catch (System.Exception e)
                {
                    _status = $"{what} failed: {e.Message}";
                    Debug.LogException(e);
                    EditorUtility.DisplayDialog("Voodoo Test Basis",
                        $"{what} failed:\n\n{e.Message}\n\nFull stack trace is in the Console.", "OK");
                }
                Repaint();
            };
        }

        void OnGUI()
        {
            if (_slots.Count == 0) Reset();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("1 Source art", EditorStyles.boldLabel);
            _rider = (GameObject)EditorGUILayout.ObjectField("Rider (all slots)", _rider, typeof(GameObject), false);
            if (_rider == null)
                EditorGUILayout.HelpBox($"Rider prefab not found at {RiderPath}.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2 Slots", EditorStyles.boldLabel);
            foreach (var s in _slots)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(s.Name, GUILayout.Width(70));
                    s.Bike  = (GameObject)EditorGUILayout.ObjectField(s.Bike, typeof(GameObject), false, GUILayout.Width(150));
                    s.Style = (RidingStyle)EditorGUILayout.EnumPopup(s.Style, GUILayout.Width(90));
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField(s.Def, typeof(CharacterDefinition), false);
                    GUI.enabled = true;
                }
                if (s.Bike == null)
                    EditorGUILayout.HelpBox($"{s.Name}: no bike prefab assigned.", MessageType.Warning);
                else if (s.Bike.GetComponent<Animator>() == null)
                    EditorGUILayout.HelpBox($"{s.Name}: '{s.Bike.name}' has no Animator on its root. The builder " +
                                            "needs one to hang BikeMaster on, add an Animator component to that " +
                                            "prefab before building.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Create/update the six definitions")) RunDeferred("Create definitions", CreateDefinitions);
            if (GUILayout.Button("Validate + build all (Player + AI)")) RunDeferred("Build", BuildAll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("3 Swap the open scene", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Spawns the built player prefab at the current player's transform, disables the old one, " +
                "and repoints RaceManager.playerTransform, RaceManager.aiPrefab and HUDManager's " +
                "p1Bike/p1BoostSystem. Undo able, and does not save the scene.",
                MessageType.None);

            var first  = _slots.Count > 0 ? _slots[0].Def : null;  
            var second = _slots.Count > 1 ? _slots[1].Def : null; 
            GUI.enabled = first != null && first.builtPlayerPrefab != null;
            if (GUILayout.Button("Swap player to Voodoo, AI prefab to Torq")) RunDeferred("Scene swap", () => SwapScene(first, second));
            GUI.enabled = true;
            if (first != null && first.builtPlayerPrefab == null)
                EditorGUILayout.HelpBox("Build the definitions first, no built player prefab yet.", MessageType.Info);

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Still manual", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("• Futuristic_Bike's five anchors + wheel names", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Humanoid bone mapping on any new rider", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Eyeballing hands/feet/lean after each build", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }
    }
}
