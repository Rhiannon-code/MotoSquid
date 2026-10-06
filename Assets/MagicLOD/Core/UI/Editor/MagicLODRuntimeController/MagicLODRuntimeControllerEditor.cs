using UnityEditor;
using UnityEngine;
using NGS.MagicLOD.Runtime;
using NGS.MagicLOD.Runtime.Components;

namespace NGS.MagicLOD.Editors.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MagicLODRuntimeController))]
    public sealed class MagicLODRuntimeControllerEditor : Editor
    {
        private static readonly GUIContent ThreadsLabel = new GUIContent("Threads");
        private static readonly GUIContent ManualLabel = new GUIContent("Manual");
        private static readonly GUIContent LODSettingsLabel = new GUIContent("LOD Settings");

        private SerializedProperty _script;
        private SerializedProperty _controllerID;
        private SerializedProperty _maxActiveRequests;
        private SerializedProperty _maxCacheSize;
        private SerializedProperty _decimationSettings;
        private SerializedProperty _lodGroupConfigurator;
        private SerializedProperty _maxSimplificationError;
        private SerializedProperty _operationMode;
        private SerializedProperty _orderingMode;
        private SerializedProperty _orderingTarget;
        private SerializedProperty _dontDestroyOnLoad;


        private void OnEnable()
        {
            _script = serializedObject.FindProperty("m_Script");
            _controllerID = serializedObject.FindProperty("_controllerID");
            _maxActiveRequests = serializedObject.FindProperty("_maxActiveRequests");
            _maxCacheSize = serializedObject.FindProperty("_maxCacheSize");
            _decimationSettings = serializedObject.FindProperty("_decimationSettings");
            _lodGroupConfigurator = serializedObject.FindProperty("_lodGroupConfigurator");
            _maxSimplificationError = serializedObject.FindProperty("_maxSimplificationError");
            _operationMode = serializedObject.FindProperty("_operationMode");
            _orderingMode = serializedObject.FindProperty("_orderingMode");
            _orderingTarget = serializedObject.FindProperty("_orderingTarget");
            _dontDestroyOnLoad = serializedObject.FindProperty("_dontDestroyOnLoad");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_script);
            }

            EditorGUILayout.PropertyField(_controllerID);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_maxActiveRequests, ThreadsLabel);
            EditorGUILayout.PropertyField(_maxCacheSize);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_operationMode);

            if (!_operationMode.hasMultipleDifferentValues)
            {
                OperationMode operationMode = (OperationMode)_operationMode.intValue;

                if (operationMode == OperationMode.GenerateLODs)
                {
                    DrawLODConfigurator();
                }
                else if (operationMode == OperationMode.Simplify)
                {
                    EditorGUILayout.PropertyField(_maxSimplificationError);
                }
            }

            EditorGUILayout.PropertyField(_decimationSettings, true);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_orderingMode);

            if (!_orderingMode.hasMultipleDifferentValues &&
                (OrderingMode)_orderingMode.intValue != OrderingMode.None)
            {
                EditorGUILayout.PropertyField(_orderingTarget);
            }

            EditorGUILayout.PropertyField(_dontDestroyOnLoad);

            serializedObject.ApplyModifiedProperties();
        }


        private void DrawLODConfigurator()
        {
            MagicLODRuntimeController controller = (MagicLODRuntimeController)target;
            bool isManual = controller.LODGroupConfigurator is ManualLODConfigurator;
            bool hasMixedConfigurators = false;

            for (int i = 1; i < targets.Length; i++)
            {
                MagicLODRuntimeController otherController = (MagicLODRuntimeController)targets[i];

                if ((otherController.LODGroupConfigurator is ManualLODConfigurator) != isManual)
                {
                    hasMixedConfigurators = true;
                    break;
                }
            }

            EditorGUI.showMixedValue = hasMixedConfigurators;
            EditorGUI.BeginChangeCheck();

            bool manual = EditorGUILayout.Toggle(ManualLabel, isManual);
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUI.showMixedValue = false;

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();

                Undo.RecordObjects(targets, "Change LOD Configurator");

                foreach (Object selectedTarget in targets)
                {
                    MagicLODRuntimeController selectedController = (MagicLODRuntimeController)selectedTarget;

                    if (manual)
                        selectedController.SetManualLODConfigurator();
                    else
                        selectedController.SetScreenSpaceLODConfigurator();

                    EditorUtility.SetDirty(selectedController);
                }

                serializedObject.Update();
                hasMixedConfigurators = false;
            }

            if (!hasMixedConfigurators)
                EditorGUILayout.PropertyField(_lodGroupConfigurator, LODSettingsLabel, true);
        }
    }
}
