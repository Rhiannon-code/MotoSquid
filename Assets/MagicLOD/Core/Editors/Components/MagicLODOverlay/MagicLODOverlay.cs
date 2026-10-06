#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NGS.MagicLOD.Runtime;
using NGS.MagicLOD.Editors.API;

namespace NGS.MagicLOD.Editors.Components
{
    public enum SelectionFilter
    {
        TrianglesCount, MeshDensity
    }

    public class MagicLODOverlay : MonoBehaviour
    {
        public bool ConsiderPrefabsSelection
        {
            get
            {
                return _selection.ConsiderPrefabs;
            }
            set
            {
                _selection.ConsiderPrefabs = value;
            }
        }

        [field: SerializeField]
        public SettingsModule Settings 
        {
            get; private set;
        }

        [field: SerializeField]
        public StatisticModule Statistic
        {
            get; private set;
        }

        [field: SerializeField]
        public GizmosModule Gizmos
        {
            get; private set;
        }

        [SerializeField]
        private SelectionModule _selection;


        private void Reset()
        {
            hideFlags |= HideFlags.DontSaveInBuild;

            Settings = new SettingsModule();
            Statistic = new StatisticModule();
            Gizmos = new GizmosModule();

            _selection = new SelectionModule();
        }

        private void OnDrawGizmos()
        {
            Gizmos?.OnDrawGizmos();
        }
         

        public static MagicLODOverlay GetInstance()
        {
            MagicLODOverlay overlay = FindFirstObjectByType<MagicLODOverlay>();

            if (overlay != null)
                return overlay;

            return new GameObject("MagicLODOverlay").AddComponent<MagicLODOverlay>();
        }

        public void AddMagicLODToSelectedGameObject()
        {
            _selection.AddMagicLODToSelectedGameObject(Settings);
        }

        public void RemoveMagicLODFromSelectedGameObjects()
        {
            _selection.RemoveMagicLODFromSelectedGameObjects();
        }

        public void AutoFindAndAdd()
        {
            _selection.AutoFindAndAdd(Settings);
        }

        public void ClearAutoAdded()
        {
            _selection.ClearAutoAdded();
        }

        public void AddSelection()
        {
            _selection.AddSelection(Settings);
        }

        public void RemoveSelection()
        {
            _selection.RemoveSelection();
        }

        public void ClearAllPending()
        {
            _selection.ClearAutoAdded();
            _selection.ClearAllPending();
        }


        public void DecimateSelected()
        {
            foreach (var magicLOD in _selection.GetSelectedMagicLODs())
            {
                if (!magicLOD.IsDecimated && !magicLOD.IsDecimationInProgress)
                    magicLOD.DecimateThreaded();
            }
        }

