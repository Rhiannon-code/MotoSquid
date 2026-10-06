using CyberRevolution.BreakableObjectsSystem.Scripts.Common;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	public class BrokenObject : MonoBehaviour {

		[SerializeField]
		private Renderer[] _renderers;
		[SerializeField]
		private Rigidbody[] _rigidbodies;
		[SerializeField]
		private Collider[] _colliders;

		public Renderer[] Renderers => _renderers;
		public Rigidbody[] Rigidbodies => _rigidbodies;
		private bool _isActive;
		private bool _firstActivation = true;

		private void Init() {
			if (_colliders == null || _colliders.Length == 0) {
				_colliders = GetComponentsInChildren<Collider>(true);
			}
			if (_renderers == null || _renderers.Length == 0) {
				_renderers = GetComponentsInChildren<Renderer>(true);
			}
			if (_rigidbodies == null || _rigidbodies.Length == 0) {
				_rigidbodies = GetComponentsInChildren<Rigidbody>(true);
			}
		}

		internal void SetActiveFast(bool value) {
			Init();
			if (_isActive == value && !_firstActivation) {
				return;
			}
			_isActive = value;
			if (value) {
				_firstActivation = false;
			}

			for (var i = 0; i < _renderers.Length; i++) {
				_renderers[i].enabled = value;
			}

			for (var i = 0; i < _rigidbodies.Length; i++) {
				Rigidbody rb = _rigidbodies[i];
				RigidbodyUtils.ResetRigidbody(rb, value);
			}

			for (int i = 0; i < _colliders.Length; i++) {
				_colliders[i].enabled = value;
			}
		}

		internal void SetMaterial(Material material) {
			for (var i = 0; i < _renderers.Length; i++) {
				_renderers[i].material = material;
			}
		}

		private void Reset() {
			Init();
		}

	}

}