using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NGS.MagicLOD.Editors.UI
{
    internal static class MagicLODUIAssetLoader
    {
        private static readonly HashSet<string> ReportedFallbacks = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, string> AnchorScriptPaths = new Dictionary<Type, string>();


        public static T Load<T>(Type anchorType, string guid, string relativePath, string displayName, out string error)
            where T : UnityEngine.Object
        {
            List<string> attempts = new List<string>();
            string assetType = typeof(T).Name;
            string safeDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? assetType
                : displayName;

            if (!string.IsNullOrWhiteSpace(guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(guidPath))
                {
                    attempts.Add($"GUID '{guid}' did not resolve to an asset.");
                }
                else
                {
                    T guidAsset = AssetDatabase.LoadAssetAtPath<T>(guidPath);

                    if (guidAsset != null)
                    {
                        error = null;
                        return guidAsset;
                    }

                    attempts.Add(
                        $"GUID '{guid}' resolved to '{guidPath}', but it is not a loadable {assetType}.");
                }
            }
            else
            {
                attempts.Add("No GUID was provided.");
            }

            string relativeAssetPath = GetRelativeAssetPath(anchorType, relativePath, out string relativeError);

            if (!string.IsNullOrEmpty(relativeAssetPath))
            {
                T relativeAsset = AssetDatabase.LoadAssetAtPath<T>(relativeAssetPath);

                if (relativeAsset != null)
                {
                    ReportFallbackOnce<T>(safeDisplayName, guid, "script-relative path", relativeAssetPath);
                    error = null;
                    return relativeAsset;
                }

                attempts.Add(
                    $"Relative path '{relativeAssetPath}' did not contain a loadable {assetType}.");
            }
            else
            {
                attempts.Add(relativeError);
            }

            T searchedAsset = FindUniqueAsset<T>(relativePath, out string searchedPath, out string searchError);

            if (searchedAsset != null)
            {
                ReportFallbackOnce<T>(safeDisplayName, guid, "unique project search", searchedPath);
                error = null;
                return searchedAsset;
            }

            attempts.Add(searchError);
            error = BuildError(safeDisplayName, assetType, relativePath, attempts);
            return null;
        }


        private static string GetRelativeAssetPath(Type anchorType, string relativePath, out string error)
        {
            error = null;

            if (anchorType == null)
            {
                error = "The owner type is unavailable, so a script-relative path could not be resolved.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                error = "No relative path was provided.";
                return null;
            }

            string scriptPath = FindAnchorScriptPath(anchorType, out string scriptError);
            string scriptDirectory = string.IsNullOrEmpty(scriptPath)
                ? null
                : Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');

            if (string.IsNullOrEmpty(scriptDirectory))
            {
                error = scriptError;
                return null;
            }

            string combinedPath = NormalizeAssetPath($"{scriptDirectory}/{relativePath}");

            if (!string.IsNullOrEmpty(combinedPath))
                return combinedPath;

            error = $"Relative path '{relativePath}' escapes the Unity asset root.";
            return null;
        }

        private static string FindAnchorScriptPath(Type anchorType, out string error)
        {
            error = null;

            if (AnchorScriptPaths.TryGetValue(anchorType, out string cachedPath))
            {
                MonoScript cachedScript = AssetDatabase.LoadAssetAtPath<MonoScript>(cachedPath);

                if (cachedScript != null && cachedScript.GetClass() == anchorType)
                    return cachedPath;

                AnchorScriptPaths.Remove(anchorType);
            }

            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            List<string> matchingPaths = new List<string>();

            foreach (string scriptGuid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

                if (script != null && script.GetClass() == anchorType)
                    matchingPaths.Add(scriptPath);
            }

            if (matchingPaths.Count == 1)
            {
                AnchorScriptPaths[anchorType] = matchingPaths[0];
                return matchingPaths[0];
            }

            if (matchingPaths.Count == 0)
            {
                error = $"The MonoScript for '{anchorType.FullName}' could not be found.";
                return null;
            }

            error =
                $"Multiple MonoScripts matched '{anchorType.FullName}': " +
                string.Join(", ", matchingPaths);
            return null;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string[] sourceParts = path.Replace('\\', '/').Split('/');
            List<string> normalizedParts = new List<string>(sourceParts.Length);

            foreach (string sourcePart in sourceParts)
            {
                if (string.IsNullOrEmpty(sourcePart) || sourcePart == ".")
                    continue;

                if (sourcePart == "..")
                {
                    if (normalizedParts.Count <= 1)
                        return null;

                    normalizedParts.RemoveAt(normalizedParts.Count - 1);
                    continue;
                }

                normalizedParts.Add(sourcePart);
            }

            if (normalizedParts.Count == 0)
                return null;

            string root = normalizedParts[0];

            if (!string.Equals(root, "Assets", StringComparison.Ordinal) &&
                !string.Equals(root, "Packages", StringComparison.Ordinal))
            {
                return null;
            }

            return string.Join("/", normalizedParts);
        }

        private static T FindUniqueAsset<T>(string relativePath, out string assetPath, out string error)
            where T : UnityEngine.Object
        {
            assetPath = null;
            error = null;

            string fileName = Path.GetFileName(relativePath);

            if (string.IsNullOrEmpty(fileName))
            {
                error = "Project search could not run because the expected file name is empty.";
                return null;
            }

            string searchName = Path.GetFileNameWithoutExtension(fileName);
            string[] foundGuids = AssetDatabase.FindAssets($"{searchName} t:{typeof(T).Name}");
            List<string> exactPaths = new List<string>();
            T uniqueAsset = null;

            foreach (string foundGuid in foundGuids)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(foundGuid);

                if (!string.Equals(
                        Path.GetFileName(foundPath),
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                T candidate = AssetDatabase.LoadAssetAtPath<T>(foundPath);

                if (candidate == null)
                    continue;

                exactPaths.Add(foundPath);
                uniqueAsset = candidate;
            }

            if (exactPaths.Count == 1)
            {
                assetPath = exactPaths[0];
                return uniqueAsset;
            }

            if (exactPaths.Count == 0)
            {
                error =
                    $"Unique project search found no {typeof(T).Name} named '{fileName}'.";
                return null;
            }

            error =
                $"Unique project search was ambiguous for '{fileName}': " +
                string.Join(", ", exactPaths);
            return null;
        }

        private static string BuildError(string displayName, string assetType, string relativePath, List<string> attempts)
        {
            StringBuilder message = new StringBuilder();
            message.Append("MagicLOD could not load ")
                .Append(displayName)
                .Append(" (")
                .Append(assetType)
                .AppendLine(").");
            message.Append("Expected relative location: '")
                .Append(relativePath)
                .AppendLine("'.");
            message.AppendLine("Attempted resolution:");

            foreach (string attempt in attempts)
                message.Append("• ").AppendLine(attempt);

            return message.ToString().TrimEnd();
        }

        private static void ReportFallbackOnce<T>(string displayName, string guid, string fallback, string assetPath)
            where T : UnityEngine.Object
        {
            string key = $"{typeof(T).FullName}|{displayName}|{assetPath}";

            if (!ReportedFallbacks.Add(key))
                return;

            Debug.LogWarning(
                $"MagicLOD loaded {displayName} through the {fallback} fallback at '{assetPath}'. " +
                $"The configured GUID '{guid}' did not provide a usable {typeof(T).Name}. " +
                "Keep the asset's .meta file intact to restore GUID-based loading.");
        }
    }
}