        public void DecimateAll()
        {
            foreach (var magicLOD in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
            {
                if (!magicLOD.IsDecimated && !magicLOD.IsDecimationInProgress)
                    magicLOD.DecimateThreaded();
            }
        }

        public void CancelDecimation()
        {
            EditorMeshDecimationProvider.Cancel();
        }


        public void ReplaceSimplifiedSelected()
        {
            foreach (var magicLOD in _selection.GetSelectedMagicLODs())
            {
                if (!magicLOD.IsDecimated)
                    continue;

                if (!magicLOD.CanApplySimplifiedMesh)
                    continue;

                magicLOD.ApplySimplifiedMesh();
            }
        }

        public void ReplaceSimplifiedAll()
        {
            foreach (var magicLOD in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
            {
                if (!magicLOD.IsDecimated)
                    continue;

                if (!magicLOD.CanApplySimplifiedMesh)
                    continue;

                magicLOD.ApplySimplifiedMesh();
            }
        }


        public void RevertSelected()
        {
            foreach (var magicLOD in _selection.GetSelectedMagicLODs())
            {
                if (magicLOD.IsDecimated)
                    magicLOD.Revert();
            }
        }

        public void RevertAll()
        {
            foreach (var magicLOD in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
            {
                if (magicLOD.IsDecimated)
                    magicLOD.Revert();
            }
        }

        public void RevertSourcesWithErrors()
        {
            foreach (var magicLOD in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
            {
                if (magicLOD.HasErrors)
                    magicLOD.Revert();
            }
        }


        public void OnGlobalSettingsChanged()
        {
            if (!Settings.ApplyToAllPending)
                return;

            IEnumerable<MagicLODEditorComponent> components = FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None)
                .Where(c => !c.IsDecimated && !c.IsDecimationInProgress);

            foreach (var magicLODComponent in components)
            {
                Settings.TryApplyGlobalSettings(magicLODComponent);
            }
        }



        [Serializable]
        public class SettingsModule
        {
            [field: SerializeField]
            public bool ApplyToAllPending
            {
                get; set;
            }

            [field: SerializeField]
            public OperationMode Mode
            {
                get; set;
            }

            [field: SerializeField]
            public DecimationSettings GlobalSettings
            {
                get; set;
            }

            [field: SerializeField]
            public ScreenSpaceLODConfigurator ScreenSpaceConfigurator
            {
                get; private set;
            }

            [field: Min(0f)]
            [field: SerializeField]
            public float MaxSimplificationError
            {
                get; set;
            }

            [field: SerializeField]
            public bool ExportMeshes
            {
                get; set;
            }

            [field: SerializeField]
            public string ExportFolder
            {
                get; set;
            }


            public SettingsModule()
            {
                Mode = OperationMode.GenerateLODs;
                GlobalSettings = DecimationSettings.Default;
                ScreenSpaceConfigurator = new ScreenSpaceLODConfigurator();
                MaxSimplificationError = 0.0001f;
                ExportMeshes = true;
                ExportFolder = "Assets/MagicLOD_Export/";
            }

            public bool TryApplyDefaultSettings(MagicLODEditorComponent target)
            {
                foreach (var other in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
                {
                    if (other == target)
                        continue;

                    if (other.IsSameSyncGroup(target))
                        return false;
                }

                target.Options.StopSyncing = true;

                target.Options.DecimationSettings = GlobalSettings;
                target.Options.MaxSimplificationError = MaxSimplificationError;
                target.Options.LODGroupConfigurator = ScreenSpaceConfigurator.Clone();
                target.Options.ExportMeshes = ExportMeshes;
                target.Options.ExportFolder = ExportFolder;

                target.Options.StopSyncing = false;

                target.OnDecimationOptionsChanged();

                return true;
            }

            public bool TryApplyGlobalSettings(MagicLODEditorComponent target)
            {
                if (!ApplyToAllPending)
                    return false;

                if (target.IsDecimated || target.IsDecimationInProgress)
                    return false;

                target.Options.StopSyncing = true;

                target.Options.OperationMode = Mode;

                DecimationSettings targetSettings = GlobalSettings;
                targetSettings.preservationVolumes = target.Options.DecimationSettings.preservationVolumes;
                target.Options.DecimationSettings = targetSettings;

                if (Mode == OperationMode.GenerateLODs)
                {
                    if (!target.IsManualLODConfigurator)
                        target.Options.LODGroupConfigurator = ScreenSpaceConfigurator.Clone();
                }
                else
                {
                    target.Options.MaxSimplificationError = MaxSimplificationError;
                }

                target.Options.ExportMeshes = ExportMeshes;
                target.Options.ExportFolder = ExportFolder;

                target.Options.StopSyncing = false;

                return true;
            }
        }

        [Serializable]
        public class SelectionModule
        {
            [field: SerializeField]
            public SelectionFilter Filter
            {
                get; set;
            }

            [field: Min(1)]
            [field: SerializeField]
            public int MinTrianglesCount
            {
                get; set;
            }

            [field: Min(0.1f)]
            [field: SerializeField]
            public float MinTrianglesPerUnit
            {
                get; set;
            }

            [field: SerializeField]
            public bool ConsiderPrefabs
            {
                get; set;
            }

            [SerializeField]
            private List<MagicLODEditorComponent> _lastAutoAdded;


            public SelectionModule()
            {
                Filter = Components.SelectionFilter.TrianglesCount;
                MinTrianglesCount = 20000;
                MinTrianglesPerUnit = 3000;
                ConsiderPrefabs = true;
            }

            public void AddMagicLODToSelectedGameObject(SettingsModule settings)
            {
                GameObject go = Selection.activeGameObject;

                if (go == null)
                    return;
                
                TryAddMagicLODToGO(go, settings, false, false);
            }

            public void RemoveMagicLODFromSelectedGameObjects()
            {
                GameObject go = Selection.activeGameObject;

                if (go == null)
                    return;

                if (go.TryGetComponent(out MagicLODEditorComponent magicLOD))
                {
                    if (magicLOD.IsDecimated)
                    {
                        Debug.Log("MagicLODOverlay::RemoveMagicLODFromSelectedGameObjects() can't remove already baked component. Revert first");
                        return;
                    }

                    DestroyImmediate(magicLOD);
                }
            }

            public void AutoFindAndAdd(SettingsModule settings)
            {
                _lastAutoAdded ??= new List<MagicLODEditorComponent>();
                _lastAutoAdded.Clear();

                IEnumerable<Renderer> renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                    .OrderBy(r => GetHierarchyDepth(r.transform));

                int idx = 0;
                int count = renderers.Count();

                try
                {
                    foreach (var renderer in renderers)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Auto Search...", $"Checked {idx} of {count}", (float)idx / count))
                            break;

                        if (!SelectionFilter(renderer.gameObject, (settings.Mode == OperationMode.Simplify)))
                            continue;

                        if (TryAddMagicLODToGO(renderer.gameObject, settings, true, ConsiderPrefabs, out MagicLODEditorComponent magicLOD))
                        {
                            _lastAutoAdded.Add(magicLOD);
                        }

                        idx++;
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            public void ClearAutoAdded()
            {
                if (_lastAutoAdded == null || _lastAutoAdded.Count == 0)
                    return;

                foreach (var lastAdded in _lastAutoAdded)
                {
                    if (lastAdded == null)
                        continue;

                    TryRemoveMagicLOD(lastAdded);
                }

                _lastAutoAdded.Clear();
            }

            public void AddSelection(SettingsModule settings)
            {
                try
                {
                    IEnumerable<Transform> selectedTransforms = Selection.gameObjects
                        .SelectMany(go => go.GetComponentsInChildren<Transform>());

                    int idx = 0;
                    int count = selectedTransforms.Count();

                    foreach (var transform in selectedTransforms)
                    {
                        if (transform == null)
                            continue;

                        if (EditorUtility.DisplayCancelableProgressBar("Add Selection...", $"Checked {idx} of {count}", (float)idx / count))
                            break;

                        TryAddMagicLODToGO(transform.gameObject, settings, true, ConsiderPrefabs);

                        idx++;
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            public void RemoveSelection()
            {
                IEnumerable<MagicLODEditorComponent> selectedMagicLODs = UnityEditor.Selection.gameObjects
                    .SelectMany((p) => p.GetComponentsInChildren<MagicLODEditorComponent>())
                    .Distinct();

                foreach (var magicLOD in selectedMagicLODs)
                {
                    TryRemoveMagicLOD(magicLOD);
                }
            }

            public void ClearAllPending()
            {
                foreach (var magicLOD in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
                {
                    if (magicLOD.IsDecimated)
                        continue;

                    TryRemoveMagicLOD(magicLOD);
                }
            }

            public IEnumerable<MagicLODEditorComponent> GetSelectedMagicLODs()
            {
                return Selection.gameObjects
                    .SelectMany(go => go.GetComponentsInChildren<MagicLODEditorComponent>())
                   .Distinct();
            }


            private bool TryAddMagicLODToGO(GameObject go, SettingsModule settings, bool checkSources, bool checkPrefabRoot)
            {
                return TryAddMagicLODToGO(go, settings, checkSources, checkPrefabRoot, out _);
            }

            private bool TryAddMagicLODToGO(GameObject go, SettingsModule settings, bool checkSources, bool checkPrefabRoot, out MagicLODEditorComponent magicLOD)
            {
                if (checkSources)
                {
                    bool includeLODs = (settings.Mode == OperationMode.Simplify);

                    if (!MagicLODSourcesUtil.TryGatherSources(go, out _, out _, includeLODs))
                    {
                        magicLOD = null;
                        return false;
                    }
                }

                if (checkPrefabRoot)
                {
                    if (PrefabUtility.IsPartOfAnyPrefab(go))
                    {
                        GameObject parent = PrefabUtility.GetOutermostPrefabInstanceRoot(go);

                        if (parent != null)
                            return TryAddMagicLODToGO(parent, settings, false, false, out magicLOD);
                    }
                }

                foreach (var parentComponent in go.GetComponentsInParent<MagicLODEditorComponent>())
                {
                    if (parentComponent.gameObject == go)
                    {
                        magicLOD = null;
                        return false;
                    }

                    if (parentComponent.Options.IncludeChildren)
                    {
                        Debug.Log("Contains Parent With Include Children");
                        magicLOD = null;
                        return false;
                    }
                }

                foreach (var childComponent in go.GetComponentsInChildren<MagicLODEditorComponent>())
                    TryRemoveMagicLOD(childComponent, false);

                magicLOD = go.AddComponent<MagicLODEditorComponent>();

                if (settings.TryApplyDefaultSettings(magicLOD))
                    magicLOD.Options.OperationMode = settings.Mode;

                return true;
            }

            private bool TryRemoveMagicLOD(MagicLODEditorComponent magicLOD, bool removeBaked = false)
            {
                if (magicLOD.IsDecimated && !removeBaked)
                    return false;

                DestroyImmediate(magicLOD);

                return true;
            }


            private bool SelectionFilter(GameObject go, bool includeLODs)
            {
                if (!MagicLODSourcesUtil.TryGatherSources(go, out Mesh mesh, out Renderer renderer, includeLODs))
                    return false;

                if (Filter == Components.SelectionFilter.TrianglesCount)
                {
                    if (mesh.GetTotalTriangles() <= MinTrianglesCount)
                        return false;
                }
                else if (Filter == Components.SelectionFilter.MeshDensity)
                {
                    Vector3 boundsSize = renderer.bounds.size;

                    float boundsVolume =
                        Mathf.Max(boundsSize.x, 0.00001f) *
                        Mathf.Max(boundsSize.y, 0.00001f) *
                        Mathf.Max(boundsSize.z, 0.00001f);

                    float meshDensity = mesh.GetTotalTriangles() / boundsVolume;

                    if (meshDensity <= MinTrianglesPerUnit)
                        return false;
                }

                return true;
            }

            private static int GetHierarchyDepth(Transform transform)
            {
                int depth = 0;

                while (transform.parent != null)
                {
                    transform = transform.parent;
                    depth++;
                }

                return depth;
            }
        }

        [Serializable]
        public class StatisticModule
        {
            [field: SerializeField]
            public int CreatedLODGroups
            {
                get; private set;
            }

            [field: SerializeField]
            public int CreatedSimplifiedMeshes
            {
                get; private set;
            }

            [field: SerializeField]
            public int PendingLODGeneration
            {
                get; private set;
            }

            [field: SerializeField]
            public int PendingSimplification
            {
                get; private set;
            }

            [field: SerializeField]
            public int SourcesWithErrors
            {
                get; private set;
            }

            [field: SerializeField]
            public int SimplifySourcesTriangles
            {
                get; private set;
            }

            [field: SerializeField]
            public int TotalSimplifiedTriangles
            {
                get; private set;
            }

            [field: SerializeField]
            public int LODSourcesTriangles
            {
                get; private set;
            }

            [field: SerializeField]
            public int TotalLowestLODTriangles
            {
                get; private set;
            }


            public StatisticModule()
            {
                Clear();
            }

            public void Refresh()
            {
                Clear();

                foreach (var magicLOD in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
                {
                    if (magicLOD.HasErrors)
                        SourcesWithErrors++;

                    if (!magicLOD.IsDecimated)
                    {
                        if (magicLOD.Options.OperationMode == OperationMode.Simplify)
                            PendingSimplification++;
                        else
                            PendingLODGeneration++;
                        
                        continue;
                    }

                    if (magicLOD.Options.OperationMode == OperationMode.Simplify)
                    {
                        CreatedSimplifiedMeshes += magicLOD.SuccessfulDecimationRequests;

                        SimplifySourcesTriangles += magicLOD.OriginalTrianglesCount;
                        TotalSimplifiedTriangles += magicLOD.SimplifiedTrianglesCount;
                    }
                    else
                    {
                        CreatedLODGroups++;

                        if (magicLOD.DecimatedTrianglesCount == null || magicLOD.DecimatedTrianglesCount.Length == 0)
                            continue;

                        LODSourcesTriangles += magicLOD.OriginalTrianglesCount;
                        TotalLowestLODTriangles += magicLOD.DecimatedTrianglesCount[magicLOD.DecimatedTrianglesCount.Length - 1];
                    }
                }
            }

            public void Clear()
            {
                CreatedLODGroups = 0;
                CreatedSimplifiedMeshes = 0;
                PendingLODGeneration = 0;
                PendingSimplification = 0;
                SourcesWithErrors = 0;
                SimplifySourcesTriangles = 0;
                TotalSimplifiedTriangles = 0;
                LODSourcesTriangles = 0;
                TotalLowestLODTriangles = 0;
            }
        }

        [Serializable]
        public class GizmosModule
        {
            private static readonly Color PendingColor = Color.white;
            private static readonly Color LODGroupColor = new Color(0.3f, 0.8f, 0.4f, 1f);
            private static readonly Color SimplifiedColor = new Color(1f, 0.7f, 0.2f, 1f);
            private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f, 1f);

            [field: SerializeField]
            public bool DrawPending
            {
                get; set;
            }

            [field: SerializeField]
            public bool DrawStatistic
            {
                get; set;
            }

            private List<Mesh> _meshes;
            private List<Renderer> _renderers;
            private List<CachedBounds> _boundsCache;


            public GizmosModule()
            {
                DrawPending = false;
                DrawStatistic = false;
            }

            public void OnDrawGizmos()
            {
                if (!DrawPending && !DrawStatistic)
                    return;

                if (_boundsCache == null)
                    Refresh();

                Matrix4x4 previousMatrix = Handles.matrix;
                Color previousColor = Handles.color;
                CompareFunction previousZTest = Handles.zTest;

                Handles.matrix = Matrix4x4.identity;

                try
                {
                    for (int i = 0; i < _boundsCache.Count; i++)
                    {
                        CachedBounds cachedBounds = _boundsCache[i];

                        if (cachedBounds.IsDecimated && !DrawStatistic)
                            continue;

                        if (!cachedBounds.IsDecimated && !DrawPending && !DrawStatistic)
                            continue;

                        DrawBounds(cachedBounds.Bounds, cachedBounds.Color);
                    }
                }
                finally
                {
                    Handles.matrix = previousMatrix;
                    Handles.color = previousColor;
                    Handles.zTest = previousZTest;
                }
            }

            public void Refresh()
            {
                _boundsCache ??= new List<CachedBounds>();
                _boundsCache.Clear();

                foreach (var magicLOD in FindObjectsByType<MagicLODEditorComponent>(FindObjectsSortMode.None))
                {
                    if (!TryGetBounds(magicLOD, out Bounds bounds))
                        continue;

                    _boundsCache.Add(new CachedBounds(
                        bounds,
                        GetBoundsColor(magicLOD),
                        magicLOD.IsDecimated));
                }
            }


            private Color GetBoundsColor(MagicLODEditorComponent magicLOD)
            {
                if (magicLOD.HasErrors)
                    return ErrorColor;

                if (!magicLOD.IsDecimated)
                    return PendingColor;

                if (magicLOD.Options.OperationMode == OperationMode.Simplify)
                    return SimplifiedColor;

                return LODGroupColor;
            }

            private bool TryGetBounds(MagicLODEditorComponent magicLOD, out Bounds bounds)
            {
                _meshes ??= new List<Mesh>();
                _renderers ??= new List<Renderer>();

                bool includeChildren = magicLOD.IsDecimated || magicLOD.Options.IncludeChildren;
                bool includeLODs = magicLOD.IsDecimated || (magicLOD.Options.OperationMode == OperationMode.Simplify);
                bool checkMeshIsReadable = false;

                MagicLODSourcesUtil.GatherSources(
                    magicLOD.gameObject,
                    includeChildren,
                    includeLODs,
                    checkMeshIsReadable,
                    ref _meshes,
                    ref _renderers);

                bounds = default;
                bool hasBounds = false;

                for (int i = 0; i < _renderers.Count; i++)
                {
                    Renderer renderer = _renderers[i];

                    if (renderer == null)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                return hasBounds;
            }

            private void DrawBounds(Bounds bounds, Color color)
            {
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;

                Vector3 p0 = center + new Vector3(-extents.x, -extents.y, -extents.z);
                Vector3 p1 = center + new Vector3(-extents.x, -extents.y, extents.z);
                Vector3 p2 = center + new Vector3(extents.x, -extents.y, extents.z);
                Vector3 p3 = center + new Vector3(extents.x, -extents.y, -extents.z);
                Vector3 p4 = center + new Vector3(-extents.x, extents.y, -extents.z);
                Vector3 p5 = center + new Vector3(-extents.x, extents.y, extents.z);
                Vector3 p6 = center + new Vector3(extents.x, extents.y, extents.z);
                Vector3 p7 = center + new Vector3(extents.x, extents.y, -extents.z);

                DrawBoundsFill(color, p0, p1, p2, p3, p4, p5, p6, p7);
                DrawBoundsEdges(color, p0, p1, p2, p3, p4, p5, p6, p7);
            }

            private void DrawBoundsFill(
                Color color,
                Vector3 p0,
                Vector3 p1,
                Vector3 p2,
                Vector3 p3,
                Vector3 p4,
                Vector3 p5,
                Vector3 p6,
                Vector3 p7)
            {
                Color fillColor = color;
                fillColor.a = 0.12f;

                Handles.color = fillColor;
                Handles.zTest = CompareFunction.LessEqual;

                Handles.DrawAAConvexPolygon(p0, p1, p2, p3);
                Handles.DrawAAConvexPolygon(p4, p5, p6, p7);
                Handles.DrawAAConvexPolygon(p0, p1, p5, p4);
                Handles.DrawAAConvexPolygon(p2, p3, p7, p6);
                Handles.DrawAAConvexPolygon(p0, p3, p7, p4);
                Handles.DrawAAConvexPolygon(p1, p2, p6, p5);
            }

            private void DrawBoundsEdges(
                Color color,
                Vector3 p0,
                Vector3 p1,
                Vector3 p2,
                Vector3 p3,
                Vector3 p4,
                Vector3 p5,
                Vector3 p6,
                Vector3 p7)
            {
                Handles.color = color;
                Handles.zTest = CompareFunction.Always;

                Handles.DrawAAPolyLine(2f, p0, p1, p2, p3, p0);
                Handles.DrawAAPolyLine(2f, p4, p5, p6, p7, p4);
                Handles.DrawAAPolyLine(2f, p0, p4);
                Handles.DrawAAPolyLine(2f, p1, p5);
                Handles.DrawAAPolyLine(2f, p2, p6);
                Handles.DrawAAPolyLine(2f, p3, p7);
            }


            private struct CachedBounds
            {
                public Bounds Bounds;
                public Color Color;
                public bool IsDecimated;


                public CachedBounds(Bounds bounds, Color color, bool isDecimated)
                {
                    Bounds = bounds;
                    Color = color;
                    IsDecimated = isDecimated;
                }
            }
        }
    }
}

#endif
