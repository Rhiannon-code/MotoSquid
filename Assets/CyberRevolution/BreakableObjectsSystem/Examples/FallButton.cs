using System.Collections.Generic;
using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEngine;
using UnityEngine.UI;

namespace CyberRevolution.BreakableObjectsSystem.Examples {

	public class FallButton : MonoBehaviour {

		[SerializeField]
		private Button _button;


		private void Awake() {
			var objects = FindObjectsOfType<BreakByCollision>();
			bool found = objects?.Length > 0;
			_button.interactable = found;
			_button.GetComponentInChildren<Text>().color = found ? Color.white : Color.black;
		}

		public void Fall() {
			var objects = FindObjectsOfType<BreakByCollision>();
			foreach (BreakByCollision o in objects) {
				o.GetComponent<Rigidbody>().isKinematic = false;
			}
		}

	}

}