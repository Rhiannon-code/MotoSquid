using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
namespace MotoSquid.World
{
    public class DecalStamperWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<Material> decalMaterials = new List<Material>();
        private Material selectedMaterial;
        private float previewSize = 100f;
        private float spacing = 10f;
        private bool isPlacingDecal = false;
        private Dictionary<Material, Texture2D> textureCache = new Dictionary<Material, Texture2D>();

        // Decal placement settings
        private Vector3 decalSize = Vector3.one;
        private float rotationAngle = 0f;
        private float zRotationAngle = 0f;
        private float projectionDistance = 0f;
        private const float PROJECTION_SPEED = 0.1f;
        private const float SCALE_SPEED = 0.1f;
        private const float ROTATION_SPEED = 15f;
        private DecalProjector previewDecal;
        private bool isRotating = false;
        private bool isZRotating = false;
        private Vector2 lastMousePosition;
    
        // Random variation settings
        private bool useRandomVariations = false;
        private Vector2 randomScaleRange = new Vector2(0.8f, 1.2f);
        private Vector2 randomYRotationRange = new Vector2(-180f, 180f);
        private Vector2 randomZRotationRange = new Vector2(-15f, 15f);

        // HDRP Decal shader property names
        private static readonly string[] BASECOLOR_PROPERTY_NAMES = new string[] 
        {
            "_BaseColorMap",
            "_MainTex",
            "_BaseMap"
        };

        [MenuItem("Tools/Decal Stamper")]
        public static void ShowWindow()
        {
            GetWindow<DecalStamperWindow>("Decal Stamper");
        }

        private void OnEnable()
        {
            RefreshDecalMaterials();
            SceneView.duringSceneGui += OnSceneGUI;
            CreatePreviewDecal();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            textureCache.Clear();
            DestroyPreviewDecal();
        }

        private void CreatePreviewDecal()
        {
            if (previewDecal == null)
            {
                GameObject previewObj = new GameObject("PreviewDecal");
                previewObj.hideFlags = HideFlags.HideAndDontSave;
                previewDecal = previewObj.AddComponent<DecalProjector>();
                previewDecal.scaleMode = DecalScaleMode.InheritFromHierarchy;
                previewDecal.fadeFactor = 1f;
            }
        }

        private void DestroyPreviewDecal()
        {
            if (previewDecal != null)
            {
                DestroyImmediate(previewDecal.gameObject);
                previewDecal = null;
            }
        }

     private void UpdatePreviewDecal(Vector3 position, Vector3 normal)
    {
        if (previewDecal != null && selectedMaterial != null)
        {
            previewDecal.gameObject.SetActive(true);
            previewDecal.material = selectedMaterial;

            // Apply projection distance along the normal
            Vector3 projectedPosition = position + normal * projectionDistance;
            previewDecal.transform.position = projectedPosition;

            Quaternion yRotation = Quaternion.Euler(0, rotationAngle, 0);
            Quaternion zRotation = Quaternion.Euler(0, 0, zRotationAngle);
            previewDecal.transform.rotation = zRotation * yRotation * Quaternion.LookRotation(-normal);

            // Explicitly apply the size
            previewDecal.size = decalSize;
            Debug.Log($"DecalProjector size updated to: {previewDecal.size}");
        }
    }

        private void HidePreviewDecal()
        {
            if (previewDecal != null)
            {
                previewDecal.gameObject.SetActive(false);
            }
        }

