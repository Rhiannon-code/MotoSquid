using System.Collections.Generic;
using UnityEngine;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Editor {

	public class PartMesh {

		public const int UV_CHANNELS_COUNT = 7;

		private readonly List<Vector3> _verticesInProcess = new List<Vector3>();
		private readonly List<Vector3> _normalsInProcess = new List<Vector3>();
		private readonly List<List<int>> _trianglesInProcess = new List<List<int>>();

		public Vector3[] Vertices;
		public Vector3[] Normals;
		public int[][] Triangles;
		public GameObject GameObject;
		public Bounds Bounds;
		public List<Vector2>[] UVs;

		/// <summary>
		/// Edges lying on the slicing plane
		/// </summary>
		public List<(int, int)> PlaneEdges = new List<(int, int)>();

		/// <summary>
		/// Temp vertices list 
		/// </summary>
		public IReadOnlyList<Vector3> VerticesInProcess => _verticesInProcess;

		public PartMesh() {
			UVs = new List<Vector2>[UV_CHANNELS_COUNT];
			for (int i = 0; i < UV_CHANNELS_COUNT; i++) {
				UVs[i] = new List<Vector2>();
			}
		}
		

		public void AddTriangle(int submesh, Vector3[] verticesPool, Vector3[] normalsPool, Vector2[][] uvsPool) {
			if (submesh >= _trianglesInProcess.Count) {
				_trianglesInProcess.Add(new List<int>());
			}

			for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++) {
				_trianglesInProcess[submesh].Add(_verticesInProcess.Count);
				_verticesInProcess.Add(verticesPool[vertexIndex]);
				Bounds.min = Vector3.Min(Bounds.min, verticesPool[vertexIndex]);
				Bounds.max = Vector3.Min(Bounds.max, verticesPool[vertexIndex]);
				if (normalsPool != null) {
					_normalsInProcess.Add(normalsPool[vertexIndex]);
				}

				for (int uvChanel = 0; uvChanel < UV_CHANNELS_COUNT; uvChanel++) {
					Vector2 uv = uvsPool[vertexIndex][uvChanel];
					if (uv != default) {
						UVs[uvChanel].Add(uv);
					}
				}
			}
		}

		public void FillArrays(List<(int, int)> planeEdges) {
			Vertices = _verticesInProcess.ToArray();
			Normals = _normalsInProcess.ToArray();
			Triangles = new int[_trianglesInProcess.Count][];
			for (var i = 0; i < _trianglesInProcess.Count; i++)
				Triangles[i] = _trianglesInProcess[i].ToArray();
			PlaneEdges = planeEdges;
		}

		public bool TryMakeGameObject(MeshDestroyer original) {
			if (Vertices.Length == 0) {
				return false;
			}

			var originalTransform = original.Transform;
			GameObject = new GameObject(original.GameObject.name) {
				transform = {
					parent = originalTransform.parent,
					localPosition = originalTransform.localPosition,
					localRotation = originalTransform.localRotation,
					localScale = originalTransform.localScale
				}
			};

			var mesh = new Mesh {
				name = original.GameObject.GetComponent<MeshFilter>().sharedMesh.name,
				vertices = Vertices,
				normals = Normals,
				subMeshCount = Triangles.Length
			};

			for (var index = 0; index < UVs.Length; index++) {
				List<Vector2> uvs = UVs[index];
				if (uvs.Count > 0) {
					mesh.SetUVs(index, uvs);
				}
			}

			for (var i = 0; i < Triangles.Length; i++) {
				mesh.SetTriangles(Triangles[i], i, true);
			}
			Bounds = mesh.bounds;

			var renderer = GameObject.AddComponent<MeshRenderer>();
			renderer.materials = original.GameObject.GetComponent<MeshRenderer>().sharedMaterials;

			var filter = GameObject.AddComponent<MeshFilter>();
			filter.sharedMesh = mesh;

			var collider = GameObject.AddComponent<MeshCollider>();
			collider.convex = true;

			return true;

		}

		public bool HasNormals() {
			return Normals != null && Normals.Length == Vertices.Length;
		}

		public bool HasUVs(int uvChannel) {
			var uvs = UVs[uvChannel];
			return uvs != null && uvs.Count == Vertices.Length;
		}

		public Vector2[] GetUVsAtVertex(int index) {
			var uvs = new Vector2[UV_CHANNELS_COUNT];
			for (int i = 0; i < UV_CHANNELS_COUNT; i++) {
				var uvChannel = UVs[i];
				uvs[i] = uvChannel.Count != 0 ? uvChannel[index] : default;
			}
			return uvs;
		}

	}

}