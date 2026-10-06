using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.Rendering.HighDefinition;

namespace MotoSquid.Bike
{
    public static class VoodooDashMaterials
    {
        const string RevMatPath  = "Assets/Materials/VoodooDash.mat";
        const string GearMatPath = "Assets/Materials/VoodooDashGear.mat";
        const string RevMapPath  = "Assets/Textures/UI/SPEEDO.png";
        const string GearMapPath = "Assets/Textures/UI/GEAR.png";

        const float Nits        = 200f;   // Tune on the material, 0 exposure weight makes it absolute
        const int   RevCells    = 12;
        const int   GearCells   = 7;
        const float CellAspect  = 933f / 365f;
        const float PanelFill   = 0.92f;
        const float ProudOffset = 0.004f;

        static readonly Vector3 DashNormal = new Vector3(0f, 0.4349f, -0.9005f);
        const float DashNormalTolerance = 0.99f;
        static readonly Bounds DashRegion = new Bounds(new Vector3(0f, 0.9415f, 0.5495f),
                                                       new Vector3(0.31f, 0.13f, 0.09f));

        [MenuItem("MotoSquid/Bike/Create Voodoo Dash Materials")]
        static void CreateMaterialsOnly() => Run(materialsOnly: true);

        [MenuItem("MotoSquid/Bike/Build Voodoo Dash On Selected Bike")]
        static void BuildOnSelection() => Run(materialsOnly: false);

        [MenuItem("MotoSquid/Bike/Wire Dash Component On Selected Bike")]
        static void WireOnSelection()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogError("Dash wiring: select the bike root first.");
                return;
            }

            Transform body = FindBody(root.transform);
            if (body == null)
            {
                Debug.LogError($"Dash wiring: no 'Body' mesh under {root.name}");
                return;
            }

            Transform bar   = body.Find("DashRevBar");
            Transform gear  = body.Find("DashGear");
            Transform speed = body.Find("DashSpeed");
            if (bar == null || gear == null)
            {
                Debug.LogError("Dash wiring: DashRevBar or DashGear missing under Body. " +
                               "Run Build Voodoo Dash On Selected Bike instead.");
                return;
            }

            Transform gearNumber = body.Find("DashGearNumber") ?? MakeGearNumber(body, gear);

