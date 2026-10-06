using System.Collections.Generic;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Common {

	public static class MeshUtils {

		public static List<Vector2>[] GetAllUVs(this Mesh mesh, int channelsCount) {
			List<Vector2>[] allUVs = new List<Vector2>[channelsCount];
			for (int i = 0; i < channelsCount; i++) {
				List<Vector2> uvs = new List<Vector2>();
				mesh.GetUVs(i, uvs);
				allUVs[i] = uvs;
			}
			return allUVs;
		}

	}

}