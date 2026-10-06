using System.Collections.Generic;
using CyberRevolution.BreakableObjectsSystem.Scripts.Common;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	public class BrokenObjectPools : MonoBehaviourSceneSingleton<BrokenObjectPools> {

		private readonly Dictionary<BrokenObjectCollection, BrokenObjectCollectionPool> _pools
			= new Dictionary<BrokenObjectCollection, BrokenObjectCollectionPool>();

		public void AddCollectionToPool(BrokenObjectCollection brokenObjectCollection) {
			var newPool = new BrokenObjectCollectionPool(brokenObjectCollection, transform);
			_pools.Add(brokenObjectCollection, newPool);
		}

		public BrokenObject GetRandomObjectFromCollection(BrokenObjectCollection collection, Transform parent, Vector3 position, Quaternion rotation, Vector3 localScale, out int index) {
			if (_pools.TryGetValue(collection, out BrokenObjectCollectionPool pool)) {
				BrokenObject go1 = pool.GetRandomObject(parent, position, localScale, rotation, out int index1);
				index = index1;
				return go1;
			}
			var newPool = new BrokenObjectCollectionPool(collection, transform);
			_pools.Add(collection, newPool);
			BrokenObject go2 = newPool.GetRandomObject(parent, position, localScale, rotation, out int index2);
			index = index2;
			return go2;
		}

		public void ReleaseObjectFromCollection(BrokenObjectCollection collection, BrokenObject go, int index) {
			if (_pools.TryGetValue(collection, out BrokenObjectCollectionPool pool)) {
				pool.ReleaseObject(go, index);
				return;
			}
			Debug.LogError("Pool should exist when releasing object!");
		}

	}

}