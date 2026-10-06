using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	internal struct BrokenPrefabReinitializeData {

		public readonly Vector3[] Positions;
		public readonly Quaternion[] Rotations;
		public BrokenPrefabReinitializeData(Vector3[] positions, Quaternion[] rotations) {
			Positions = positions;
			Rotations = rotations;
		}

	}

}