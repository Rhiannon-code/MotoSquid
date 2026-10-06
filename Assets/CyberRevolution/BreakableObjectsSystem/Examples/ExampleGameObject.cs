using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CyberRevolution.BreakableObjectsSystem.Examples {

	public class ExampleGameObject : MonoBehaviour {

		private Vector3 _axis;

		private void Awake() {
			_axis = Random.insideUnitSphere;
			GetComponent<BreakableObject>().OriginalObject.GetComponent<Renderer>().material.color = Random.ColorHSV(0f, 1f, 0.9f, 1f, 0.9f, 1f);
			transform.Rotate(_axis * Random.Range(0, 360));
		}

	}

}