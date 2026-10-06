using System;
using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEditor;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Editor {

	public class BrokenCollectionWindow : EditorWindow {

		private string _originalName;
		private readonly BrokenCollectionWindowParams _windowParams = new BrokenCollectionWindowParams();
		private SerializedObject _windowSerialized;
		[SerializeField]
		private BreakableObjectParams _breakableObjectParams;


		[MenuItem("Window/Create broken objects")]
		public static void ShowWindow() {
			BrokenCollectionWindow window = (BrokenCollectionWindow)GetWindow(typeof(BrokenCollectionWindow));
			window.minSize = new Vector2(450, 250);
			window.titleContent = new GUIContent("Create broken objects");
			window._windowSerialized = new SerializedObject(window);
			window.Show();
		}

		private void OnSelectionChange() {
			if (Selection.gameObjects.Length == 1) {
				_originalName = Selection.gameObjects[0].name;
				_windowParams.Path = $"Assets/{_originalName}";
			}
			Repaint();
		}

		private void OnValidate() {
			_windowSerialized = new SerializedObject(this);
		}

		private void OnGUI() {
			float showParams = Selection.gameObjects.Length == 1
			                   && Selection.gameObjects[0].GetComponent<MeshFilter>() != null
				? 1f
				: 0f;

			if (EditorGUILayout.BeginFadeGroup(showParams)) {
				GameObject gameObject = Selection.gameObjects[0];
				_originalName = gameObject.name;

				GUILayout.Label("Broken Object Settings", EditorStyles.boldLabel);
				EditorGUILayout.BeginHorizontal();
				{
					GUILayout.Label("Path to save:");
					_windowParams.Path = EditorGUILayout.TextField(_windowParams.Path);
					if (GUILayout.Button("Browse")) {
						_windowParams.Path = EditorUtility
							.SaveFolderPanel("Save broken object collection", "Assets/", _originalName)
							.Replace('\\', '/');
					}
				}
				EditorGUILayout.EndHorizontal();

				_windowParams.IterationsCount = EditorGUILayout.IntSlider("Iterations count",
					Mathf.Clamp(_windowParams.IterationsCount, 1, 10), 1, 10);
				_windowParams.BrokenObjectVariationsCount = EditorGUILayout.IntField("Variants count", _windowParams.BrokenObjectVariationsCount);
				_windowParams.ShardCollidersEnabled = EditorGUILayout.Toggle("Add colliders to shards", _windowParams.ShardCollidersEnabled);
				if (_windowParams.ShardCollidersEnabled) {
					EditorGUI.indentLevel = 1;
					_windowParams.ShardColliderType = (ColliderType)EditorGUILayout.EnumPopup("Shard collider type", _windowParams.ShardColliderType);
					EditorGUI.indentLevel = 0;
				}
				_windowParams.AddBrokenObjectToScene = EditorGUILayout.Toggle("Add broken object to scene", _windowParams.AddBrokenObjectToScene);
				_windowParams.AddNewGeometry = EditorGUILayout.Toggle("Add new geometry (Works only with convex meshes!)", _windowParams.AddNewGeometry);
				_windowParams.DestroyByClick = EditorGUILayout.Toggle("Destroy object by click", _windowParams.DestroyByClick);
				_windowParams.DestroyByCollision = EditorGUILayout.Toggle("Destroy object by collision", _windowParams.DestroyByCollision);
				if (_windowParams.DestroyByCollision) {
					EditorGUI.indentLevel = 1;
					_windowParams.IsKinematic = EditorGUILayout.Toggle("Is kinematic", _windowParams.IsKinematic);
					EditorGUI.indentLevel = 0;
				}
				if (_windowParams.DestroyByCollision || _windowParams.DestroyByClick) {
					EditorGUI.indentLevel = 1;
					_windowParams.ObjectColliderType = (ColliderType)EditorGUILayout.EnumPopup("Object collider type", _windowParams.ObjectColliderType);
					EditorGUI.indentLevel = 0;
				}

				GUILayout.Space(10);
				GUILayout.Label("Advanced", EditorStyles.boldLabel);
				_windowParams.ShowAdvancedSettings = EditorGUILayout.BeginToggleGroup("Show advanced settings", _windowParams.ShowAdvancedSettings);
				if (_windowParams.ShowAdvancedSettings) {
					EditorGUI.indentLevel = 1;
					EditorGUILayout.PropertyField(_windowSerialized.FindProperty("_breakableObjectParams"), true);
					_windowParams.AddPoolsToScene = EditorGUILayout.Toggle("Add pools to scene", _windowParams.AddPoolsToScene);
					EditorGUI.indentLevel = 0;
					GUILayout.Space(10);
				}
				EditorGUILayout.EndToggleGroup();

				if (GUILayout.Button("Create")) {
					try {
						BrokenCollectionCreator.CreateBrokenObject(gameObject, _windowParams, _breakableObjectParams);
					} catch (BrokenCollectionCreatorException e) {
						EditorUtility.DisplayDialog(e.ErrorTitle, e.Error, "Ok");
					} catch (Exception e) {
						EditorUtility.DisplayDialog("Unknown error", "Unknown error. Please copy it from the Console window and contact asset creator to fix it", "Ok");
						Debug.LogException(e);
					}
				}
			} else {
				GUILayout.Label("Please select game object which has MeshFilter component", EditorStyles.boldLabel);
			}
			EditorGUILayout.EndFadeGroup();
		}

	}

}