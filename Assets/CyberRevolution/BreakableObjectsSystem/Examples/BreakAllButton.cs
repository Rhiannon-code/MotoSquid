using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Examples {

	public class BreakAllButton : MonoBehaviour {

		public void BreakAll() {
			var objects = FindObjectsOfType<BreakableObject>();
			foreach (BreakableObject breakableObject in objects)
				breakableObject.Break();
		}

	}

}