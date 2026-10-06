using MotoSquid.Characters;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace MotoSquid.Bike
{
    public static class BikeRoster
    {
        public const string PrefabDir = "Assets/Prefabs";

        static readonly (string rel, bool player)[] Legacy =
        {
            ("Player1", true), ("Player2", true),
            ("Bliss", false), ("Mace", false), ("Slick", false),
            ("Soul", false),  ("Torq", false), ("Voodoo", false),
            ("BikeAI", false),                       
            ("Characters/Voodoo_R_Player", true),
            ("Characters/Voodoo_R_AI",     false),
        };

        static readonly (string folder, bool player)[] BuiltFolders =
        {
            ($"{PrefabDir}/Characters/Player", true),
            ($"{PrefabDir}/Characters/AI",     false),
        };

        public static (string rel, bool player)[] All
        {
            get
            {
                var list = new List<(string, bool)>();
                var seen = new HashSet<string>();

                foreach (var (rel, player) in Legacy)
                    if (seen.Add(PathOf(rel))) list.Add((PathOf(rel), player));

                foreach (var (folder, player) in BuiltFolders)
                {
                    if (!AssetDatabase.IsValidFolder(folder)) continue;
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (seen.Add(path)) list.Add((path, player));
                    }
                }

                foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(CharacterDefinition)}"))
                {
                    var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (def == null) continue;
                    Add(list, seen, def.builtPlayerPrefab, true);
                    Add(list, seen, def.builtAIPrefab, false);
                }
                return list.ToArray();
            }
        }

        static void Add(List<(string, bool)> list, HashSet<string> seen, UnityEngine.GameObject prefab, bool player)
        {
            if (prefab == null) return;
            var path = AssetDatabase.GetAssetPath(prefab);
            if (!string.IsNullOrEmpty(path) && seen.Add(path)) list.Add((path, player));
        }

        public static string PathOf(string relOrPath) =>
            relOrPath.StartsWith("Assets/") ? relOrPath : $"{PrefabDir}/{relOrPath}.prefab";

        public static string NameOf(string relOrPath) =>
            System.IO.Path.GetFileNameWithoutExtension(relOrPath);
    }
}
