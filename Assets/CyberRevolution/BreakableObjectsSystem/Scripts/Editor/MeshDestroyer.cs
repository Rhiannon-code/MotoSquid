using System.Collections.Generic;
using CyberRevolution.BreakableObjectsSystem.Scripts.Common;
using UnityEngine;
using Plane = UnityEngine.Plane;

namespace CyberRevolution.BreakableObjectsSystem.Scripts.Editor {

	public class MeshDestroyer {

		public MeshFilter MeshFilter { get; }

		public GameObject GameObject { get; }

		public Transform Transform { get; }

		public List<GameObject> Parts { get; } = new List<GameObject>();

		private bool _edgeSet;
		private Vector3 _edgeVertex = Vector3.zero;
		private Vector2[] _edgeUV;
		private Plane _edgePlane;

		private readonly int _cutCascades;
		private readonly bool _addNewGeometry;

		public MeshDestroyer(MeshFilter meshFilter, int cutCascadesCount, bool addNewGeometry) {
			MeshFilter = meshFilter;
			GameObject = meshFilter.gameObject;
			Transform = meshFilter.transform;
			_cutCascades = cutCascadesCount;
			_addNewGeometry = addNewGeometry;
		}

		public void DestroyMesh() {
			Mesh originalMesh = MeshFilter.sharedMesh;

			originalMesh.RecalculateBounds();
			var parts = new List<PartMesh>();
			var subParts = new List<PartMesh>();

			var mainPart = new PartMesh {
				Vertices = originalMesh.vertices,
				Normals = originalMesh.normals,
				Triangles = new int[originalMesh.subMeshCount][],
				Bounds = originalMesh.bounds,
				UVs = originalMesh.GetAllUVs(PartMesh.UV_CHANNELS_COUNT)
			};

			var localScale = MeshFilter.transform.localScale;
			Vector3 boundsSize = mainPart.Bounds.size;
			mainPart.Bounds.size = new Vector3(boundsSize.x / localScale.x, boundsSize.y / localScale.y, boundsSize.z / localScale.z);
			for (int i = 0; i < originalMesh.subMeshCount; i++)
				mainPart.Triangles[i] = originalMesh.GetTriangles(i);

			parts.Add(mainPart);

			for (var c = 0; c < _cutCascades; c++) {
				for (var i = 0; i < parts.Count; i++) {
					var bounds = parts[i].Bounds;

					var plane = new Plane(Random.onUnitSphere, new Vector3(Random.Range(bounds.min.x, bounds.max.x),
						Random.Range(bounds.min.y, bounds.max.y),
						Random.Range(bounds.min.z, bounds.max.z)));


					subParts.Add(GenerateMesh(parts[i], plane, true));
					subParts.Add(GenerateMesh(parts[i], plane, false));
				}
				parts = new List<PartMesh>(subParts);
				subParts.Clear();
			}

			for (var i = 0; i < parts.Count; i++) {
				if (parts[i].TryMakeGameObject(this)) {
					Parts.Add(parts[i].GameObject);
				}
			}

			if (Parts.Count == 0) {
				Debug.LogError("Can not destroy mesh");
			}
		}

