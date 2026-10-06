#if UNITY_EDITOR

using NGS.MagicLOD.Editors.Components;
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NGS.MagicLOD.Editors
{
    public class MagicLODBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder
        {
            get
            {
                return 0;
            }
        }

        private Type[] _excludeTypes = new Type[]
        {
            typeof(MagicLODEditorComponent),
            typeof(MagicLODOverlay),
        };


        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!BuildPipeline.isBuildingPlayer)
                return;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var excludeType in _excludeTypes)
                {
                    foreach (var component in root.GetComponentsInChildren(excludeType, true))
                    {
                        component.hideFlags |= HideFlags.DontSaveInBuild;
                    }
                }
            }
        }
    }
}

#endif