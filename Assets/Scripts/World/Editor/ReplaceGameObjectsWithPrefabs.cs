using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace MotoSquid.World
{
    public class ReplaceGameObjectsWithPrefabs : EditorWindow
    {
        private GameObject[] selectedObjects;
        private DefaultAsset prefabFolder;
        private bool keepWindowOpen = true;
        private Vector2 scrollPosition;
        private string statusMessage = "";
        private bool showSuccessObjects = false;
        private bool showFailedObjects = false;
        private List<string> successObjects = new List<string>();
        private List<string> failedObjects = new List<string>();

        [MenuItem("Custom/Replace GameObjects With Prefabs")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(ReplaceGameObjectsWithPrefabs), false, "Replace GameObjects");
        }

        void OnGUI()
        {
            GUILayout.Label("Replace Selected GameObjects With Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("This tool will replace selected objects in the scene with prefabs of the same name from the selected folder.", MessageType.Info);
            EditorGUILayout.Space();

            // Get selected objects
            EditorGUILayout.LabelField("Step 1: Select objects in your scene hierarchy");
            if (GUILayout.Button("Get Selected Objects"))
            {
                selectedObjects = Selection.gameObjects;
                statusMessage = "";
                successObjects.Clear();
                failedObjects.Clear();
            }

            // Display selected objects
            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                EditorGUILayout.LabelField($"Selected Objects: {selectedObjects.Length}");
                EditorGUI.indentLevel++;
            
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
                foreach (GameObject obj in selectedObjects)
                {
                    if (obj != null)
                    {
                        EditorGUILayout.LabelField(obj.name);
                    }
                }
                EditorGUILayout.EndScrollView();
            
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("No objects selected");
            }

            EditorGUILayout.Space();

            // Select prefab folder
            EditorGUILayout.LabelField("Step 2: Select prefab folder");
            prefabFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefab Folder", prefabFolder, typeof(DefaultAsset), false);

            if (prefabFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(prefabFolder);
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    EditorGUILayout.HelpBox("Selected asset is not a folder!", MessageType.Error);
                    prefabFolder = null;
                }
            }

            EditorGUILayout.Space();

            // Replace button
            GUI.enabled = selectedObjects != null && selectedObjects.Length > 0 && prefabFolder != null;
            if (GUILayout.Button("Replace Objects"))
            {
                ReplaceSelectedObjects();
            }
            GUI.enabled = true;

            EditorGUILayout.Space();

            // Status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            
                // Show success objects
                if (successObjects.Count > 0)
                {
                    showSuccessObjects = EditorGUILayout.Foldout(showSuccessObjects, $"Successfully Replaced Objects ({successObjects.Count})");
                    if (showSuccessObjects)
                    {
                        EditorGUI.indentLevel++;
                        foreach (string objName in successObjects)
                        {
                            EditorGUILayout.LabelField(objName);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            
                // Show failed objects
                if (failedObjects.Count > 0)
                {
                    showFailedObjects = EditorGUILayout.Foldout(showFailedObjects, $"Failed to Replace Objects ({failedObjects.Count})");
                    if (showFailedObjects)
                    {
                        EditorGUI.indentLevel++;
                        foreach (string objName in failedObjects)
                        {
                            EditorGUILayout.LabelField(objName);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.Space();
            keepWindowOpen = EditorGUILayout.Toggle("Keep Window Open After Replace", keepWindowOpen);
        }

        void ReplaceSelectedObjects()
        {
            if (selectedObjects == null || selectedObjects.Length == 0 || prefabFolder == null)
                return;

            string folderPath = AssetDatabase.GetAssetPath(prefabFolder);
            successObjects.Clear();
            failedObjects.Clear();
        
            Undo.RecordObjects(selectedObjects, "Replace GameObjects With Prefabs");

            foreach (GameObject oldObject in selectedObjects)
            {
                if (oldObject == null)
                    continue;

                string objectName = oldObject.name;
                string prefabPath = Path.Combine(folderPath, objectName + ".prefab");
            
                // Normalize path separators for Unity
                prefabPath = prefabPath.Replace('\\', '/');
            
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab != null)
                {
                    // Store transform info
                    Vector3 position = oldObject.transform.position;
                    Quaternion rotation = oldObject.transform.rotation;
                    Vector3 scale = oldObject.transform.localScale;
                    Transform parent = oldObject.transform.parent;
                    int siblingIndex = oldObject.transform.GetSiblingIndex();

                    // Instantiate the prefab
                    GameObject newObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                
                    // Apply the stored transform values
                    newObject.transform.position = position;
                    newObject.transform.rotation = rotation;
                    newObject.transform.localScale = scale;
                    newObject.transform.parent = parent;
                    newObject.transform.SetSiblingIndex(siblingIndex);
                
                    // Destroy the old object
                    DestroyImmediate(oldObject);
                
                    successObjects.Add(objectName);
                }
                else
                {
                    failedObjects.Add(objectName);
                }
            }

            if (successObjects.Count > 0 || failedObjects.Count > 0)
            {
                statusMessage = $"Replaced {successObjects.Count} objects. Failed to replace {failedObjects.Count} objects.";
            }
            else
            {
                statusMessage = "No objects were replaced.";
            }

            // Clear selection if window should close
            if (!keepWindowOpen)
            {
                selectedObjects = null;
                prefabFolder = null;
                this.Close();
            }
        }
    }
}