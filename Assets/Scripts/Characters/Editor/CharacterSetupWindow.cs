using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Characters
{
    public class CharacterSetupWindow : EditorWindow
    {
        CharacterDefinition _def;
        CharacterLibrary    _library;
        Vector2 _scroll;
        List<string> _problems;
        string _lastValidated;

        [MenuItem("MotoSquid/Characters/Character Setup %#m")]
        public static void Open()
        {
            var w = GetWindow<CharacterSetupWindow>("Character Setup");
            w.minSize = new Vector2(460, 560);
            w.Show();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("MotoSquid: Character Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Prepare the art (see the checklist below, this part is manual).\n" +
                "2. Generate the animation controllers for the riding style (once per style).\n" +
                "3. Fill in a Character Definition and press Build.",
                MessageType.None);

            EditorGUILayout.Space();
            DrawStyleSection();
            EditorGUILayout.Space();
            DrawDefinitionSection();
            EditorGUILayout.Space();
            DrawLibrarySection();
            EditorGUILayout.Space();
            DrawManualChecklist();

            EditorGUILayout.EndScrollView();
        }

        void DrawStyleSection()
        {
            EditorGUILayout.LabelField("Step 2: Animation controllers (shared per style)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Every character riding the same style shares these two controllers. Regenerate only " +
                "when the clip set changes, rebuilding characters afterwards is not required, they " +
                "reference the controller asset.", MessageType.None);

            foreach (RidingStyle style in System.Enum.GetValues(typeof(RidingStyle)))
            {
                var s = style;
                bool riderOk = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    CharacterBuilder.RiderControllerPath(s)) != null;
                bool bikeOk = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    CharacterBuilder.BikeControllerPath(s)) != null;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{s}", GUILayout.Width(90));
                EditorGUILayout.LabelField(riderOk ? "rider ✔" : "rider ", GUILayout.Width(60));
                EditorGUILayout.LabelField(bikeOk ? "bike ✔" : "bike ", GUILayout.Width(60));
                if (GUILayout.Button(riderOk && bikeOk ? "Regenerate" : "Generate"))
                    GenerateStyle(s);
                EditorGUILayout.EndHorizontal();
            }
        }

        static void GenerateStyle(RidingStyle style)
        {
            string s = style.ToString();
            LeanClipGenerator.Generate(s);
            RiderAnimatorGenerator.Generate(s);
            BikeAnimatorGenerator.Generate(s);
            CombatAnimInjector.Inject();
            Debug.Log($"[CharacterSetup] Generated the {s} animation stack " +
                      "(lean clip -> rider controller -> bike controller -> combat states).");
        }

        void DrawDefinitionSection()
        {
            EditorGUILayout.LabelField("Step 3: Build a character", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _def = (CharacterDefinition)EditorGUILayout.ObjectField(
                "Character Definition", _def, typeof(CharacterDefinition), false);
            if (EditorGUI.EndChangeCheck()) _problems = null;

            if (GUILayout.Button("Create New Character Definition…")) CreateDefinition();

            if (_def == null)
            {
                EditorGUILayout.HelpBox("Assign or create a Character Definition to continue.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Validate", GUILayout.Height(22)))
            {
                _problems = CharacterBuilder.Validate(_def);
                _lastValidated = _def.characterName;
            }

            if (_problems != null && _lastValidated == _def.characterName) DrawProblems(_problems);

            EditorGUILayout.Space(4);
            bool blocked = _problems != null && _lastValidated == _def.characterName
                        && CharacterBuilder.HasBlockingProblems(_problems);
            using (new EditorGUI.DisabledScope(blocked))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Build Player", GUILayout.Height(26)))
                    RunBuild(() => CharacterBuilder.Build(_def, CharacterBuilder.Role.Player));
                if (GUILayout.Button("Build AI", GUILayout.Height(26)))
                    RunBuild(() => CharacterBuilder.Build(_def, CharacterBuilder.Role.AI));
                if (GUILayout.Button("Build Both", GUILayout.Height(26)))
                    RunBuild(() => CharacterBuilder.BuildBoth(_def));
                EditorGUILayout.EndHorizontal();
            }
            if (blocked)
                EditorGUILayout.HelpBox("Build is disabled until the blocking problems above are fixed.",
                                        MessageType.Warning);

            if (_def.builtPlayerPrefab != null || _def.builtAIPrefab != null)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Last build", EditorStyles.miniBoldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Player", _def.builtPlayerPrefab, typeof(GameObject), false);
                    EditorGUILayout.ObjectField("AI",     _def.builtAIPrefab,     typeof(GameObject), false);
                }
            }
        }

        void RunBuild(System.Func<bool> build)
        {
            if (build()) _problems = CharacterBuilder.Validate(_def);
            Repaint();
        }

        void DrawProblems(List<string> problems)
        {
            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox("Ready to build, no problems found.", MessageType.Info);
                return;
            }

            var blocking = problems.Where(p => !p.StartsWith("[warning]")).ToArray();
            var warnings = problems.Where(p =>  p.StartsWith("[warning]"))
                                   .Select(p => p.Substring("[warning]".Length).Trim()).ToArray();

            if (blocking.Length > 0)
                EditorGUILayout.HelpBox("MUST FIX BY HAND:\n\n• " + string.Join("\n\n• ", blocking),
                                        MessageType.Error);
            if (warnings.Length > 0)
                EditorGUILayout.HelpBox("Will build, but incomplete:\n\n• " + string.Join("\n\n• ", warnings),
                                        MessageType.Warning);
        }

        void CreateDefinition()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Character Definition", "CharacterDefinition", "asset",
                "Where should the definition asset live?",
                "Assets/MotoSquid/Data/Characters");
            if (string.IsNullOrEmpty(path)) return;

            var def = CreateInstance<CharacterDefinition>();
            def.characterName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            _def = def;
            _problems = null;
            Selection.activeObject = def;
        }

        void DrawLibrarySection()
        {
            EditorGUILayout.LabelField("Batch: whole roster", EditorStyles.boldLabel);
            _library = (CharacterLibrary)EditorGUILayout.ObjectField(
                "Character Library", _library, typeof(CharacterLibrary), false);

            using (new EditorGUI.DisabledScope(_library == null || _library.Count == 0))
            {
                if (GUILayout.Button("Validate All"))
                {
                    int bad = 0;
                    foreach (var d in _library.characters)
                    {
                        if (d == null) continue;
                        var p = CharacterBuilder.Validate(d);
                        if (!CharacterBuilder.HasBlockingProblems(p)) continue;
                        bad++;
                        Debug.LogError($"[CharacterSetup] '{d.characterName}' is not buildable:\n  " +
                                       string.Join("\n  ", p.Where(x => !x.StartsWith("[warning]"))));
                    }
                    Debug.Log($"[CharacterSetup] Validated {_library.Count} character(s): " +
                              $"{_library.Count - bad} ready, {bad} blocked.");
                }

                if (GUILayout.Button("Build All (Player + AI)"))
                {
                    int ok = 0;
                    foreach (var d in _library.characters)
                    {
                        if (d == null) continue;
                        if (CharacterBuilder.BuildBoth(d)) ok++;
                    }
                    Debug.Log($"[CharacterSetup] Built {ok}/{_library.Count} character(s).");
                }
            }
        }

        void DrawManualChecklist()
        {
            EditorGUILayout.LabelField("Step 1: What YOU have to do (the tool cannot)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
"CHARACTER MODEL\n" +
"1. Import the FBX. In the Inspector: Rig > Animation Type = Humanoid > Apply.\n" +
"2. Rig > Configure: every bone in the solid green skeleton must be mapped, hips, spine,\n" +
"   both arms (upper/lower/hand) and both legs (upper/lower/foot). Unmapped = no IK.\n" +
"3. Drag the configured FBX into the scene, then into a folder to make it a PREFAB.\n" +
"   Assign that prefab to the definition's riderPrefab. (An FBX alone will not do.)\n" +
"\n" +
"BIKE MODEL\n" +
"4. Name the wheel transforms Front_Wheel and Back_Wheel (or Rear_Wheel), exactly.\n" +
"5. Add five empty child GameObjects with these EXACT names, and POSITION them:\n" +
"      SeatAnchor      where the rider's hips sit\n" +
"      GripAnchor_L    left handlebar grip     GripAnchor_R   right handlebar grip\n" +
"      FootPegGrip_L   left foot peg           FootPegGrip_R  right foot peg\n" +
"   These are the only thing placing the rider. Eyeball them in the Scene view, the tool\n" +
"   has no way to guess where a grip is on an arbitrary mesh.\n" +
"6. Save the bike as a PREFAB and assign it to bikePrefab.\n" +
"\n" +
"ANIMATION CLIP IMPORT SETTINGS (once per clip set, not per character)\n" +
"7. LOOP TIME ON for every *_Loop clip plus AS_Idle_Mounted, AS_Idle_Riding, AS_Brake.\n" +
"   LOOP TIME OFF for one shots: *_Start, *_End, AS_Mounted_to_Ride, AS_Ride_to_Mounted\n" +
"   and every combat clip. A looping one shot re-triggers; a non looping loop freezes.\n" +
"\n" +
"WEAPONS\n" +
"8. Create the weapon prop as a prefab, then a MeleeWeapon asset (Create > MotoSquid >\n" +
"   Melee Weapon). Assign the prop and set which hand holds it.\n" +
"9. Position/rotation in the hand is per weapon and must be eyeballed: build a character,\n" +
"   look at the grip in the Scene view, adjust the weapon's localPosition/localEuler,\n" +
"   repeat. The tool cannot know where a model's handle is.\n" +
"\n" +
"AFTER BUILDING: verify by eye, none of this can be checked automatically\n" +
"10. Both wheels touching the ground (suspension is tuned for the donor's wheel sizes).\n" +
"11. Hands on the grips, feet on the pegs, knees not through the tank.\n" +
"12. Steering turns the fork; the rider leans with the bike, not through it.",
                MessageType.None);

        }
    }
}
