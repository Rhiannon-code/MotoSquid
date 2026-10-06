using MotoSquid.Rider;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MotoSquid.Combat
{
    public static class CombatAnimInjector
    {
        const string TestRoot        = "Assets/Animations/Test";
        const string LegacyControllerPath = "Assets/Ash Assets/Arcade Bike Physics Pro/Resources/Biker Animation Controller.controller";
        const string MaskPath        = "Assets/Animations/CombatUpperMask.mask";
        const string FullMaskPath    = "Assets/Animations/CombatFullMask.mask";
        const string UpperLayerName  = "Combat Upper";
        const string FullLayerName   = "Combat Full";
        public const string CombatTag = "Combat";       
        public const string UpperTag  = "CombatUpper";  
        public const string HitTag    = "CombatHit";  

        static readonly string[] Triggers =
        {
            "PunchLeft", "PunchRight", "KickLeft", "KickRight",
            "Hit", "HitFront", "HitBack", "HitLeft", "HitRight",
        };

        static string _packRoot;

        [MenuItem("MotoSquid/Animation/Add Combat Clips To Rider Controllers")]
        public static void Inject()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[CombatAnim] Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }

            int done = 0;
            foreach (var style in RiderAnimatorGenerator.Styles)
            {
                _packRoot = $"{TestRoot}/{style}";
                if (!AssetDatabase.IsValidFolder(_packRoot))
                {
                    Debug.LogWarning($"[CombatAnim] No clip folder at {_packRoot} : {style} skipped.");
                    continue;
                }

                var rm = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    RiderAnimatorGenerator.OutputPathFor(style));
                if (rm == null)
                {
                    Debug.LogWarning($"[CombatAnim] RiderMaster_{style}.controller not found, generate it " +
                                     $"first (MotoSquid > Animation > Generate Rider Animator ({style})), " +
                                     "then re-run. That style's characters get no combat until you do.");
                    continue;
                }

                if (!TryLoadClips(style, out var c)) continue;

                ResetTriggers(rm);

                var fullSm = BuildMaskedLayer(rm, FullLayerName, FullMaskPath, allBody: true);
                AddMove(fullSm, fullSm.defaultState, "KickLeft",  c.kickLStart, c.kickLEnd, CombatTag);
                AddMove(fullSm, fullSm.defaultState, "KickRight", c.kickRStart, c.kickREnd, CombatTag);

                var upperSm = BuildMaskedLayer(rm, UpperLayerName, MaskPath, allBody: false);
                AddCombatUpper(upperSm, c);

                EditorUtility.SetDirty(rm);
                done++;
            }

            InjectLegacy();

            AssetDatabase.SaveAssets();
            Debug.Log($"[CombatAnim] Combat layers rebuilt on {done} RiderMaster controller(s). " +
                      "The weapon swing states are named PunchLeft/PunchRight and play AS_Long_Weapon_*; " +
                      "MeleeWeaponHolder swaps that clip per weapon via an override controller, so a new " +
                      "weapon does NOT need this re-run. Leave Loop Time OFF on all combat clips.");
        }

        static void InjectLegacy()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(LegacyControllerPath);
            if (ctrl == null) return;

            _packRoot = $"{TestRoot}/SuperSport";
            if (!TryLoadClips("SuperSport", out var c)) return;

            var sm = ctrl.layers[0].stateMachine;
            var sitting = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Sitting");
            if (sitting == null)
            {
                Debug.LogWarning("[CombatAnim] Legacy Biker controller has no 'Sitting' state, skipped.");
                return;
            }

            foreach (var cs in sm.states.Where(s => s.state.tag == CombatTag || s.state.tag == UpperTag
                                                 || s.state.tag == HitTag).ToArray())
                sm.RemoveState(cs.state);
            for (int i = ctrl.layers.Length - 1; i >= 1; i--)
                if (ctrl.layers[i].name == UpperLayerName) ctrl.RemoveLayer(i);
            ResetTriggers(ctrl);

            AddMove(sm, sitting, "KickLeft",  c.kickLStart, c.kickLEnd, CombatTag);
            AddMove(sm, sitting, "KickRight", c.kickRStart, c.kickREnd, CombatTag);
            var upperSm = BuildMaskedLayer(ctrl, UpperLayerName, MaskPath, allBody: false);
            AddCombatUpper(upperSm, c);

            EditorUtility.SetDirty(ctrl);
            Debug.Log("[CombatAnim] Legacy 'Biker Animation Controller' also updated (procedural path).");
        }

        static void AddCombatUpper(AnimatorStateMachine sm, Clips c)
        {
            AddMove(sm, sm.defaultState, "PunchLeft",  c.weaponLeft,  null, UpperTag);
            AddMove(sm, sm.defaultState, "PunchRight", c.weaponRight, null, UpperTag);
            AddMove(sm, sm.defaultState, "HitFront", c.hitFront, null, HitTag, 1f, 0.08f);
            AddMove(sm, sm.defaultState, "HitBack",  c.hitBack,  null, HitTag, 1f, 0.08f);
            AddMove(sm, sm.defaultState, "HitLeft",  c.hitLeft,  null, HitTag, 1f, 0.08f);
            AddMove(sm, sm.defaultState, "HitRight", c.hitRight, null, HitTag, 1f, 0.08f);
            AddMove(sm, sm.defaultState, "Hit",      c.hitFront, null, HitTag, 1f, 0.08f);
        }

        struct Clips
        {
            public AnimationClip weaponLeft, weaponRight;
            public AnimationClip kickLStart, kickLEnd, kickRStart, kickREnd;
            public AnimationClip hitFront, hitBack, hitLeft, hitRight;
        }

        static bool TryLoadClips(string style, out Clips c)
        {
            c = new Clips
            {
                weaponLeft  = FindClip("AS_Long_Weapon_Left"),
                weaponRight = FindClip("AS_Long_Weapon_Right"),
                kickLStart  = FindClip("AS_Kick_Left_Start"),
                kickLEnd    = FindClip("AS_Kick_Left_End"),
                kickRStart  = FindClip("AS_Kick_Right_Start"),
                kickREnd    = FindClip("AS_Kick_Right_End"),
                hitFront    = FindClip("AS_Get_Hit_Front"),
                hitBack     = FindClip("AS_Get_Hit_Back"),
                hitLeft     = FindClip("AS_Get_Hit_Left"),
                hitRight    = FindClip("AS_Get_Hit_Right"),
            };

            var missing = new[]
            {
                (c.weaponLeft, "AS_Long_Weapon_Left"), (c.weaponRight, "AS_Long_Weapon_Right"),
                (c.kickLStart, "AS_Kick_Left_Start"),  (c.kickLEnd, "AS_Kick_Left_End"),
                (c.kickRStart, "AS_Kick_Right_Start"), (c.kickREnd, "AS_Kick_Right_End"),
                (c.hitFront, "AS_Get_Hit_Front"),      (c.hitBack, "AS_Get_Hit_Back"),
                (c.hitLeft, "AS_Get_Hit_Left"),        (c.hitRight, "AS_Get_Hit_Right"),
            }.Where(x => x.Item1 == null).Select(x => x.Item2).ToArray();

            if (missing.Length == 0) return true;
            Debug.LogError($"[CombatAnim] {style}: missing clip(s) {string.Join(", ", missing)}. " +
                           $"Searched recursively under {_packRoot}.");
            return false;
        }

        static void ResetTriggers(AnimatorController ctrl)
        {
            foreach (var p in ctrl.parameters.Where(p => Triggers.Contains(p.name)).ToArray())
                ctrl.RemoveParameter(p);
            foreach (var t in Triggers)
                ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
        }

        static AnimatorStateMachine BuildMaskedLayer(AnimatorController ctrl, string layerName,
            string maskPath, bool allBody)
        {
            for (int i = ctrl.layers.Length - 1; i >= 1; i--)
                if (ctrl.layers[i].name == layerName) ctrl.RemoveLayer(i);

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, allBody);
            if (!allBody)
            {
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            }
            EditorUtility.SetDirty(mask);

            var sm = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, ctrl);
            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                stateMachine = sm,
                avatarMask = mask,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });
            var idle = sm.AddState("Idle"); 
            sm.defaultState = idle;
            return sm;
        }

        static void AddMove(AnimatorStateMachine sm, AnimatorState home, string trigger,
            AnimationClip clip, AnimationClip endClip, string tag, float speed = 1f,
            float enterDuration = 0.25f)  
        {
            var state = sm.AddState(trigger);
            state.motion = clip;
            state.tag = tag;
            state.speed = speed;

            var enter = sm.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = enterDuration;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);

            var last = state;
            if (endClip != null)
            {
                var end = sm.AddState(trigger + "End");
                end.motion = endClip;
                end.tag = tag;
                var chain = state.AddTransition(end);
                chain.hasExitTime = true; chain.exitTime = 0.85f; chain.duration = 0.1f;
                last = end;
            }

            var back = last.AddTransition(home);
            back.hasExitTime = true; back.exitTime = 0.85f; back.duration = 0.25f;
        }

        static AnimationClip FindClip(string fbxName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{fbxName} t:Model", new[] { _packRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != fbxName) continue;  
                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null) return clip;
            }
            return null;
        }
    }
}
