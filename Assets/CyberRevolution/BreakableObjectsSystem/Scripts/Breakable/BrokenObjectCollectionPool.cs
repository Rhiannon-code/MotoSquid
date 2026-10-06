using System.Collections.Generic;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	public class BrokenObjectCollectionPool {

		private readonly BrokenObjectCollection _collection;
		private readonly Transform _poolTransform;

		private Dictionary<int, BrokenPrefabReinitializeData> _prefabReinitializeData;
		private Stack<BrokenObject>[] _objectPools;
		private int _prefabsCount;

		public BrokenObjectCollectionPool(BrokenObjectCollection collection, Transform poolTransform) {
			_poolTransform = poolTransform;
			_collection = collection;
			InitializePool();
		}

		private void InitializePool() {
			_prefabsCount = _collection.BreakableObjectPrefabs.Count;
			_objectPools = new Stack<BrokenObject>[_prefabsCount];
			for (int i = 0; i < _objectPools.Length; i++) {
				_objectPools[i] = new Stack<BrokenObject>(_collection.PoolCapacity);
			}
			_prefabReinitializeData = new Dictionary<int, BrokenPrefabReinitializeData>(_prefabsCount);
			for (int prefabIndex = 0; prefabIndex < _prefabsCount; prefabIndex++) {
				_prefabReinitializeData.Add(prefabIndex, GetReinitializeData(prefabIndex));
				for (int i = 0; i < _collection.PoolCapacity; i++) {
					BrokenObject brokenObject = Object.Instantiate(_collection.BreakableObjectPrefabs[prefabIndex], _poolTransform);
					brokenObject.SetActiveFast(false);
					_objectPools[prefabIndex].Push(brokenObject);
				}
			}
		}

		public BrokenObject GetRandomObject(Transform parent, Vector3 position, Vector3 localScale, Quaternion rotation, out int index) {
			index = Random.Range(0, _prefabsCount);
			Stack<BrokenObject> pool = _objectPools[index];
			if (pool.Count == 0) {
				BrokenObject brokenObject = Object.Instantiate(_collection.BreakableObjectPrefabs[index], position, rotation, parent);
				Transform t = brokenObject.transform;
				t.localScale = localScale;
				brokenObject.SetActiveFast(true);
				return brokenObject;
			}
			BrokenObject randomObject = pool.Pop();
			Transform tr = randomObject.transform;
			tr.parent = parent;
			tr.position = position;
			tr.rotation = rotation;
			tr.localScale = localScale;
			randomObject.SetActiveFast(true);
			return randomObject;
		}

		public void ReleaseObject(BrokenObject go, int index) {
			Stack<BrokenObject> pool = _objectPools[index];
			go.transform.parent = _poolTransform;
			RestoreChildPositions(go, index);
			pool.Push(go);
			go.SetActiveFast(false);
		}

		private void RestoreChildPositions(BrokenObject go, int prefabIndex) {
			Vector3[] oldPositions = _prefabReinitializeData[prefabIndex].Positions;
			Quaternion[] oldRotations = _prefabReinitializeData[prefabIndex].Rotations;
			int childIndex = 0;
			foreach (Transform t in go.transform) {
				t.localPosition = oldPositions[childIndex];
				t.localRotation = oldRotations[childIndex];
				childIndex++;
			}
		}

		private BrokenPrefabReinitializeData GetReinitializeData(int prefabIndex) {
			BrokenObject prefab = _collection.BreakableObjectPrefabs[prefabIndex];
			int childCount = prefab.transform.childCount;
			if (childCount == 0) {
				Debug.LogError($"{nameof(BrokenObjectCollectionPool)}: Child count of the broken root is null!");
			}
			int childIndex = 0;
			Vector3[] positions = new Vector3[childCount];
			Quaternion[] rotations = new Quaternion[childCount];
			foreach (Transform t in prefab.transform) {
				positions[childIndex] = t.localPosition;
				rotations[childIndex] = t.localRotation;
				childIndex++;
			}
			return new BrokenPrefabReinitializeData(positions, rotations);
		}

	}

}