        private Texture2D GetBaseColorTexture(Material material)
        {
            foreach (string propertyName in BASECOLOR_PROPERTY_NAMES)
            {
                if (material.HasProperty(propertyName))
                {
                    Texture2D texture = material.GetTexture(propertyName) as Texture2D;
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }
            return null;
        }

        private void RefreshDecalMaterials()
        {
            decalMaterials.Clear();
            textureCache.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Material");
        
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            
                if (material != null && material.shader.name.Contains("HDRP/Decal"))
                {
                    decalMaterials.Add(material);
                    Texture2D baseColorTexture = GetBaseColorTexture(material);
                    if (baseColorTexture != null)
                    {
                        textureCache[material] = baseColorTexture;
                    }
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Materials"))
            {
                RefreshDecalMaterials();
            }
        
            if (GUILayout.Button(isPlacingDecal ? "Stop Placing" : "Start Placing"))
            {
                isPlacingDecal = !isPlacingDecal;
                if (!isPlacingDecal)
                {
                    selectedMaterial = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (isPlacingDecal)
            {
                EditorGUILayout.HelpBox(
    "Controls:\n" +
                    "Left Click - Place Decal\n" +
                    "Ctrl + Scroll - Scale Both X and Y\n" +
                    "Ctrl + Shift + Scroll - Scale X Axis\n" +
                    "Alt + Scroll - Scale Y Axis\n" +
                    "Shift + Left Click + Drag - Rotate Y Axis\n" +
                    "Alt + Left Click + Drag - Rotate Z Axis\n" +
                    "Q/E - Adjust Projection Distance\n" +
                    "R - Toggle Random Variations\n" +
                    "ESC - Cancel Placement",
                    MessageType.Info
                );

                EditorGUILayout.Space();

                // Decal transform controls
                decalSize = EditorGUILayout.Vector3Field("Decal Size", decalSize);
                EditorGUILayout.FloatField("Y Rotation", rotationAngle);
                EditorGUILayout.FloatField("Z Rotation", zRotationAngle);
                projectionDistance = EditorGUILayout.FloatField("Projection Distance", projectionDistance);

                EditorGUILayout.Space();

                // Random variation controls
                useRandomVariations = EditorGUILayout.Toggle("Use Random Variations", useRandomVariations);
                if (useRandomVariations)
                {
                    EditorGUI.indentLevel++;
                    randomScaleRange = EditorGUILayout.Vector2Field("Scale Range (Min, Max)", randomScaleRange);
                    randomYRotationRange = EditorGUILayout.Vector2Field("Y Rotation Range", randomYRotationRange);
                    randomZRotationRange = EditorGUILayout.Vector2Field("Z Rotation Range", randomZRotationRange);
                    EditorGUI.indentLevel--;
                }
            }

            previewSize = EditorGUILayout.Slider("Preview Size", previewSize, 50f, 200f);
            spacing = EditorGUILayout.Slider("Spacing", spacing, 5f, 50f);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
            EditorGUILayout.BeginVertical();
            float availableWidth = position.width - 20;
            int previewsPerRow = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (previewSize + spacing)));

            for (int i = 0; i < decalMaterials.Count; i++)
            {
                if (i % previewsPerRow == 0)
                {
                    if (i > 0)
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.BeginHorizontal();
                }

                Material material = decalMaterials[i];
                if (material == null) continue;

                EditorGUILayout.BeginVertical(GUILayout.Width(previewSize), GUILayout.Height(previewSize + 20));

                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
            
                if (textureCache.TryGetValue(material, out Texture2D baseColorTexture))
                {
                    EditorGUI.DrawTextureTransparent(previewRect, baseColorTexture, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.8f, 0.8f, 0.8f));
                    GUI.Label(previewRect, "No Texture", EditorStyles.centeredGreyMiniLabel);
                }

                EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.y, previewRect.width, 1), Color.gray);
                EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.yMax - 1, previewRect.width, 1), Color.gray);
                EditorGUI.DrawRect(new Rect(previewRect.x, previewRect.y, 1, previewRect.height), Color.gray);
                EditorGUI.DrawRect(new Rect(previewRect.xMax - 1, previewRect.y, 1, previewRect.height), Color.gray);

                if (Event.current.type == EventType.MouseDown && previewRect.Contains(Event.current.mousePosition))
                {
                    selectedMaterial = material;
                    isPlacingDecal = true;
                    decalSize = Vector3.one;
                    rotationAngle = 0f;
                    zRotationAngle = 0f;
                    projectionDistance = 0f;
                    Event.current.Use();
                    Repaint();
                }

                if (material == selectedMaterial)
                {
                    Color highlightColor = new Color(0.8f, 0.8f, 1f, 0.5f);
                    EditorGUI.DrawRect(previewRect, highlightColor);
                }

                string displayName = material.name;
                if (textureCache.TryGetValue(material, out Texture2D tex))
                {
                    displayName += $"\n{tex.width}x{tex.height}";
                }
                EditorGUILayout.LabelField(displayName, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(previewSize));

                EditorGUILayout.EndVertical();
            }

            if (decalMaterials.Count > 0)
            {
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isPlacingDecal || selectedMaterial == null)
            {
                HidePreviewDecal();
                return;
            }

            Event e = Event.current;
        
        if (e.type == EventType.ScrollWheel)
    {
        HandleScaling(e, sceneView);
        if (e.type == EventType.Used)
        {
            return; // Exit if the scaling handled the event
        }
    }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Q:
                        projectionDistance -= PROJECTION_SPEED;
                        e.Use();
                        sceneView.Repaint();
                        break;
                    case KeyCode.E:
                        projectionDistance += PROJECTION_SPEED;
                        e.Use();
                        sceneView.Repaint();
                        break;
                    case KeyCode.R:
                        useRandomVariations = !useRandomVariations;
                        e.Use();
                        Repaint();
                        break;
                }
            }

            if (e.type == EventType.ScrollWheel)
            {
                HandleScaling(e, sceneView);
            }

            // Handle Y rotation with Shift + Left Mouse Button
            if (e.shift && !e.alt && e.type == EventType.MouseDown && e.button == 0)
            {
                isRotating = true;
                isZRotating = false;
                lastMousePosition = e.mousePosition;
                e.Use();
            }
            // Handle Z rotation with Alt + Left Mouse Button
            else if (e.alt && !e.shift && e.type == EventType.MouseDown && e.button == 0)
            {
                isZRotating = true;
                isRotating = false;
                lastMousePosition = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                isRotating = false;
                isZRotating = false;
            }

            if (isRotating && e.type == EventType.MouseDrag)
            {
                float delta = (e.mousePosition - lastMousePosition).x;
                rotationAngle += delta * ROTATION_SPEED * 0.1f;
                lastMousePosition = e.mousePosition;
                e.Use();
                sceneView.Repaint();
            }
            else if (isZRotating && e.type == EventType.MouseDrag)
            {
                float delta = (e.mousePosition - lastMousePosition).x;
                zRotationAngle += delta * ROTATION_SPEED * 0.1f;
                lastMousePosition = e.mousePosition;
                e.Use();
                sceneView.Repaint();
            }

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                UpdatePreviewDecal(hit.point, hit.normal);

                if (e.type == EventType.MouseDown && e.button == 0 && !e.shift && !e.alt)
                {
                    if (useRandomVariations)
                    {
                        PlaceRandomizedDecal(hit.point, hit.normal);
                    }
                    else
                    {
                        PlaceDecal(hit.point, hit.normal);
                    }
                    e.Use();
                }
            }
            else
            {
                HidePreviewDecal();
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                isPlacingDecal = false;
                selectedMaterial = null;
                HidePreviewDecal();
                Repaint();
            }

            if (e.type == EventType.Layout)
            {
                HandleUtility.Repaint();
            }
        }

    private void HandleScaling(Event e, SceneView sceneView)
    {
        if (e.type != EventType.ScrollWheel) return;

        // Determine scroll direction
        float direction = Mathf.Sign(e.delta.y); // -1 for up, +1 for down

        // Scale factor calculation
        float scaleFactor = direction < 0 ? (1f + SCALE_SPEED) : (1f - SCALE_SPEED);

        // Width scaling with Ctrl + Alt
        if (e.control && e.alt && !e.shift)
        {
            decalSize.x *= scaleFactor;
            decalSize.x = Mathf.Max(0.01f, decalSize.x); // Prevent negative or zero size
            e.Use();
            sceneView.Repaint();
            return;
        }

        // Height scaling with Alt only
        if (e.alt && !e.control && !e.shift)
        {
            decalSize.y *= scaleFactor;
            decalSize.y = Mathf.Max(0.01f, decalSize.y);
            e.Use();
            sceneView.Repaint();
            return;
        }

        // Both axes scaling with Ctrl only
        if (e.control && !e.alt && !e.shift)
        {
            decalSize.x *= scaleFactor;
            decalSize.y *= scaleFactor;
            decalSize.x = Mathf.Max(0.01f, decalSize.x);
            decalSize.y = Mathf.Max(0.01f, decalSize.y);
            e.Use();
            sceneView.Repaint();
            return;
        }
    }



        private void PlaceRandomizedDecal(Vector3 position, Vector3 normal)
        {
            // Calculate random variations
            float randomScale = Random.Range(randomScaleRange.x, randomScaleRange.y);
            float randomYRot = Random.Range(randomYRotationRange.x, randomYRotationRange.y);
            float randomZRot = Random.Range(randomZRotationRange.x, randomZRotationRange.y);

            GameObject decalObject = new GameObject($"Decal_{selectedMaterial.name}");
            DecalProjector decalProjector = decalObject.AddComponent<DecalProjector>();
        
            // Apply projection distance
            Vector3 projectedPosition = position + normal * projectionDistance;
            decalObject.transform.position = projectedPosition;
        
            // Apply random rotations
            Quaternion yRotation = Quaternion.Euler(0, rotationAngle + randomYRot, 0);
            Quaternion zRotation = Quaternion.Euler(0, 0, zRotationAngle + randomZRot);
            decalObject.transform.rotation = zRotation * yRotation * Quaternion.LookRotation(-normal);
        
            // Apply random scale
            decalProjector.material = selectedMaterial;
            decalProjector.size = Vector3.Scale(decalSize, new Vector3(randomScale, randomScale, randomScale));
            decalProjector.fadeFactor = 1f;
            decalProjector.scaleMode = DecalScaleMode.InheritFromHierarchy;
        
            // Register undo
            Undo.RegisterCreatedObjectUndo(decalObject, "Place Random Decal");
        }

        private void PlaceDecal(Vector3 position, Vector3 normal)
        {
            GameObject decalObject = new GameObject($"Decal_{selectedMaterial.name}");
            DecalProjector decalProjector = decalObject.AddComponent<DecalProjector>();
        
            // Apply projection distance
            Vector3 projectedPosition = position + normal * projectionDistance;
            decalObject.transform.position = projectedPosition;
        
            // Apply rotations
            Quaternion yRotation = Quaternion.Euler(0, rotationAngle, 0);
            Quaternion zRotation = Quaternion.Euler(0, 0, zRotationAngle);
            decalObject.transform.rotation = zRotation * yRotation * Quaternion.LookRotation(-normal);
        
            // Set decal properties
            decalProjector.material = selectedMaterial;
            decalProjector.size = decalSize;
            decalProjector.fadeFactor = 1f;
            decalProjector.scaleMode = DecalScaleMode.InheritFromHierarchy;
        
            // Register undo
            Undo.RegisterCreatedObjectUndo(decalObject, "Place Decal");
        }
    }
}
#endif