using JetBrains.Annotations;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Common {

	public abstract class MonoBehaviourSceneSingleton<TMonoBehaviour> : MonoBehaviour
		where TMonoBehaviour : MonoBehaviourSceneSingleton<TMonoBehaviour> {

		private bool _mIsPrimary;

		private static TMonoBehaviour _instance;

		public static TMonoBehaviour Instance => _instance;

		[UsedImplicitly]
		protected virtual void Awake() {
			if (_instance != null) {
				_mIsPrimary = false;
				Destroy(gameObject);
				return;
			}

			_mIsPrimary = true;
			_instance = (TMonoBehaviour)this;
			OnSingletonAwake();
		}

		protected void OnEnable() {
			_mIsPrimary = true;
			_instance = (TMonoBehaviour)this;
			OnSingletonEnable();
		}

		[UsedImplicitly]
		protected void Start() {
			if (_mIsPrimary) {
				OnSingletonStart();
			}
		}

		[UsedImplicitly]
		protected virtual void OnDestroy() {
			if (!_mIsPrimary) {
				return;
			}

			try {
				OnSingletonDestroy();
			} finally {
				_instance = null;
			}
		}

		protected virtual void OnSingletonAwake() { }

		protected virtual void OnSingletonStart() { }

		protected virtual void OnSingletonDestroy() { }

		protected virtual void OnSingletonEnable() { }

	}

}