            Wire(root.transform, bar.GetComponent<Renderer>(), gear.GetComponent<Renderer>(),
                 gearNumber != null ? gearNumber.GetComponent<TMP_Text>() : null,
                 speed != null ? speed.GetComponent<TMP_Text>() : null);
        }

        // The HUD shows a gear sprite AND a gear number, the dash only ever had the sprite
        static Transform MakeGearNumber(Transform body, Transform gearQuad)
        {
            var created = new GameObject("DashGearNumber");
            created.transform.SetParent(body, false);

            var text = created.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.text      = "1";
            text.fontSize  = 1f;

            created.transform.SetPositionAndRotation(
                gearQuad.position + gearQuad.forward * -0.002f, gearQuad.rotation);
            created.transform.localScale = gearQuad.localScale * 0.5f;
            return created.transform;
        }

        static void Run(bool materialsOnly)
        {
            var revMap  = AssetDatabase.LoadAssetAtPath<Texture>(RevMapPath);
            var gearMap = AssetDatabase.LoadAssetAtPath<Texture>(GearMapPath);
            if (revMap == null || gearMap == null)
            {
                Debug.LogError($"Dash setup: missing {(revMap == null ? RevMapPath : GearMapPath)}");
                return;
            }

            Material rev  = SpriteStripMaterial(RevMatPath, revMap, RevCells);
            Material gear = SpriteStripMaterial(GearMatPath, gearMap, GearCells);
            if (rev == null || gear == null) return;

            AssetDatabase.SaveAssets();
            Debug.Log($"Dash setup: materials ready at {RevMatPath} and {GearMatPath}");

            if (materialsOnly) return;

            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogError("Dash setup: select the bike root first (open the prefab, click its top object).");
                return;
            }
            BuildQuads(root.transform, rev, gear);
        }

        static Material SpriteStripMaterial(string path, Texture map, int cells)
        {
            var shader = Shader.Find("HDRP/Unlit");
            if (shader == null) { Debug.LogError("Dash setup: HDRP/Unlit not found"); return null; }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                // An earlier version of this tool made it Lit, whose properties are named differently
                material.shader = shader;
            }

            // Transparent so the strip's own alpha cuts the gauge out. The base colour is black and
            // emission is left unmultiplied by it, so the line art is pure glow rather than lit white
            HDMaterial.SetSurfaceType(material, transparent: true);
            material.SetTexture("_UnlitColorMap", map);
            material.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 1f));
            material.SetTexture("_EmissiveColorMap", map);
            material.SetFloat("_AlbedoAffectEmissive", 0f);
            material.SetFloat("_UseEmissiveIntensity", 1f);
            material.SetColor("_EmissiveColorLDR", Color.white);
            material.SetFloat("_EmissiveExposureWeight", 0f);
            HDMaterial.SetEmissiveIntensity(material, Nits, EmissiveIntensityUnit.Nits);
            material.EnableKeyword("_EMISSIVE_COLOR_MAP");

            // Rest on the strip's first real frame. Play mode drives this per bike, but without it the
            // Scene view shows every cell at once and reads as tiling
            var st = new Vector4(1f / cells, 1f, 1f / cells, 0f);
            material.SetVector("_UnlitColorMap_ST", st);
            material.SetVector("_EmissiveColorMap_ST", st);

            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildQuads(Transform root, Material revMat, Material gearMat)
        {
            Transform body = FindBody(root);
            if (body == null)
            {
                Debug.LogError($"Dash setup: no 'Body' mesh under {root.name}");
                return;
            }

            if (!MeasurePanel(root, body, out Vector3 centre, out Vector3 normal,
                              out Vector3 right, out Vector3 up, out float width, out float height))
            {
                Debug.LogError("Dash setup: could not find the dash faces on the Body mesh");
                return;
            }

            Debug.Log($"Dash setup: panel {width * 100f:F1} x {height * 100f:F1} cm, " +
                      $"centre {centre} in {root.name} space");

            float barWidth  = Mathf.Min(width, height * CellAspect) * PanelFill;
            float barHeight = barWidth / CellAspect;

            Vector3 worldNormal = root.TransformDirection(normal);
            Vector3 worldUp     = root.TransformDirection(up);

            // Sizes above are in root space, the quads live several scaled parents deeper, so every
            // size is taken to world and then back down through whatever the parent chain scales by
            float rootScale = root.lossyScale.x;

            var bar = Quad(body, "DashRevBar", revMat);
            bar.position = root.TransformPoint(centre + normal * ProudOffset);
            SetWorldSize(bar, barWidth * rootScale, barHeight * rootScale);
            Face(bar, worldNormal, worldUp);

            // Starting corner for the gear readout, it is meant to be nudged in the Scene view
            float gearWidth = barWidth * 0.16f;
            var gearQuad = Quad(body, "DashGear", gearMat);
            gearQuad.position = root.TransformPoint(centre + normal * (ProudOffset * 1.5f)
                                                    + right * (barWidth * 0.40f)
                                                    - up * (barHeight * 0.28f));
            SetWorldSize(gearQuad, gearWidth * rootScale, gearWidth / CellAspect * rootScale);
            Face(gearQuad, worldNormal, worldUp);

            Transform speed = FindOrCreateSpeed(body, root, centre, normal, right, up,
                                                barWidth, barHeight, worldNormal, worldUp);

            Transform gearNumber = body.Find("DashGearNumber") ?? MakeGearNumber(body, gearQuad);

            Wire(root, bar.GetComponent<Renderer>(), gearQuad.GetComponent<Renderer>(),
                 gearNumber.GetComponent<TMP_Text>(),
                 speed != null ? speed.GetComponent<TMP_Text>() : null);

            Debug.Log($"Dash setup: built DashRevBar, DashGear and DashSpeed under {body.name}");
        }

        static void Wire(Transform root, Renderer bar, Renderer gear, TMP_Text gearNumber, TMP_Text speed)
        {
            var screen = root.GetComponent<BikeDashScreen>();
            if (screen == null) screen = root.gameObject.AddComponent<BikeDashScreen>();
            if (screen == null)
            {
                Debug.LogError($"Dash wiring: could not add BikeDashScreen to {root.name}. " +
                               "Add it by hand: select the root, Add Component, Bike Dash Screen.");
                return;
            }

            var so = new SerializedObject(screen);
            Assign(so, "bike",      root.GetComponentInChildren<BikeController>());
            Assign(so, "boost",     root.GetComponentInChildren<BoostSystem>());
            Assign(so, "revBar",    bar);
            Assign(so, "gear",      gear);
            Assign(so, "gearText",  gearNumber);
            Assign(so, "speedText", speed);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(root.gameObject);

            var check = new SerializedObject(screen);
            Debug.Log($"Dash wiring on {root.name}: " +
                      $"bike={check.FindProperty("bike").objectReferenceValue != null} " +
                      $"boost={check.FindProperty("boost").objectReferenceValue != null} " +
                      $"revBar={bar != null} gear={gear != null} " +
                      $"gearText={gearNumber != null} speedText={speed != null}");
        }

        static void Assign(SerializedObject so, string field, Object value)
        {
            var property = so.FindProperty(field);
            if (property == null) return;
            if (value != null || property.objectReferenceValue == null)
                property.objectReferenceValue = value;
        }

        static Transform FindBody(Transform root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == "Body" && filter.sharedMesh != null)
                    return filter.transform;
            return null;
        }

        static bool MeasurePanel(Transform root, Transform body, out Vector3 centre, out Vector3 normal,
                                 out Vector3 right, out Vector3 up, out float width, out float height)
        {
            centre = normal = right = up = Vector3.zero;
            width = height = 0f;

            Mesh mesh = body.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            var picked = new List<int>();
            Vector3 normalSum = Vector3.zero;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = ToRoot(root, body, verts[tris[i]]);
                Vector3 b = ToRoot(root, body, verts[tris[i + 1]]);
                Vector3 c = ToRoot(root, body, verts[tris[i + 2]]);
                Vector3 faceCentre = (a + b + c) / 3f;
                if (!DashRegion.Contains(faceCentre)) continue;

                // Winding decides the sign, which differs between exporters, so match on the axis and
                // settle the direction afterwards against the known outward normal
                Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
                float alignment = Vector3.Dot(faceNormal, DashNormal);
                if (Mathf.Abs(alignment) < DashNormalTolerance) continue;

                picked.Add(i);
                normalSum += alignment < 0f ? -faceNormal : faceNormal;
            }

            if (picked.Count == 0) return false;

            normal = normalSum.normalized;
            right  = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
            up     = Vector3.Cross(normal, right).normalized;
            if (up.y < 0f) { up = -up; right = Vector3.Cross(up, normal).normalized; }

            float minU = float.MaxValue, maxU = float.MinValue;
            float minV = float.MaxValue, maxV = float.MinValue;
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (int i in picked)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = ToRoot(root, body, verts[tris[i + k]]);
                    sum += p; count++;
                }

            Vector3 mean = sum / count;
            foreach (int i in picked)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 d = ToRoot(root, body, verts[tris[i + k]]) - mean;
                    float u = Vector3.Dot(d, right), v = Vector3.Dot(d, up);
                    minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                    minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
                }

            width  = maxU - minU;
            height = maxV - minV;
            centre = mean + right * ((maxU + minU) * 0.5f) + up * ((maxV + minV) * 0.5f);
            return true;
        }

        static Vector3 ToRoot(Transform root, Transform body, Vector3 local) =>
            root.InverseTransformPoint(body.TransformPoint(local));

        // A Quad primitive's face points down its local -Z while TMP text reads along +Z, so the mesh
        // is asked which way it is wound rather than assuming
        static void Face(Transform target, Vector3 worldNormal, Vector3 worldUp)
        {
            // TMP text has no MeshFilter, and like every primitive Unity creates it is authored to be
            // read from its local -Z, which is why the fallback is back and not forward
            var filter = target.GetComponent<MeshFilter>();
            Vector3 meshNormal = filter != null && filter.sharedMesh != null &&
                                 filter.sharedMesh.normals.Length > 0
                ? filter.sharedMesh.normals[0]
                : Vector3.back;

            Vector3 forward = meshNormal.z < 0f ? -worldNormal : worldNormal;
            target.rotation = Quaternion.LookRotation(forward, worldUp);
        }

        static void SetWorldSize(Transform target, float width, float height)
        {
            Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(width  / Mathf.Max(1e-6f, parentScale.x),
                                            height / Mathf.Max(1e-6f, parentScale.y), 1f);
        }

        static Transform Quad(Transform parent, string name, Material material)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                var created = GameObject.CreatePrimitive(PrimitiveType.Quad);
                created.name = name;
                Object.DestroyImmediate(created.GetComponent<Collider>());
                created.transform.SetParent(parent, false);
                existing = created.transform;
            }

            var renderer = existing.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return existing;
        }

        static Transform FindOrCreateSpeed(Transform body, Transform root, Vector3 centre, Vector3 normal,
                                           Vector3 right, Vector3 up, float barWidth, float barHeight,
                                           Vector3 worldNormal, Vector3 worldUp)
        {
            Transform existing = body.Find("DashSpeed");
            if (existing == null)
            {
                var created = new GameObject("DashSpeed");
                created.transform.SetParent(body, false);
                var text = created.AddComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.text      = "0";
                existing = created.transform;
            }

            var label = existing.GetComponent<TMP_Text>();
            if (label != null) label.fontSize = barHeight * 0.45f;

            var rect = existing.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(barWidth * 0.35f, barHeight * 0.5f);

            existing.localScale = Vector3.one;
            SetWorldSize(existing, 1f, 1f);
            existing.position   = root.TransformPoint(centre + normal * (ProudOffset * 1.5f)
                                                      - right * (barWidth * 0.18f));
            Face(existing, worldNormal, worldUp);
            return existing;
        }
    }
}
