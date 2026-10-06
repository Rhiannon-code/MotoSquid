#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NGS.MagicLOD.Editors.API
{
    public static class AssetsPathUtil
    {
        private static string _applicationDataPath;
        public static char[] _invalidChars;


        static AssetsPathUtil()
        {
            _applicationDataPath = Application.dataPath.Replace('\\', '/');
            _invalidChars = Path.GetInvalidFileNameChars();
        }

        public static bool IsDirectoryExists(string folderPath)
        {
            return Directory.Exists(folderPath);
        }

        public static bool ValidateFolderPath(string path, out string normalizedPath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                normalizedPath = null;
                return false;
            }

            normalizedPath = path.Replace('\\', '/');

            if (normalizedPath.StartsWith(_applicationDataPath, StringComparison.OrdinalIgnoreCase))
                normalizedPath = "Assets" + normalizedPath.Substring(_applicationDataPath.Length);

            if (!normalizedPath.EndsWith("/"))
                normalizedPath += "/";

            if (!normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(normalizedPath, "Assets/", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        public static bool TryCreateFolder(string path)
        {
            if (!ValidateFolderPath(path, out string normalizedPath))
                return false;

            Directory.CreateDirectory(normalizedPath);
            AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.Default);

            return true;
        }

        public static string SanitizeFileName(string name, string fallback = "defaultName")
        {
            if (string.IsNullOrWhiteSpace(name))
                return "UnknownMesh";

            foreach (char c in _invalidChars)
                name = name.Replace(c, '_');

            return name.Replace('/', '_').Replace('\\', '_');
        }
    }
}

#endif
