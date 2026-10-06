using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MotoSquid.Rider
{

    public static class RiderAnimatorGenerator
    {
        const string TestRoot = "Assets/Animations/Test";

        public static readonly string[] Styles = { "SuperSport", "Upright" };
        static string PackRootFor(string style)          => $"{TestRoot}/{style}";
        public static string OutputPathFor(string style) => $"{TestRoot}/RiderMaster_{style}.controller";
        public static string[] OutputPaths => Styles.Select(OutputPathFor).ToArray();
        static string _packRoot;
        const string CombatUpperMaskPath = "Assets/Animations/CombatUpperMask.mask";
        const string LeanClipName        = "AS_Lean_Additive";
        const float  LeanLayerWeight      = 0.3f;

        [MenuItem("MotoSquid/Animation/Generate Rider Animator (SuperSport)")]
        public static void GenerateSuperSport() => Generate("SuperSport");

        [MenuItem("MotoSquid/Animation/Generate Rider Animator (Upright)")]
        public static void GenerateUpright() => Generate("Upright");
        public static void Generate(string style)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[RiderAnimator] Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }

            _packRoot         = PackRootFor(style);
            string outputPath = OutputPathFor(style);

            var mounted      = FindClip("AS_Idle_Mounted");
            var mountToRide  = FindClip("AS_Mounted_to_Ride");
            var idleRiding   = FindClip("AS_Idle_Riding");
            var drive        = FindClip("AS_Drive_Fast_Loop");
            var rideToMount  = FindClip("AS_Ride_to_Mounted");
            var turnLeft     = FindClip("AS_Turn_V1_Left_Loop");
            var turnRight    = FindClip("AS_Turn_V1_Right_Loop");
            var turnLeftHard  = FindClip("AS_Turn_V2_Left_Loop");
            var turnRightHard = FindClip("AS_Turn_V2_Right_Loop");
            var brake        = FindClip("AS_Brake");
            var wheelieStart = FindClip("AS_Wheelie_Start");
            var wheelieLoop  = FindClip("AS_Wheelie_Loop");
            var wheelieEnd   = FindClip("AS_Wheelie_End");
            var airLoop      = FindClip("AS_In_Air_Jump_Loop");
            var airEnd       = FindClip("AS_In_Air_Jump_End");
            var reverseLoop  = FindClip("AS_Drive_Reverse_Right_Loop");

            var missing = new[]
            {
                (mounted, "AS_Idle_Mounted"), (mountToRide, "AS_Mounted_to_Ride"),
                (idleRiding, "AS_Idle_Riding"), (drive, "AS_Drive_Fast_Loop"),
                (rideToMount, "AS_Ride_to_Mounted"),
                (turnLeft, "AS_Turn_V1_Left_Loop"), (turnRight, "AS_Turn_V1_Right_Loop"),
                (brake, "AS_Brake"),
                (wheelieStart, "AS_Wheelie_Start"), (wheelieLoop, "AS_Wheelie_Loop"),
                (wheelieEnd, "AS_Wheelie_End"),
                (airLoop, "AS_In_Air_Jump_Loop"), (airEnd, "AS_In_Air_Jump_End"),
                (reverseLoop, "AS_Drive_Reverse_Right_Loop"),
            }.Where(x => x.Item1 == null).Select(x => x.Item2).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[RiderAnimator] Missing clip(s): {string.Join(", ", missing)}. " +
                               $"Searched recursively under {_packRoot}.");
                return;
            }

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(outputPath);
            ctrl.AddParameter("Speed",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Riding",   AnimatorControllerParameterType.Bool);  
            ctrl.AddParameter("Steer",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Wheelie",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Braking",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Reverse",  AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var sMounted   = sm.AddState("Mounted");       sMounted.motion   = mounted;   sMounted.tag   = "Mounted";
            var sMountRide = sm.AddState("MountedToRide"); sMountRide.motion = mountToRide; sMountRide.tag = "Mounted";
            var sRideMount = sm.AddState("RideToMounted"); sRideMount.motion = rideToMount; sRideMount.tag = "Mounted";
            var sBrake     = sm.AddState("Brake");         sBrake.motion     = brake;
            var sWheelieS  = sm.AddState("WheelieStart");  sWheelieS.motion  = wheelieStart;
            var sWheelieL  = sm.AddState("WheelieLoop");   sWheelieL.motion  = wheelieLoop;
            var sWheelieE  = sm.AddState("WheelieEnd");    sWheelieE.motion  = wheelieEnd;
            var sAirLoop   = sm.AddState("AirLoop");       sAirLoop.motion   = airLoop;
            var sAirEnd    = sm.AddState("AirEnd");        sAirEnd.motion    = airEnd;
            var sReverse   = sm.AddState("Reverse");       sReverse.motion   = reverseLoop;
            sm.defaultState = sMounted;
            var rideTree = new BlendTree
            {
                name = "Ride (Speed)",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(rideTree, ctrl);
            rideTree.AddChild(SteerTree(ctrl, "Steer (Cruise)", idleRiding, turnLeft, turnRight, turnLeftHard, turnRightHard), 0f);
            rideTree.AddChild(SteerTree(ctrl, "Steer (Flat out)", drive,    turnLeft, turnRight, turnLeftHard, turnRightHard), 1f);
            var sRide = sm.AddState("Ride");
            sRide.motion = rideTree;

            if (turnLeftHard == null || turnRightHard == null)
                Debug.LogWarning("[RiderAnimator] AS_Turn_V2_*_Loop not found, the steer blend uses the V1 " +
                                 "pair across the full range, so hard cornering leans no deeper than half lock. " +
                                 "Reimport the Riding folder and re-run to get the progressive lean.");

            var t1 = OnCondition(sMounted, sMountRide, MountBlend);
            t1.AddCondition(AnimatorConditionMode.If, 0f, "Riding");
            t1.AddCondition(AnimatorConditionMode.Greater, MountSpeed, "Speed");
            t1.AddCondition(AnimatorConditionMode.IfNot, 0f, "Reverse");
            OnClipEnd(sMountRide, sRide, MountExitTime, MountBlend);

            var t5 = OnCondition(sRide, sRideMount, MountBlend);
            t5.AddCondition(AnimatorConditionMode.Less, DismountSpeed, "Speed");
            t5.AddCondition(AnimatorConditionMode.IfNot, 0f, "Reverse");
            OnClipEnd(sRideMount, sMounted, MountExitTime, MountBlend);

            // Brake
            var tB1 = OnCondition(sRide, sBrake);
            tB1.AddCondition(AnimatorConditionMode.If, 0f, "Braking");
            var tB2 = OnCondition(sBrake, sRide, 0.25f);
            tB2.AddCondition(AnimatorConditionMode.IfNot, 0f, "Braking");
            var tB3 = OnCondition(sBrake, sRideMount, 0.25f);
            tB3.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");
            tB3.AddCondition(AnimatorConditionMode.IfNot, 0f, "Reverse");

            // Wheelie (player only, the AI never raises the flag)
            var tW1 = OnCondition(sRide, sWheelieS);
            tW1.AddCondition(AnimatorConditionMode.If, 0f, "Wheelie");
            OnClipEnd(sWheelieS, sWheelieL);
            var tW2 = OnCondition(sWheelieL, sWheelieE);
            tW2.AddCondition(AnimatorConditionMode.IfNot, 0f, "Wheelie");
            OnClipEnd(sWheelieE, sRide);

            // Airborne
            var tA1 = OnCondition(sRide, sAirLoop);
            tA1.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            var tA2 = OnCondition(sWheelieL, sAirLoop); 
            tA2.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            var tA3 = OnCondition(sAirLoop, sAirEnd);
            tA3.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            OnClipEnd(sAirEnd, sRide);

            // Reverse (look back loop, player only, the AI never reverses)
            var tR1 = OnCondition(sRide, sReverse, 0.25f);
            tR1.AddCondition(AnimatorConditionMode.If, 0f, "Reverse");
            var tR2 = OnCondition(sMounted, sReverse, 0.25f);
            tR2.AddCondition(AnimatorConditionMode.If, 0f, "Riding");
            tR2.AddCondition(AnimatorConditionMode.If, 0f, "Reverse");
            var tR3 = OnCondition(sReverse, sRide, 0.25f);
            tR3.AddCondition(AnimatorConditionMode.IfNot, 0f, "Reverse");

            // Additive upper body lean layer (optional, see fields above)
            var leanPath  = $"{_packRoot}/{LeanClipName}.anim";
            var leanClip  = AssetDatabase.LoadAssetAtPath<AnimationClip>(leanPath);
            var upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(CombatUpperMaskPath);
            if (leanClip == null)
                Debug.LogWarning($"[RiderAnimator] No lean clip at {leanPath}, additive lean layer skipped. " +
                                 "Run MotoSquid/Animation/Generate Lean Clip first to create it, then regenerate.");
            else if (upperMask == null)
                Debug.LogWarning($"[RiderAnimator] CombatUpperMask not found at {CombatUpperMaskPath}, additive " +
                                 "lean layer skipped.");
            else
                AddLeanLayer(ctrl, leanClip, upperMask, LeanLayerWeight);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RiderAnimator] Generated {outputPath} from the {style} clip set " +
                      "(Mounted / Ride[Speed x Steer nested blend tree] / Brake / Wheelie / Air / Reverse). " +
                      "LOOP TIME ON: AS_Idle_Mounted, AS_Idle_Riding, AS_Drive_Fast_Loop, " +
                      "AS_Turn_V1_Left_Loop, AS_Turn_V1_Right_Loop, AS_Turn_V2_Left_Loop, " +
                      "AS_Turn_V2_Right_Loop, AS_Brake, AS_Wheelie_Loop, " +
                      "AS_In_Air_Jump_Loop, AS_Drive_Reverse_Right_Loop. " +
                      "LOOP TIME OFF: AS_Mounted_to_Ride, AS_Ride_to_Mounted, AS_Wheelie_Start, " +
                      "AS_Wheelie_End, AS_In_Air_Jump_End. " +
                      "Every clip in the Ride tree must be the SAME LENGTH normalised loop or the " +
                      "blend will foot skate: check them together in the Animation window.");
            Selection.activeObject = ctrl;
        }

        static BlendTree SteerTree(AnimatorController ctrl, string name, AnimationClip straight,
            AnimationClip turnLeft, AnimationClip turnRight,
            AnimationClip turnLeftHard, AnimationClip turnRightHard)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Steer",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);

            bool hard = turnLeftHard != null && turnRightHard != null;
            if (hard) tree.AddChild(turnLeftHard, -1f);
            tree.AddChild(turnLeft, hard ? -0.5f : -1f);
            tree.AddChild(straight, 0f);
            tree.AddChild(turnRight, hard ? 0.5f : 1f);
            if (hard) tree.AddChild(turnRightHard, 1f);
            return tree;
        }

        const float MountSpeed    = 0.02f; 
        const float DismountSpeed = 0.01f;
        const float MountBlend    = 0.30f;
        const float MountExitTime = 0.65f;  

        static AnimatorStateTransition OnCondition(AnimatorState from, AnimatorState to, float duration = 0.15f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            return t;
        }

        static void OnClipEnd(AnimatorState from, AnimatorState to, float exitTime = 0.9f, float duration = 0.15f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
        }

        static void AddLeanLayer(AnimatorController ctrl, AnimationClip leanClip, AvatarMask mask, float weight)
        {
            var sm = new AnimatorStateMachine
            {
                name = "Upper Lean (Additive)",
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(sm, ctrl);
            sm.AddState("Lean").motion = leanClip;

            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Upper Lean (Additive)",
                defaultWeight = weight,
                blendingMode = AnimatorLayerBlendingMode.Additive,
                avatarMask = mask,
                stateMachine = sm,
                iKPass = false,
            });
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
