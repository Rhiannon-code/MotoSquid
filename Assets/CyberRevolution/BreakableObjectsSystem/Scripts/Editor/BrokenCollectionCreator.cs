using System.Collections.Generic;
using System.IO;
using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using CyberRevolution.BreakableObjectsSystem.Scripts.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Editor {

	public static class BrokenCollectionCreator {

		public static void CreateBrokenObject(GameObject originalGameObject,
			BrokenCollectionWindowParams parameters, BreakableObjectParams breakableObjectParams) {
			// Define a path where to create broken object
			string directoryName = parameters.Path;
			if (directoryName.StartsWith(Application.dataPath)) {
				directoryName = FileUtil.GetProjectRelativePath(directoryName);
			}

			if (string.IsNullOrEmpty(directoryName)) {
				throw new BrokenCollectionCreatorException("Can not create Broken Objects!", $"Invalid path: {parameters.Path}");
			}

			if (!SceneManager.GetActiveScene().isLoaded) {
				throw new BrokenCollectionCreatorException("Can not create Broken Objects!", "Please open any scene to unable create game objects");
			}

			// Create temporary instance of original game object
			string originalParentName = $"Breakable{originalGameObject.name}";
			GameObject originalInstanceParent = new GameObject(originalParentName);
			originalInstanceParent.transform.SetPositionAndRotation(originalGameObject.transform.position, originalGameObject.transform.rotation);
			GameObject originalInstance = Object.Instantiate(originalGameObject, originalInstanceParent.transform);
			originalInstance.name = originalGameObject.name;
			originalInstance.transform.localScale = Vector3.one;
			originalInstance.transform.localPosition = Vector3.zero;
			originalInstance.transform.localRotation = Quaternion.identity;

			// Create several variations of broken object
			string directoryPathForAllBrokenParts = Path.Combine(directoryName, $"Broken{originalGameObject.name}");
			var brokenObjects = new List<BrokenObject>();
			for (int i = 0; i < parameters.BrokenObjectVariationsCount; i++) {
				brokenObjects.Add(CreateBrokenObjectFromMeshFilter(originalInstance,
					directoryPathForAllBrokenParts, parameters, i));
			}

			// Create broken object collection
			var brokenObjectCollection = ScriptableObject.CreateInstance<BrokenObjectCollection>();
			AssetDatabase.CreateAsset(brokenObjectCollection, Path.Combine(directoryPathForAllBrokenParts, $"Broken{originalGameObject.name}Collection.asset"));
			brokenObjectCollection.BreakableObjectPrefabs = brokenObjects;
			EditorUtility.SetDirty(brokenObjectCollection);

			// Configure original game object to be breakable
			var breakableObject = originalInstanceParent.AddComponent<BreakableObject>();
			breakableObject.OriginalObject = originalInstance;
			breakableObject.BrokenObjectsCollection = brokenObjectCollection;
			breakableObject.Params = breakableObjectParams;
			if (parameters.DestroyByClick) {
				AddBreakTrigger<BreakByClick>(parameters.ObjectColliderType, breakableObject, true);
			}
			if (parameters.DestroyByCollision) {
				AddBreakTrigger<BreakByCollision>(parameters.ObjectColliderType, breakableObject, parameters.IsKinematic);
			}

			// Save breakable (original) object and destroy temporary instance
			originalInstanceParent.transform.localScale = originalGameObject.transform.localScale;
			string breakableSavePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directoryPathForAllBrokenParts, $"{originalParentName}.prefab"));
			GameObject savedObject = PrefabUtility.SaveAsPrefabAssetAndConnect(originalInstanceParent, breakableSavePath, InteractionMode.UserAction);
			EditorUtility.SetDirty(savedObject);

			if (!parameters.AddBrokenObjectToScene) {
				Object.DestroyImmediate(originalInstanceParent);
			} else if (!PrefabUtility.IsPartOfAnyPrefab(originalGameObject)) {
				// If we decided to add new breakable object to scene and we have created it from another object on scene we should slightly move it
				var meshFilter = originalGameObject.GetComponent<Renderer>();
				Vector3 size = meshFilter.bounds.size;
				originalInstanceParent.transform.position += new Vector3(0, size.y * 0.5f, size.z * 0.5f);
				EditorUtility.SetDirty(originalInstanceParent);
			}

			if (parameters.AddPoolsToScene) {
				AddPoolsToScene();
			}
		}

		private static BrokenObject CreateBrokenObjectFromMeshFilter(GameObject gameObject,
			string directoryName, BrokenCollectionWindowParams parameters, int variationIndex) {

			string originalName = gameObject.name;
			List<GameObject> parts = new List<GameObject> { gameObject };

			// Destroy mesh and create game object for each part of it.
			for (int i = 0; i < parameters.IterationsCount; i++) {
				if (parts.Count > 0) {
					List<GameObject> partsToDestroy = new List<GameObject>(parts);
					parts.Clear();
					foreach (GameObject go in partsToDestroy) {
						var meshFilter = go.GetComponent<MeshFilter>();
						var meshDestroyer = new MeshDestroyer(meshFilter, parameters.CutCascadesCount, parameters.AddNewGeometry);
						meshDestroyer.DestroyMesh();
						parts.AddRange(meshDestroyer.Parts);
						if (i > 0) {
							Object.DestroyImmediate(go);
						}
					}
				}
			}

			// Create parent game object
			var brokenParentGoName = $"Broken{originalName}_{variationIndex}";
			var brokenObject = new GameObject(brokenParentGoName);
			if (parts.Count > 0) {
				brokenObject.transform.parent = parts[0].transform.parent;
			}
			brokenObject.transform.localPosition = Vector3.zero;
			brokenObject.transform.localRotation = Quaternion.identity;
			brokenObject.transform.localScale = Vector3.one;

			// Create directory for broken part
			string brokenDirectoryPath = Path.Combine(directoryName, brokenParentGoName);
			Directory.CreateDirectory(brokenDirectoryPath);

			// Save meshes of broken parts to assets
			for (var index = 0; index < parts.Count; index++) {
				GameObject part = parts[index];
				var partMeshFilter = part.GetComponent<MeshFilter>();
				var partGoName = $"Broken_{originalName}_{variationIndex}_part_{index}";

				SaveMesh(partMeshFilter, Path.Combine(brokenDirectoryPath, partGoName));
				part.name = partGoName;
				part.transform.parent = brokenObject.transform;

				part.AddComponent<Rigidbody>();
				if (parameters.ShardCollidersEnabled) {
					part.GetOrAddCollider(parameters.ShardColliderType == ColliderType.Default ? ColliderType.Mesh : parameters.ShardColliderType);
				} else {
					if (part.TryGetComponent(out Collider collider)) {
						Object.DestroyImmediate(collider);
					}
				}
			}

			// Save broken game object to assets and destroy it on the scene
			brokenObject.AddComponent<BrokenObject>();
			string brokenSavePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(brokenDirectoryPath, $"{brokenParentGoName}.prefab"));
			GameObject savedObject = PrefabUtility.SaveAsPrefabAssetAndConnect(brokenObject, brokenSavePath, InteractionMode.UserAction);
			Object.DestroyImmediate(brokenObject);
			return savedObject.GetComponent<BrokenObject>();
		}

		public static void SaveMesh(MeshFilter meshFilter, string path) {
			MeshUtility.Optimize(meshFilter.sharedMesh);
			AssetDatabase.CreateAsset(meshFilter.sharedMesh, path + ".asset");
			AssetDatabase.SaveAssets();
		}

		private static void AddPoolsToScene() {
			var pools = Object.FindObjectOfType<BrokenObjectPools>(true);
			if (pools != null) {
				if (!pools.gameObject.activeSelf) {
					pools.gameObject.SetActive(true);
				}
				if (!pools.gameObject.activeInHierarchy) {
					pools.transform.parent = null;
				}
				if (!pools.enabled) {
					pools.enabled = true;
				}
			} else {
				var newPools = new GameObject("BrokenObjectPools");
				newPools.AddComponent<BrokenObjectPools>();
				EditorUtility.SetDirty(newPools);
			}
		}

		private static void AddBreakTrigger<T>(ColliderType colliderType, BreakableObject breakableObject, bool isKinematic) where T : BreakAbstractTrigger {
			if (colliderType == ColliderType.Default) {
				colliderType = ColliderType.Mesh;
			}
			var collider = breakableObject.OriginalObject.GetOrAddCollider(colliderType);
			var trigger = breakableObject.gameObject.AddComponent<T>();
			trigger.BreakableObject = breakableObject;
			trigger.Collider = collider;
			trigger.Collider.isTrigger = false;

			var rigidbody = breakableObject.GetComponent<Rigidbody>();
			if (rigidbody == null) {
				breakableObject.gameObject.AddComponent<Rigidbody>();
			}
			rigidbody = breakableObject.GetComponent<Rigidbody>();
			rigidbody.isKinematic = isKinematic;
			breakableObject.Rigidbody = rigidbody;
		}

	}

}