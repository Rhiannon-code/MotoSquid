using System;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Breakable {

	[Serializable]
	public class BreakableObjectParams {

		[Header("Explosion")]
		public float _minForce = 200;
		public float _maxForce = 600;
		public float _explosionRadius = 10;
		[Header("Torque")]
		public bool _enableTorque;
		public float _torqueMin = 50;
		public float _torqueMax = 500;
		[Header("Shards")]
		public float _shardsLifetimeMin = 3;
		public float _shardsLifetimeMax = 4;
		public float _shardsScaleFactor = 1f;

	}

}