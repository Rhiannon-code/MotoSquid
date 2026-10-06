using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Examples.Example1 {

	public class Example1 : MonoBehaviour {

		[SerializeField]
		private BrokenObjectCollection _brokenObjectCollection;

		private void Start() {
			BrokenObjectPools.Instance.AddCollectionToPool(_brokenObjectCollection);
		}

	}

}