		private PartMesh GenerateMesh(PartMesh original, Plane plane, bool left) {
			var partMesh = new PartMesh();
			var ray1 = new Ray();
			var ray2 = new Ray();

			List<(int, int)> planeEdges = new List<(int, int)>();

			Vector3[] normalsPool = new Vector3[3];
			Vector3[] verticesPool = new Vector3[3];
			Vector2[][] uvsPool = new Vector2[3][];

			for (var i = 0; i < uvsPool.Length; i++) {
				uvsPool[i] = new Vector2[PartMesh.UV_CHANNELS_COUNT];
			}

			bool hasNormals = original.HasNormals();


			for (var i = 0; i < original.Triangles.Length; i++) {
				var triangles = original.Triangles[i];
				_edgeSet = false;

				for (var j = 0; j < triangles.Length; j = j + 3) {
					var sideA = plane.GetSide(original.Vertices[triangles[j]]) == left;
					var sideB = plane.GetSide(original.Vertices[triangles[j + 1]]) == left;
					var sideC = plane.GetSide(original.Vertices[triangles[j + 2]]) == left;

					var sideCount = (sideA ? 1 : 0) +
					                (sideB ? 1 : 0) +
					                (sideC ? 1 : 0);
					if (sideCount == 0) {
						continue;
					}


					if (sideCount == 3) {
						for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++) {
							verticesPool[vertexIndex] = original.Vertices[triangles[j + vertexIndex]];
							if (hasNormals) {
								normalsPool[vertexIndex] = original.Normals[triangles[j + vertexIndex]];
							}
							uvsPool[vertexIndex] = original.GetUVsAtVertex(triangles[j + vertexIndex]);
						}

						partMesh.AddTriangle(i, verticesPool, hasNormals ? normalsPool : null, uvsPool);
						continue;
					}

					//cut points
					var singleIndex = sideB == sideC ? 0 : sideA == sideC ? 1 : 2;

					ray1.origin = original.Vertices[triangles[j + singleIndex]];
					var dir1 = original.Vertices[triangles[j + ((singleIndex + 1) % 3)]] - original.Vertices[triangles[j + singleIndex]];
					ray1.direction = dir1;
					plane.Raycast(ray1, out float enter1);
					var lerp1 = enter1 / dir1.magnitude;

					ray2.origin = original.Vertices[triangles[j + singleIndex]];
					var dir2 = original.Vertices[triangles[j + ((singleIndex + 2) % 3)]] - original.Vertices[triangles[j + singleIndex]];
					ray2.direction = dir2;
					plane.Raycast(ray2, out float enter2);
					var lerp2 = enter2 / dir2.magnitude;

					//first vertex = ancor
					var edgeUvs1 = new Vector2[PartMesh.UV_CHANNELS_COUNT];
					VectorUtils.LerpVector2Array(original.GetUVsAtVertex(triangles[j + singleIndex]), original.GetUVsAtVertex(triangles[j + ((singleIndex + 1) % 3)]), lerp1, edgeUvs1);
					var edgeUvs2 = new Vector2[PartMesh.UV_CHANNELS_COUNT];
					VectorUtils.LerpVector2Array(original.GetUVsAtVertex(triangles[j + singleIndex]), original.GetUVsAtVertex(triangles[j + ((singleIndex + 2) % 3)]), lerp2, edgeUvs2);

					if (_addNewGeometry) {
						AddEdge(i,
							partMesh,
							left ? plane.normal * -1f : plane.normal,
							ray1.origin + ray1.direction.normalized * enter1,
							ray2.origin + ray2.direction.normalized * enter2,
							edgeUvs1,
							edgeUvs2);
					}

					if (sideCount == 1) {
						planeEdges.Add((partMesh.VerticesInProcess.Count + 1, partMesh.VerticesInProcess.Count + 2));

						(verticesPool[0], verticesPool[1], verticesPool[2]) =
							(original.Vertices[triangles[j + singleIndex]],
								ray1.origin + ray1.direction.normalized * enter1,
								ray2.origin + ray2.direction.normalized * enter2);

						if (hasNormals) {
							(normalsPool[0], normalsPool[1], normalsPool[2]) =
								(original.Normals[triangles[j + singleIndex]],
									Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
									Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
						}

						uvsPool[0] = original.GetUVsAtVertex(triangles[j + singleIndex]);

						VectorUtils.LerpVector2Array(original.GetUVsAtVertex(triangles[j + singleIndex]),
							original.GetUVsAtVertex(triangles[j + (singleIndex + 1) % 3]), lerp1, uvsPool[1]);

						VectorUtils.LerpVector2Array(original.GetUVsAtVertex(triangles[j + singleIndex]),
							original.GetUVsAtVertex(triangles[j + (singleIndex + 2) % 3]), lerp2, uvsPool[2]);

						partMesh.AddTriangle(i, verticesPool, hasNormals ? normalsPool : null, uvsPool);
						continue;
					}

					if (sideCount == 2) {
						(verticesPool[0], verticesPool[1], verticesPool[2]) =
							(ray1.origin + ray1.direction.normalized * enter1,
								original.Vertices[triangles[j + ((singleIndex + 1) % 3)]],
								original.Vertices[triangles[j + ((singleIndex + 2) % 3)]]);
						if (hasNormals) {

							(normalsPool[0], normalsPool[1], normalsPool[2]) =
								(Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
									original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
									original.Normals[triangles[j + ((singleIndex + 2) % 3)]]);
						}


						VectorUtils.LerpVector2Array(original.GetUVsAtVertex(triangles[j + singleIndex]),
							original.GetUVsAtVertex(triangles[j + (singleIndex + 1) % 3]), lerp1, uvsPool[0]);
						uvsPool[1] = original.GetUVsAtVertex(triangles[j + ((singleIndex + 1) % 3)]);
						uvsPool[2] = original.GetUVsAtVertex(triangles[j + ((singleIndex + 2) % 3)]);

						partMesh.AddTriangle(i, verticesPool, hasNormals ? normalsPool : null, uvsPool);

						(verticesPool[0], verticesPool[1], verticesPool[2]) =
							(ray1.origin + ray1.direction.normalized * enter1,
								original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
								ray2.origin + ray2.direction.normalized * enter2);
						if (hasNormals) {
							(normalsPool[0], normalsPool[1], normalsPool[2]) =
								(Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
									original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
									Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
						}

						VectorUtils.LerpVector2Array(original.GetUVsAtVertex(triangles[j + singleIndex]),
							original.GetUVsAtVertex(triangles[j + (singleIndex + 1) % 3]), lerp1, uvsPool[0]);
						uvsPool[1] = original.GetUVsAtVertex(triangles[j + ((singleIndex + 2) % 3)]);
						VectorUtils.LerpVector2Array(original.GetUVsAtVertex(triangles[j + singleIndex]),
							original.GetUVsAtVertex(triangles[j + (singleIndex + 2) % 3]), lerp2, uvsPool[0]);

						planeEdges.Add((partMesh.VerticesInProcess.Count, partMesh.VerticesInProcess.Count + 2));
						partMesh.AddTriangle(i, verticesPool, hasNormals ? normalsPool : null, uvsPool);
					}


				}
			}

			partMesh.FillArrays(planeEdges);
			return partMesh;
		}

		private void AddEdge(int subMesh, PartMesh partMesh, Vector3 normal, Vector3 vertex1, Vector3 vertex2, Vector2[] uvs1, Vector2[] uvs2) {
			if (!_edgeSet) {
				_edgeSet = true;
				_edgeVertex = vertex1;
				_edgeUV = uvs1;
			} else {
				_edgePlane.Set3Points(_edgeVertex, vertex1, vertex2);

				// todo: Optimize: use pools
				partMesh.AddTriangle(subMesh,
					new[] {
						_edgeVertex,
						_edgePlane.GetSide(_edgeVertex + normal) ? vertex1 : vertex2,
						_edgePlane.GetSide(_edgeVertex + normal) ? vertex2 : vertex1
					},
					new[] {
						normal,
						normal,
						normal,
					},
					new[] {
						_edgeUV,
						uvs1,
						uvs2
					});
			}
		}

	}

}