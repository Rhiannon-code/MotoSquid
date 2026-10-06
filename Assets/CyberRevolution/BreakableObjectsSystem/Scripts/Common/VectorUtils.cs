using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Common {

	public static class VectorUtils {

		public static void LerpVector2Array(Vector2[] v1, Vector2[] v2, float lerp, Vector2[] result) {
			for (int i = 0; i < result.Length; i++) {
				result[i] = Vector2.Lerp(v1[i], v2[i], lerp);
			}
		}

	}

}