using System.Collections;
using CyberRevolution.BreakableObjectsSystem.Scripts.Common;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	public class BreakableObject : MonoBehaviour {

		[SerializeField]
		private GameObject _originalObject;
		[SerializeField]
		private BreakableObjectParams _params;
		[SerializeField]
		private BrokenObjectCollection _brokenObjectsCollection;

		[SerializeField]
		private OnBreakBehaviourAbstract _onBreakBehaviour;
		[SerializeField]
		private OnRestoreBehaviourAbstract _onRestoreBehaviour;
		[SerializeField]
		private Rigidbody _rigidbody;

		private BrokenObject _brokenObjectActive;
		private bool _rigidbodyKinematicCachedState;

		public GameObject OriginalObject {
			get => _originalObject;
			set => _originalObject = value;
		}

		public BreakableObjectParams Params {
			get => _params;
			set => _params = value;
		}

		public BrokenObjectCollection BrokenObjectsCollection {
			get => _brokenObjectsCollection;
			set => _brokenObjectsCollection = value;
		}

		public Rigidbody Rigidbody {
			get => _rigidbody;
			set => _rigidbody = value;
		}

		public bool Broken { get; private set; }
		public bool IsBreakingInProgress { get; private set; }
		private int _currentBrokenObjectIndex;

		private void Awake() {
			if (_rigidbody != null) {
				_rigidbodyKinematicCachedState = _rigidbody.isKinematic;
			}
		}

		public void Break(Vector3 explosionPosition = default) {
			if (Broken) {
				return;
			}
			StartCoroutine(BreakCoroutine(explosionPosition));
		}

		public void Restore() {
			if (IsBreakingInProgress) {
				StopAllCoroutines();
				FinishBreaking(_currentBrokenObjectIndex);
			}

			if (_onRestoreBehaviour != null) {
				_onRestoreBehaviour.OnRestore();
			}

			OriginalObject.SetActive(true);
			if (_brokenObjectActive != null) {
				_brokenObjectActive.SetActiveFast(false);
			}

			if (_rigidbody != null) {
				_rigidbody.isKinematic = _rigidbodyKinematicCachedState;
			}

			Broken = false;
		}

		private IEnumerator BreakCoroutine(Vector3 explosionPosition) {
			Broken = true;
			IsBreakingInProgress = true;

			if (_rigidbody != null) {
				RigidbodyUtils.ResetRigidbody(_rigidbody, false);
			}

			if (_onBreakBehaviour != null) {
				_onBreakBehaviour.OnBreak();
			}

			OriginalObject.SetActive(false);
			Transform tr = transform;

			_brokenObjectActive = BrokenObjectPools.Instance.GetRandomObjectFromCollection(_brokenObjectsCollection, tr,
				tr.position, tr.rotation, OriginalObject.transform.localScale * _params._shardsScaleFactor, out _currentBrokenObjectIndex);
			_brokenObjectActive.SetMaterial(OriginalObject.GetComponent<Renderer>().sharedMaterial);

			if (explosionPosition == default) {
				explosionPosition = _brokenObjectActive.transform.position;
			}
			//hack: Wait for physics update for shards reset
			yield return new WaitForFixedUpdate();

			foreach (Rigidbody rb in _brokenObjectActive.Rigidbodies) {
				rb.linearVelocity = Vector3.zero;
				rb.AddExplosionForce(Random.Range(_params._minForce, _params._maxForce), explosionPosition, _params._explosionRadius);
				if (_params._enableTorque) {
					float t = Random.Range(_params._torqueMin, _params._torqueMax);
					rb.AddTorque(Random.onUnitSphere * t);
				}
			}

			yield return Yielders.ForSeconds(Random.Range(_params._shardsLifetimeMin, _params._shardsLifetimeMax));
			FinishBreaking(_currentBrokenObjectIndex);
		}

		private void FinishBreaking(int index) {
			BrokenObjectPools.Instance.ReleaseObjectFromCollection(_brokenObjectsCollection, _brokenObjectActive, index);
			_brokenObjectActive = null;
			IsBreakingInProgress = false;
		}

		private void Reset() {
			_rigidbody = GetComponent<Rigidbody>();
		}

	}

}