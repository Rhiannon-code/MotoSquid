using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	public abstract class BreakAbstractTrigger : MonoBehaviour {

		[SerializeField]
		protected Collider _collider;
		[SerializeField]
		protected BreakableObject _breakableObject;

		public BreakableObject BreakableObject {
			get => _breakableObject;
			set => _breakableObject = value;
		}

		public Collider Collider {
			get => _collider;
			set => _collider = value;
		}

		private void Reset() {
			_breakableObject = transform.GetComponent<BreakableObject>();
			if (_breakableObject != null) {
				_collider = _breakableObject.OriginalObject.GetComponent<Collider>();
				if (_collider == null) {
					MeshCollider meshCollider = _breakableObject.OriginalObject.AddComponent<MeshCollider>();
					meshCollider.convex = true;
					_collider = meshCollider;
				}
				var rb = _breakableObject.GetComponent<Rigidbody>();
				if (rb == null) {
					rb = _breakableObject.gameObject.AddComponent<Rigidbody>();
					rb.isKinematic = true;
					_breakableObject.Rigidbody = rb;
				}
			}
		}

	}

}