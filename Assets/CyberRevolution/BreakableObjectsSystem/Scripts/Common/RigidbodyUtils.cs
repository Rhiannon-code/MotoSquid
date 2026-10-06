using CyberRevolution.BreakableObjectsSystem.Scripts.Breakable;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Common {

	public static class RigidbodyUtils {

		public static void ResetRigidbody(Rigidbody rb, bool value) {
			rb.isKinematic = !value;
			if (!value) {
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}
		}

		public static Collider GetOrAddCollider(this GameObject go, ColliderType colliderType) {
			var collider = go.GetComponent<Collider>();

			if (collider != null) {
				ColliderType? type = collider.GetColliderType();
				if (type != colliderType && colliderType != ColliderType.Default) {
					Object.DestroyImmediate(collider);
					collider = null;
				}
			}

			if (collider == null) {
				switch (colliderType) {
					case ColliderType.Box:
						collider = go.AddComponent<BoxCollider>();
						break;
					case ColliderType.Sphere:
						collider = go.AddComponent<SphereCollider>();
						break;
					case ColliderType.Mesh:
					default:
						MeshCollider meshCollider = go.AddComponent<MeshCollider>();
						meshCollider.convex = true;
						collider = meshCollider;
						break;
				}
			}
			return collider;
		}

		public static ColliderType? GetColliderType(this Collider collider) {
			return collider switch {
				MeshCollider _ => ColliderType.Mesh,
				SphereCollider _ => ColliderType.Sphere,
				BoxCollider _ => ColliderType.Box,
				_ => null
			};
		}

	}

}