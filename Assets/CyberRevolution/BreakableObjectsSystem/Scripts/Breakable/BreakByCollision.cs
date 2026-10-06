using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	public class BreakByCollision : BreakAbstractTrigger {

		private void OnCollisionEnter(Collision other) {
			Vector3 explosionPosition = other.GetContact(0).point;
			_breakableObject.Break(explosionPosition);
			Debug.DrawLine(explosionPosition, explosionPosition + Vector3.one, Color.red);
		}

	}

}