using MotoSquid.AI;
using MotoSquid.Bike;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotoSquid.Performance
{
    public class GPUInstancingHelper : EditorWindow
    {
        // Filter
        private string nameFilter = "Building";
        private bool useNameFilter = true;
        private string tagFilter = "";
        private bool useTagFilter;
        private bool includeChildren = true;

        // Root objects (matched by name/tag)
        private List<GameObject> rootObjects = new();
        private List<GameObject> rootsWithoutBatchingStatic = new();   // Roots missing the flag (can restore)
        private List<GameObject> rootsWithBatchingStatic = new();      // Roots that have it (can remove)

        // Child renderers (actual geometry)
        private List<MeshRenderer> renderers = new();
        private List<Material> noInstancing = new();
        private HashSet<int> skinnedMatIds = new(); // Materials shared with SkinnedMeshRenderers must not enable instancing
        private List<(string label, List<MeshRenderer> group)> batchGroups = new();
        private List<MeshRenderer> childrenWithBatchingStatic = new(); // Children blocking instancing
        private List<MeshRenderer> notShadowCaster = new();

        private bool scanned;

        // UI state
        private int tab;
        private Vector2 scroll;
        private static readonly string[] Tabs = { "Materials", "Batch Groups", "Issues" };

        [MenuItem("Tools/GPU Instancing Helper")]
        public static void Open() => GetWindow<GPUInstancingHelper>("GPU Instancing Helper");

        private void OnGUI()
        {
            DrawFilters();
            EditorGUILayout.Space(2);

            if (GUILayout.Button("Scan Scene", GUILayout.Height(28)))
                Scan();

            if (!scanned) return;

            EditorGUILayout.Space(4);
            DrawStats();
            EditorGUILayout.Space(4);

            tab = GUILayout.Toolbar(tab, Tabs);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawMaterialsTab(); break;
                case 1: DrawGroupsTab(); break;
                case 2: DrawIssuesTab(); break;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            DrawActions();
        }

        // Filter UI
        private void DrawFilters()
        {
            EditorGUILayout.LabelField("Filter Objects", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                useNameFilter = EditorGUILayout.Toggle(useNameFilter, GUILayout.Width(14));
                EditorGUI.BeginDisabledGroup(!useNameFilter);
                EditorGUILayout.LabelField("Name contains:", GUILayout.Width(105));
                nameFilter = EditorGUILayout.TextField(nameFilter);
                EditorGUI.EndDisabledGroup();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                useTagFilter = EditorGUILayout.Toggle(useTagFilter, GUILayout.Width(14));
                EditorGUI.BeginDisabledGroup(!useTagFilter);
                EditorGUILayout.LabelField("Tag:", GUILayout.Width(105));
                tagFilter = EditorGUILayout.TagField(tagFilter);
                EditorGUI.EndDisabledGroup();
            }

            includeChildren = EditorGUILayout.ToggleLeft(
                "Scan child renderers (finds geometry on LOD/mesh child objects)", includeChildren);
        }

        // Scan
        private void Scan()
        {
            rootObjects.Clear();
            rootsWithoutBatchingStatic.Clear();
            rootsWithBatchingStatic.Clear();
            renderers.Clear();
            noInstancing.Clear();
            skinnedMatIds.Clear();
            batchGroups.Clear();
            childrenWithBatchingStatic.Clear();
            notShadowCaster.Clear();

            var seenRoots = new HashSet<int>();
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (useNameFilter && !string.IsNullOrEmpty(nameFilter) &&
                    go.name.IndexOf(nameFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (useTagFilter && !string.IsNullOrEmpty(tagFilter) && go.tag != tagFilter)
                    continue;

                if (!seenRoots.Add(go.GetInstanceID())) continue;
                if (IsInBikeHierarchy(go.transform)) continue;
                rootObjects.Add(go);

                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                    rootsWithBatchingStatic.Add(go);
                else
                    rootsWithoutBatchingStatic.Add(go);
            }

            var seenRenderers = new HashSet<int>();
            foreach (var root in rootObjects)
            {
                var toAdd = includeChildren
                    ? root.GetComponentsInChildren<MeshRenderer>(includeInactive: false)
                    : root.GetComponents<MeshRenderer>();

                foreach (var r in toAdd)
                {
                    if (!seenRenderers.Add(r.GetInstanceID())) continue;
                    if (IsInBikeHierarchy(r.transform)) continue;
                    renderers.Add(r);
                }
            }

            foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var mat in smr.sharedMaterials)
                    if (mat != null) skinnedMatIds.Add(mat.GetInstanceID());

            var seenMats = new HashSet<int>();
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || !seenMats.Add(mat.GetInstanceID())) continue;
                    if (!mat.enableInstancing && !skinnedMatIds.Contains(mat.GetInstanceID()))
                        noInstancing.Add(mat);
                }
            }

            var groupMap = new Dictionary<string, List<MeshRenderer>>();
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var matKey = string.Join("|", r.sharedMaterials
                    .Select(m => m != null ? m.GetInstanceID().ToString() : "null"));
                var key = $"{mf.sharedMesh.GetInstanceID()}::{matKey}";

                if (!groupMap.TryGetValue(key, out var list))
                {
                    var meshName = mf.sharedMesh.name;
                    var matNames = string.Join(", ", r.sharedMaterials
                        .Where(m => m != null).Select(m => m.name));
                    groupMap[key] = list = new List<MeshRenderer>();
                    batchGroups.Add(($"{meshName}  [{matNames}]", list));
                }
                list.Add(r);
            }

            batchGroups.Sort((a, b) => b.group.Count.CompareTo(a.group.Count));

            foreach (var r in renderers)
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                    childrenWithBatchingStatic.Add(r);

                if (!r.staticShadowCaster)
                    notShadowCaster.Add(r);
            }

            scanned = true;
        }

        // Stats bar
        private void DrawStats()
        {
            var uniqueMats = new HashSet<int>();
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                    if (m != null) uniqueMats.Add(m.GetInstanceID());

            int groupsOver1 = batchGroups.Count(g => g.group.Count > 1);
            int instanceable = batchGroups.Where(g => g.group.Count > 1).Sum(g => g.group.Count);

            EditorGUILayout.LabelField("Overview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                Stat("Root Objects", rootObjects.Count);
                Stat("Renderers\n(incl. children)", renderers.Count);
                Stat("Unique Materials", uniqueMats.Count);
                Stat("Need Instancing", noInstancing.Count,
                    noInstancing.Count > 0 ? Color.yellow : Color.green);
                Stat("Batch Groups", groupsOver1);
                Stat("Instanceable", instanceable,
                    instanceable > 0 ? Color.green : Color.grey);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Stat("Root Batching Static ON", rootsWithBatchingStatic.Count,
                    rootsWithBatchingStatic.Count > 0 ? Color.green : Color.yellow);
                Stat("Root Batching Static OFF", rootsWithoutBatchingStatic.Count,
                    rootsWithoutBatchingStatic.Count > 0 ? Color.yellow : Color.green);
                Stat("Child Batching Static\n(blocks instancing)", childrenWithBatchingStatic.Count,
                    childrenWithBatchingStatic.Count > 0 ? Color.yellow : Color.green);
            }
        }

        private static void Stat(string label, int value, Color? col = null)
        {
            var prev = GUI.color;
            if (col.HasValue) GUI.color = col.Value;
            GUILayout.Label($"{label}\n{value}", EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            GUI.color = prev;
        }

        // Materials tab
        private void DrawMaterialsTab()
        {
            if (skinnedMatIds.Count > 0)
                EditorGUILayout.HelpBox(
                    $"{skinnedMatIds.Count} material(s) shared with SkinnedMeshRenderers were excluded " +
                    "enabling instancing on these breaks GPU skinning and makes meshes invisible.",
                    MessageType.Info);

            if (noInstancing.Count == 0)
            {
                EditorGUILayout.HelpBox("All eligible materials have GPU Instancing enabled.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                $"{noInstancing.Count} material(s) do not have GPU Instancing enabled.",
                MessageType.Warning);
            EditorGUILayout.Space(2);

            foreach (var mat in noInstancing)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
                    if (GUILayout.Button("Enable", GUILayout.Width(60)))
                        SetInstancing(mat, true);
                }
            }
        }

        // Batch groups tab
        private void DrawGroupsTab()
        {
            var multi = batchGroups.Where(g => g.group.Count > 1).ToList();
            var single = batchGroups.Where(g => g.group.Count == 1).ToList();

            if (!includeChildren)
            {
                EditorGUILayout.HelpBox(
                    "Enable \"Scan child renderers\" in the filter to detect shared meshes on LOD/mesh children.",
                    MessageType.Info);
            }

            if (multi.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No renderers share the same mesh + material combination.\n\n" +
                    "This means GPU instancing cannot batch these objects. " +
                    "Static Batching is the correct approach for buildings with unique meshes.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"{multi.Count} group(s) {multi.Sum(g => g.group.Count)} renderers can be GPU instanced",
                    EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                foreach (var (label, group) in multi)
                    DrawGroupRow(label, group, Color.green);
            }

            if (single.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(
                    $"{single.Count} unique combination(s) cannot be instanced:",
                    EditorStyles.miniLabel);
                foreach (var (label, group) in single)
                    DrawGroupRow(label, group, Color.grey);
            }
        }

        private static void DrawGroupRow(string label, List<MeshRenderer> group, Color col)
        {
            var prev = GUI.color;
            GUI.color = col;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.color = prev;
                EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"× {group.Count}", EditorStyles.boldLabel, GUILayout.Width(42));
                if (GUILayout.Button("Select", GUILayout.Width(55)))
                    Selection.objects = group.Select(r => (Object)r.gameObject).ToArray();
            }
            GUI.color = prev;
        }

        // Issues tab
        private void DrawIssuesTab()
        {
            // Root Batching Static restore
            EditorGUILayout.LabelField("Root Objects Static Batching", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Static Batching is correct for building roots with unique meshes it merges them into " +
                "combined draw calls at runtime. Roots should have Batching Static ON.\n\n" +
                $"ON: {rootsWithBatchingStatic.Count}    OFF (needs restore): {rootsWithoutBatchingStatic.Count}",
                rootsWithoutBatchingStatic.Count > 0 ? MessageType.Warning : MessageType.Info);

            if (rootsWithoutBatchingStatic.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Missing"))
                        Selection.objects = rootsWithoutBatchingStatic.Cast<Object>().ToArray();
                    if (GUILayout.Button($"Restore Batching Static ({rootsWithoutBatchingStatic.Count})"))
                        RestoreBatchingStatic();
                }
            }

            EditorGUILayout.Space(8);

            // Child Batching Static
            EditorGUILayout.LabelField("Child Renderers GPU Instancing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Child renderers that share a mesh+material should NOT have Batching Static, " +
                "so GPU instancing can batch them instead.\n\n" +
                $"Child renderers with Batching Static: {childrenWithBatchingStatic.Count}",
                childrenWithBatchingStatic.Count > 0 ? MessageType.Warning : MessageType.Info);

            if (childrenWithBatchingStatic.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All"))
                        Selection.objects = childrenWithBatchingStatic.Select(r => (Object)r.gameObject).ToArray();
                    if (GUILayout.Button($"Remove from Children ({childrenWithBatchingStatic.Count})"))
                        RemoveChildBatchingStatic();
                }
            }

            EditorGUILayout.Space(8);

            // Static Shadow Caster
            EditorGUILayout.LabelField("Shadow Caster Caching", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"{notShadowCaster.Count} renderer(s) do not have Static Shadow Caster enabled. " +
                "This allows HDRP to cache shadows into the atlas instead of recomputing each frame.",
                notShadowCaster.Count > 0 ? MessageType.Warning : MessageType.Info);

            if (notShadowCaster.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All"))
                        Selection.objects = notShadowCaster.Select(r => (Object)r.gameObject).ToArray();
                    if (GUILayout.Button($"Enable Static Shadow Caster ({notShadowCaster.Count})"))
                        EnableStaticShadowCasters();
                }
            }
        }

        // Actions bar
        private void DrawActions()
        {
            EditorGUILayout.LabelField("Bulk Actions", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(noInstancing.Count == 0);
            if (GUILayout.Button($"Enable GPU Instancing on All {noInstancing.Count} Material(s)"))
                EnableAllInstancing();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(rootsWithoutBatchingStatic.Count == 0);
            if (GUILayout.Button($"Restore Batching Static on {rootsWithoutBatchingStatic.Count} Root(s)"))
                RestoreBatchingStatic();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(childrenWithBatchingStatic.Count == 0);
            if (GUILayout.Button($"Remove Batching Static from {childrenWithBatchingStatic.Count} Child Renderer(s)"))
                RemoveChildBatchingStatic();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(notShadowCaster.Count == 0);
            if (GUILayout.Button($"Enable Static Shadow Caster on {notShadowCaster.Count} Renderer(s)"))
                EnableStaticShadowCasters();
            EditorGUI.EndDisabledGroup();
        }

        // Helpers
        private static void SetInstancing(Material mat, bool enabled)
        {
            Undo.RecordObject(mat, "Set GPU Instancing");
            mat.enableInstancing = enabled;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
        }

        private void EnableAllInstancing()
        {
            foreach (var mat in noInstancing)
                SetInstancing(mat, true);
            noInstancing.Clear();
        }

        private void RestoreBatchingStatic()
        {
            Undo.RecordObjects(rootsWithoutBatchingStatic.Cast<Object>().ToArray(), "Restore Batching Static");
            foreach (var go in rootsWithoutBatchingStatic)
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.BatchingStatic);
                rootsWithBatchingStatic.Add(go);
            }
            rootsWithoutBatchingStatic.Clear();
        }

        private void RemoveChildBatchingStatic()
        {
            Undo.RecordObjects(childrenWithBatchingStatic.Select(r => (Object)r.gameObject).ToArray(),
                "Remove Child Batching Static");
            foreach (var r in childrenWithBatchingStatic)
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                    flags & ~StaticEditorFlags.BatchingStatic);
            }
            childrenWithBatchingStatic.Clear();
        }

        private void EnableStaticShadowCasters()
        {
            Undo.RecordObjects(notShadowCaster.Select(r => (Object)r).ToArray(),
                "Enable Static Shadow Caster");
            foreach (var r in notShadowCaster)
            {
                r.staticShadowCaster = true;
                EditorUtility.SetDirty(r);
            }
            notShadowCaster.Clear();
        }

        static bool IsInBikeHierarchy(Transform t)
        {
            Transform current = t;
            while (current != null)
            {
                if (current.GetComponent<BikeController>() != null) return true;
                if (current.GetComponent<BikeAILogic>()    != null) return true;
                current = current.parent;
            }
            return false;
        }
    }
}
#endif
