using MotoSquid.Rider;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MotoSquid.World
{
    public class DecalMaterialCreator : EditorWindow
    {
        private string baseTexturePath = "Assets/Textures/Decals";
        private string outputPath = "Assets/Materials/Decals";
        private bool createFolderStructure = true;
        private bool overwriteExisting = false;

        // Texture type identifiers
        private Dictionary<string, string> textureTypes = new Dictionary<string, string>
        {
            { "BaseColor", "_BaseColorMap" },
            { "Normal", "_NormalMap" },
            { "MaskMap", "_MaskMap" },
            { "Metallic", "_MetallicGlossMap" }
        };

        [MenuItem("Tools/HDRP/Decal Material Creator")]
        public static void ShowWindow()
        {
            GetWindow<DecalMaterialCreator>("Decal Material Creator");
        }

        private void OnGUI()
        {
            GUILayout.Label("HDRP Decal Material Creator", EditorStyles.boldLabel);

            baseTexturePath = EditorGUILayout.TextField("Texture Folder Path", baseTexturePath);
            outputPath = EditorGUILayout.TextField("Output Folder Path", outputPath);
            createFolderStructure = EditorGUILayout.Toggle("Create Folder Structure", createFolderStructure);
            overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", overwriteExisting);

            if (GUILayout.Button("Create Decal Materials"))
            {
                CreateDecalMaterials();
            }
        }

        private void CreateDecalMaterials()
        {
            // Ensure the output directory exists
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Get all texture files in the base path and its subdirectories
            string[] textureFiles = Directory.GetFiles(baseTexturePath, "*.png", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(baseTexturePath, "*.tga", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles(baseTexturePath, "*.jpg", SearchOption.AllDirectories))
                .ToArray();

            // Group textures by base name
            var textureGroups = GroupTexturesByBaseName(textureFiles);

            int materialsCreated = 0;
            foreach (var group in textureGroups)
            {
                CreateDecalMaterial(group.Key, group.Value);
                materialsCreated++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"Created {materialsCreated} decal materials.");
        }

        private Dictionary<string, Dictionary<string, string>> GroupTexturesByBaseName(string[] textureFiles)
        {
            var groups = new Dictionary<string, Dictionary<string, string>>();

            foreach (string texturePath in textureFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(texturePath);
                string baseName = "";
                string textureType = "";

                // Try to identify the texture type and extract the base name
                if (IdentifyTextureType(fileName, out baseName, out textureType))
                {
                    if (!groups.ContainsKey(baseName))
                    {
                        groups[baseName] = new Dictionary<string, string>();
                    }

                    if (textureType != null)
                    {
                        groups[baseName][textureType] = texturePath;
                    }
                }
            }

            return groups;
        }

        private bool IdentifyTextureType(string fileName, out string baseName, out string textureType)
        {
            baseName = fileName;
            textureType = null;

            // Check each texture type suffix
            foreach (var type in textureTypes.Keys)
            {
                // Look for patterns like "Name_BaseColor" or "Name_Normal"
                if (fileName.EndsWith($"_{type}"))
                {
                    baseName = fileName.Substring(0, fileName.Length - (type.Length + 1)); // +1 for underscore
                    textureType = type;
                    return true;
                }
            }

            // If no specific type was found, assume it's a base color texture
            textureType = "BaseColor";
            return true;
        }

        private void CreateDecalMaterial(string baseName, Dictionary<string, string> texturePaths)
        {
            string materialPath = Path.Combine(outputPath, $"{baseName}_Decal.mat");

            // Check if material already exists
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null && !overwriteExisting)
            {
                Debug.Log($"Material already exists: {materialPath}");
                return;
            }

            // Create new material if it doesn't exist or overwrite is enabled
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Decal"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            // Set default global opacity to 1
            material.SetFloat("_DecalBlend", 1.0f);

            // Disable all "Affect" properties by default
            material.SetInt("_AffectAlbedo", 0);
            material.SetInt("_AffectNormal", 0);
            material.SetInt("_AffectMetal", 0);
            material.SetInt("_AffectAO", 0);
            material.SetInt("_AffectSmoothness", 0);
            material.SetInt("_AffectEmission", 0);

            // Process each texture type and enable appropriate "Affect" properties
            foreach (var texturePath in texturePaths)
            {
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath.Value);
            
                switch (texturePath.Key)
                {
                    case "BaseColor":
                        material.SetTexture("_BaseColorMap", texture);
                        material.SetInt("_AffectAlbedo", 1);
                        break;
                    
                    case "Normal":
                        material.SetTexture("_NormalMap", texture);
                        material.SetInt("_AffectNormal", 1);
                        material.EnableKeyword("_NORMAL_MAP");
                        break;
                    
                    case "MaskMap":
                        material.SetTexture("_MaskMap", texture);
                        material.SetInt("_AffectMetal", 1);
                        material.SetInt("_AffectAO", 1);
                        material.SetInt("_AffectSmoothness", 1);
                        material.EnableKeyword("_MASKMAP");
                        break;
                    
                    case "Metallic":
                        material.SetTexture("_MetallicGlossMap", texture);
                        material.SetInt("_AffectMetal", 1);
                        material.EnableKeyword("_METALLICGLOSSMAP");
                        break;
                }
            }

            Debug.Log($"Created/Updated decal material: {baseName}_Decal with {texturePaths.Count} textures");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}