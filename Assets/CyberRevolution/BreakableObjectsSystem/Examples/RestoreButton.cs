using System.Collections.Generic;
using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Examples {

	public class RestoreButton : MonoBehaviour {

		private readonly Dictionary<BreakableObject, Vector3> _cachedPositions = new Dictionary<BreakableObject, Vector3>();

		private void Awake() {
			var objects = FindObjectsOfType<BreakableObject>();
			foreach (BreakableObject breakableObject in objects) {
				_cachedPositions.Add(breakableObject, breakableObject.transform.position);
			}
		}

		public void Restore() {
			var objects = FindObjectsOfType<BreakableObject>();
			foreach (BreakableObject breakableObject in objects) {
				breakableObject.Restore();
				breakableObject.transform.position = _cachedPositions[breakableObject];
			}
		}

	}

}