using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Examples.Example2.Effect {

	public class BreakEffect : OnBreakBehaviourAbstract {

		[SerializeField]
		private ParticleSystem _particleSystem;

		public override void OnBreak() {
			_particleSystem.Play(true);
		}

	}

}