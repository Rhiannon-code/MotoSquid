using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MotoSquid.Rider
{

    public static class BikeAnimatorGenerator
    {
        const string TestRoot = "Assets/Animations/Test";

        public static readonly string[] Styles = { "SuperSport", "Upright" };
        static string PackRootFor(string style)          => $"{TestRoot}/{style}";
        public static string OutputPathFor(string style) => $"{TestRoot}/BikeMaster_{style}.controller";
        public static string[] OutputPaths => Styles.Select(OutputPathFor).ToArray();

        static string _packRoot;

        [MenuItem("MotoSquid/Animation/Generate Bike Animator (SuperSport)")]
        public static void GenerateSuperSport() => Generate("SuperSport");

        [MenuItem("MotoSquid/Animation/Generate Bike Animator (Upright)")]
        public static void GenerateUpright() => Generate("Upright");

        public static void Generate(string style)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[BikeAnimator] Editor is compiling/importing. Wait for it to settle, then run again.");
                return;
            }

            _packRoot         = PackRootFor(style);
            string outputPath = OutputPathFor(style);

            var turnLeft     = FindClip("AS_Turn_V1_Left_Loop_Bike");
            var turnRight    = FindClip("AS_Turn_V1_Right_Loop_Bike");
            var turnLeftHard  = FindClip("AS_Turn_V2_Left_Loop_Bike");
            var turnRightHard = FindClip("AS_Turn_V2_Right_Loop_Bike");
            var wheelieStart = FindClip("AS_Wheelie_Start_Bike");
            var wheelieLoop  = FindClip("AS_Wheelie_Loop_Bike");
            var wheelieEnd   = FindClip("AS_Wheelie_End_Bike");
            var reverseLoop  = FindClip("AS_Drive_Reverse_Right_Loop_Bike");
            var mountToRide  = FindClip("AS_Mounted_to_Ride_Bike");
            var rideToMount  = FindClip("AS_Ride_to_Mounted_Bike");

            var missing = new[]
            {
                (turnLeft, "AS_Turn_V1_Left_Loop_Bike"), (turnRight, "AS_Turn_V1_Right_Loop_Bike"),
            }.Where(x => x.Item1 == null).Select(x => x.Item2).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"[BikeAnimator] Missing required Turn *_Bike clip(s): {string.Join(", ", missing)}. " +
                               $"Searched recursively under {_packRoot}. If the file is on disk, Unity's importers " +
                               "may still be running (the 'Shutdown worker was forced killed' errors) reimport the " +
                               "SuperSport folder, let it settle, then run again.");
                return;
            }
            bool hasWheelie = wheelieStart != null && wheelieLoop != null && wheelieEnd != null;
            bool hasReverse = reverseLoop != null;
            bool hasMount   = mountToRide != null && rideToMount != null;

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(outputPath);

            ctrl.AddParameter("Speed",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Riding",   AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Steer",    AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Wheelie",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Reverse",  AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var sDefault = sm.AddState("Default");
            AnimatorState sMounted = null, sMountRide = null, sRideMount = null;
            if (hasMount)
            {
                sMounted   = sm.AddState("Mounted");
                sMounted.motion = mountToRide;              
                sMounted.speed  = 0f;                    
                sMountRide = sm.AddState("MountedToRide"); sMountRide.motion = mountToRide;
                sRideMount = sm.AddState("RideToMounted"); sRideMount.motion = rideToMount;
                sm.defaultState = sMounted;
            }
            else sm.defaultState = sDefault;
            var rideTree = new BlendTree
            {
                name = "Ride (Steer)",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Steer",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(rideTree, ctrl);
            bool hardLean = turnLeftHard != null && turnRightHard != null;
            if (hardLean) rideTree.AddChild(turnLeftHard, -1f);
            rideTree.AddChild(turnLeft, hardLean ? -0.5f : -1f);
            rideTree.AddChild(null, 0f);                     
            rideTree.AddChild(turnRight, hardLean ? 0.5f : 1f);
            if (hardLean) rideTree.AddChild(turnRightHard, 1f);
            sDefault.motion = rideTree;
            sDefault.name   = "Ride";
            if (hasMount)
            {
                var tMr = OnCondition(sMounted, sMountRide);
                tMr.AddCondition(AnimatorConditionMode.If, 0f, "Riding");
                tMr.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
                OnClipEnd(sMountRide, sDefault);

                var tRm = OnCondition(sDefault, sRideMount, 0.25f);
                tRm.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");
                OnClipEnd(sRideMount, sMounted);
            }

            if (hasWheelie)
            {
                var sWheelieS = sm.AddState("WheelieStart"); sWheelieS.motion = wheelieStart;
                var sWheelieL = sm.AddState("WheelieLoop");  sWheelieL.motion = wheelieLoop;
                var sWheelieE = sm.AddState("WheelieEnd");   sWheelieE.motion = wheelieEnd;
                var tW = OnCondition(sDefault, sWheelieS);
                tW.AddCondition(AnimatorConditionMode.If, 0f, "Wheelie");
                OnClipEnd(sWheelieS, sWheelieL);
                var tWe = OnCondition(sWheelieL, sWheelieE);
                tWe.AddCondition(AnimatorConditionMode.IfNot, 0f, "Wheelie");
                OnClipEnd(sWheelieE, sDefault);
            }
            if (hasReverse)
            {
                var sReverse = sm.AddState("Reverse"); sReverse.motion = reverseLoop;
                var tR = sm.AddAnyStateTransition(sReverse);
                tR.hasExitTime = false; tR.duration = 0.2f; tR.canTransitionToSelf = false;
                tR.AddCondition(AnimatorConditionMode.If, 0f, "Reverse");
                var tRb = OnCondition(sReverse, sDefault, 0.2f);
                tRb.AddCondition(AnimatorConditionMode.IfNot, 0f, "Reverse");
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BikeAnimator] Generated {outputPath} from the {style} *_Bike clip set " +
                      $"({(hasMount ? "Mounted/MountedToRide/RideToMounted/" : "")}Ride[Steer blend tree]" +
                      $"{(hasWheelie ? "/Wheelie" : ", Wheelie SKIPPED, clip missing")}" +
                      $"{(hasReverse ? "/Reverse" : "/Reverse SKIPPED, clip missing")}). " +
                      "Put a GENERIC Animator (no avatar) with this " +
                      "controller + BikeRiderAnimator on the intact bike art. LOOP TIME ON: " +
                      "AS_Turn_V1_Left/Right_Loop_Bike, AS_Wheelie_Loop_Bike, AS_Drive_Reverse_Right_Loop_Bike; " +
                      "OFF: AS_Wheelie_Start_Bike, AS_Wheelie_End_Bike.");
            Selection.activeObject = ctrl;
        }

        static AnimatorStateTransition OnCondition(AnimatorState from, AnimatorState to, float duration = 0.15f)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            return t;
        }

        static void OnClipEnd(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.15f;
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
