using System.Collections.Generic;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	[CreateAssetMenu(fileName = FILENAME, menuName = MENU_ENTRY)]
	public class BrokenObjectCollection : ScriptableObject {

		public const string FILENAME = "BrokenObjectCollection";
		public const string MENU_ENTRY = "Broken object collection/Empty";

		[SerializeField]
		private List<BrokenObject> _breakableObjectPrefabs;
		[SerializeField]
		private int _poolCapacity;

		public int PoolCapacity => _poolCapacity;

		public List<BrokenObject> BreakableObjectPrefabs {
			get => _breakableObjectPrefabs;
			set => _breakableObjectPrefabs = value;
		}

	}

}