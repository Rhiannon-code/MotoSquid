using System;
using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Editor {

	[Serializable]
	public class BrokenCollectionWindowParams {

		public int CutCascadesCount = 1;
		public int IterationsCount = 2;
		public int BrokenObjectVariationsCount = 1;
		public bool ShardCollidersEnabled = true;
		public ColliderType ShardColliderType = ColliderType.Default;
		public bool AddPoolsToScene = true;
		public bool AddBrokenObjectToScene = true;
		public bool AddNewGeometry = true;
		public bool DestroyByClick = false;
		public bool DestroyByCollision = false;
		public ColliderType ObjectColliderType = ColliderType.Default;
		public bool IsKinematic = true;
		public string Path = "Assets";

		public bool ShowAdvancedSettings = false;

	}

}