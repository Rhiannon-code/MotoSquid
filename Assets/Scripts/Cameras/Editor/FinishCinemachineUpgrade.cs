using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Cameras
{
    public static class FinishCinemachineUpgrade
    {
        [MenuItem("MotoSquid/Fix/Finish Cinemachine Upgrade (Selected Prefabs)")]
        static void Run()
        {
            var prefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
            if (prefabs == null || prefabs.Length == 0)
            {
                EditorUtility.DisplayDialog("Finish Cinemachine Upgrade",
                    "Select one or more prefabs in the Project window first.", "OK");
                return;
            }

            int cameras = 0, touched = 0;
            var log = new StringBuilder();

            foreach (var asset in prefabs)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { log.AppendLine($"x {asset.name}: could not open"); continue; }

                try
                {
                    int n = 0;
                    foreach (var cam in root.GetComponentsInChildren<CinemachineCamera>(true))
                        if (Convert(cam, log)) n++;

                    if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); touched++; cameras += n; }
                    else log.AppendLine($", {asset.name}: nothing to convert");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Finish Cinemachine Upgrade",
                $"Converted {cameras} camera(s) across {touched} prefab(s).\n\n{log}", "OK");
        }

#pragma warning disable 618 // Deprecated types are the whole point here
        static bool Convert(CinemachineCamera cam, StringBuilder log)
        {
            var go       = cam.gameObject;
            var oldVcam  = go.GetComponent<CinemachineVirtualCamera>();
            var oldBody  = go.GetComponent<CinemachineTransposer>();
            var oldAim   = go.GetComponent<CinemachineComposer>();
            if (oldVcam == null && oldBody == null && oldAim == null) return false;

            if (oldBody != null)
            {
                var follow = go.GetComponent<CinemachineFollow>() ?? go.AddComponent<CinemachineFollow>();
                follow.FollowOffset = oldBody.m_FollowOffset;

                var t = follow.TrackerSettings;
                t.BindingMode        = oldBody.m_BindingMode;
                t.PositionDamping    = new Vector3(oldBody.m_XDamping, oldBody.m_YDamping, oldBody.m_ZDamping);
                t.AngularDampingMode = oldBody.m_AngularDampingMode;
                t.RotationDamping    = new Vector3(oldBody.m_PitchDamping, oldBody.m_YawDamping, oldBody.m_RollDamping);
                t.QuaternionDamping  = oldBody.m_AngularDamping;
                follow.TrackerSettings = t;
            }

            if (oldAim != null)
            {
                var comp = go.GetComponent<CinemachineRotationComposer>() ?? go.AddComponent<CinemachineRotationComposer>();

                var c = comp.Composition;
                c.ScreenPosition = new Vector2(oldAim.m_ScreenX - 0.5f, oldAim.m_ScreenY - 0.5f);
                comp.Composition = c;

                comp.Damping = new Vector2(oldAim.m_HorizontalDamping, oldAim.m_VerticalDamping);
            }

            log.AppendLine($"+ {go.name}: " +
                           (oldBody != null ? "Transposer->Follow " : "") +
                           (oldAim  != null ? "Composer->RotationComposer " : "") +
                           "and stripped the deprecated components");

            if (oldAim  != null) Object.DestroyImmediate(oldAim,  true);
            if (oldBody != null) Object.DestroyImmediate(oldBody, true);
            if (oldVcam != null) Object.DestroyImmediate(oldVcam, true);
            return true;
        }
#pragma warning restore 618
    }
}
