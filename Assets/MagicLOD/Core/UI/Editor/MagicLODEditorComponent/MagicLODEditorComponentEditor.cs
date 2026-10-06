using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NGS.MagicLOD.Runtime;
using NGS.MagicLOD.Editors.Components;
using NGS.MagicLOD.Editors.API;

namespace NGS.MagicLOD.Editors.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MagicLODEditorComponent))]
    public sealed class MagicLODEditorComponentEditor : Editor
    {
        private const string LayoutGuid = "2b294f6548f38b94086aa3c4ba3403d2";
        private const string StylesGuid = "69624a372953f764abde072ebabb4a8f";
        private const string LayoutFileName = "MagicLODEditorComponentEditor.uxml";
        private const string StylesFileName = "MagicLODEditorComponentEditor.uss";
        private const string HiddenClassName = "is-hidden";
        private const string ActiveModeClassName = "mode-toolbar__item--active";
        private const string NarrowLayoutClassName = "magic-lod--narrow";
        private const string ExtraNarrowLayoutClassName = "magic-lod--extra-narrow";
        private const string ShortLayoutClassName = "magic-lod--short";
        private const int RefreshIntervalMs = 100;
        private const int OptionsChangedDelayMs = 200;
        private const float NarrowLayoutWidth = 320f;
        private const float ExtraNarrowLayoutWidth = 240f;
        private const float ShortLayoutHeight = 360f;

        private VisualElement _root;
        private VisualElement _responsiveContainer;
        private bool _inspectorClosedNotified;
        private bool _isUpdatingControls;

        private CoreSettingsModule _coreSettings;
        private LODSettingsModule _lodSettings;
        private PreservationsModule _preservations;
        private WorkflowModule _workflow;

        private DelayedAction _syncOptionsChanged;
        private DelayedAction _decimationOptionsChanged;
        private DelayedAction _exportOptionsChanged;

        public override VisualElement CreateInspectorGUI()
        {
            _inspectorClosedNotified = false;

            _root = LoadLayout(out string assetError);
            if (_root == null)
                return CreateMissingAssetsMessage(assetError);

            _coreSettings = new CoreSettingsModule(this, _root, serializedObject);
            _lodSettings = new LODSettingsModule(this, _root, serializedObject);
            _preservations = new PreservationsModule(this, _root, serializedObject);
            _workflow = new WorkflowModule(this, _root);

            _syncOptionsChanged = new DelayedAction(_root, NotifyTargetsSyncOptionsChanged);
            _decimationOptionsChanged = new DelayedAction(_root, NotifyTargetsDecimationOptionsChanged);
            _exportOptionsChanged = new DelayedAction(_root, NotifyTargetsExportOptionsChanged);

            _preservations.RefreshList();
            RefreshControls();
            _workflow.Tick(force: true);

            _root.schedule.Execute(() => _workflow.Tick()).Every(RefreshIntervalMs);
            _root.RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            _root.RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            return _root;
        }

        private void OnDisable()
        {
            DetachResponsiveContainer();
            NotifyInspectorClosed();
            _syncOptionsChanged?.Flush();
            _decimationOptionsChanged?.Flush();
            _exportOptionsChanged?.Flush();
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            _inspectorClosedNotified = false;
            VisualElement root = _root;
            IPanel panel = evt.destinationPanel;

            // Parent geometry is not stable until the attach event has finished.
            root?.schedule.Execute(() =>
            {
                if (_root != root || root.panel != panel)
                    return;

                AttachResponsiveContainer();
                RefreshResponsiveLayout(root.layout.width, GetResponsiveHeight(root.layout.height));
            });
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            DetachResponsiveContainer();
            NotifyInspectorClosed();
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshResponsiveLayout(evt.newRect.width, GetResponsiveHeight(evt.newRect.height));
        }

        private void OnResponsiveContainerGeometryChanged(GeometryChangedEvent evt)
        {
            float width = _root == null ? 0f : _root.layout.width;
            RefreshResponsiveLayout(width, evt.newRect.height);
        }

        private void AttachResponsiveContainer()
        {
            DetachResponsiveContainer();
            _responsiveContainer = FindResponsiveContainer();

            if (_responsiveContainer != null && _responsiveContainer != _root)
                _responsiveContainer.RegisterCallback<GeometryChangedEvent>(OnResponsiveContainerGeometryChanged);
        }

        private void DetachResponsiveContainer()
        {
            if (_responsiveContainer != null && _responsiveContainer != _root)
                _responsiveContainer.UnregisterCallback<GeometryChangedEvent>(OnResponsiveContainerGeometryChanged);

            _responsiveContainer = null;
        }

        private VisualElement FindResponsiveContainer()
        {
            VisualElement ancestor = _root?.parent;

            while (ancestor != null)
            {
                if (ancestor.ClassListContains("magic-lod-overlay"))
                    return ancestor;

                ancestor = ancestor.parent;
            }

            return _root?.panel?.visualTree;
        }

        private float GetResponsiveHeight(float fallback)
        {
            float height = _responsiveContainer == null
                ? fallback
                : _responsiveContainer.contentRect.height;

            return IsValidLayoutSize(height) ? height : fallback;
        }

        private void RefreshResponsiveLayout(float width, float height)
        {
            if (_root == null)
                return;

            if (IsValidLayoutSize(width))
            {
                _root.EnableInClassList(NarrowLayoutClassName, width < NarrowLayoutWidth);
                _root.EnableInClassList(ExtraNarrowLayoutClassName, width < ExtraNarrowLayoutWidth);
            }

            if (IsValidLayoutSize(height))
                _root.EnableInClassList(ShortLayoutClassName, height < ShortLayoutHeight);
        }

        private static bool IsValidLayoutSize(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private void RefreshControls()
        {
            if (!TryGetInspectedComponent(out MagicLODEditorComponent component))
                return;

            bool generateLods = component.Options.OperationMode == OperationMode.GenerateLODs;
            bool exportMeshes = component.Options.ExportMeshes;

            using (new UpdateScope(this))
            {
                _coreSettings.RefreshVisibility(generateLods, exportMeshes, component.Options.SyncMode);
                _lodSettings.RefreshControls();
                _workflow.RefreshVisibility(generateLods);
            }
        }

        private void CollapsePreservationsForPreview() => _preservations?.CollapseForPreview();

        private void RefreshPreservationListIfNeeded() => _preservations?.RefreshList(force: false);

        private bool TryGetInspectedComponent(out MagicLODEditorComponent component) =>
            (component = target as MagicLODEditorComponent) != null;

        private void ForEachTarget(Action<MagicLODEditorComponent> action)
        {
            if (action == null || targets == null)
                return;

            foreach (UnityEngine.Object currentTarget in targets)
            {
                if (currentTarget is MagicLODEditorComponent component)
                    action(component);
            }
        }

        private void ModifyTargets(string undoName, Action<MagicLODEditorComponent> action)
        {
            if (action == null || targets == null)
                return;

            using (new UpdateScope(this))
            {
                Undo.RecordObjects(targets, undoName);
                ForEachTarget(component =>
                {
                    action(component);
                    EditorUtility.SetDirty(component);
                });

                serializedObject.Update();
            }
        }

        private VisualElement LoadLayout(out string error)
        {
            VisualTreeAsset visualTree = MagicLODUIAssetLoader.Load<VisualTreeAsset>(
                typeof(MagicLODEditorComponentEditor),
                LayoutGuid,
                LayoutFileName,
                "MagicLOD Component layout",
                out string layoutError);
            StyleSheet styleSheet = MagicLODUIAssetLoader.Load<StyleSheet>(
                typeof(MagicLODEditorComponentEditor),
                StylesGuid,
                StylesFileName,
                "MagicLOD Component styles",
                out string stylesError);

            if (visualTree == null || styleSheet == null)
            {
                List<string> errors = new List<string>();

                if (!string.IsNullOrEmpty(layoutError))
                    errors.Add(layoutError);

                if (!string.IsNullOrEmpty(stylesError))
                    errors.Add(stylesError);

                error = string.Join("\n\n", errors);
                return null;
            }

            TemplateContainer root = visualTree.CloneTree();
            root.styleSheets.Add(styleSheet);
            error = null;
            return root;
        }

        private VisualElement CreateMissingAssetsMessage(string details)
        {
            string message = string.IsNullOrEmpty(details)
                ? "MagicLOD inspector assets are missing."
                : $"MagicLOD inspector assets are missing.\n\n{details}";
            HelpBox helpBox = new HelpBox(message, HelpBoxMessageType.Error);
            helpBox.style.marginTop = 8;
            helpBox.style.marginBottom = 8;

            return helpBox;
        }

        private static void SetVisible(VisualElement element, bool visible) => element?.EnableInClassList(HiddenClassName, !visible);

        private void BindAndTrack(VisualElement root, string elementName, SerializedProperty property, Action onChanged)
        {
            PropertyField field = root.Q<PropertyField>(elementName);

            if (property == null || field == null)
                return;

            if (field.userData is not PropertyFieldChangeHandler changeHandler)
            {
                changeHandler = new PropertyFieldChangeHandler(this);
                field.userData = changeHandler;
                field.RegisterCallback<SerializedPropertyChangeEvent>(changeHandler.OnPropertyChanged);
            }

            changeHandler.SetProperty(property, onChanged);

            field.Unbind();
            field.BindProperty(property);
        }

        private void BindTrackedField(VisualElement root, string elementName, string relativePropertyName, SerializedProperty rootProperty, Action onChanged)
        {
            SerializedProperty property = rootProperty?.FindPropertyRelative(relativePropertyName);

            if (property != null)
                BindAndTrack(root, elementName, property, onChanged);
        }

        private void NotifySyncOptionsChanged() => _syncOptionsChanged?.Schedule();

        private void NotifyDecimationOptionsChanged() => _decimationOptionsChanged?.Schedule();

        private void NotifyExportOptionsChanged() => _exportOptionsChanged?.Schedule();

        private void NotifyTargetsSyncOptionsChanged() => ForEachTarget(component => component.OnSyncOptionsChanged());

        private void NotifyTargetsDecimationOptionsChanged() => ForEachTarget(component => component.OnDecimationOptionsChanged());

        private void NotifyTargetsExportOptionsChanged() => ForEachTarget(component => component.OnExportOptionsChanged());

        private void NotifyInspectorClosed()
        {
            if (_inspectorClosedNotified)
                return;

            _inspectorClosedNotified = true;
            ForEachTarget(component => component.OnInspectorClosed());
        }

        private readonly struct UpdateScope : IDisposable
        {
            private readonly MagicLODEditorComponentEditor _editor;
            private readonly bool _wasUpdating;

            public UpdateScope(MagicLODEditorComponentEditor editor)
            {
                _editor = editor;
                _wasUpdating = editor._isUpdatingControls;
                _editor._isUpdatingControls = true;
            }

            public void Dispose() => _editor._isUpdatingControls = _wasUpdating;
        }

        private sealed class DelayedAction
        {
            private readonly Action _action;
            private readonly IVisualElementScheduledItem _scheduledItem;
            private bool _isPending;


            public DelayedAction(VisualElement root, Action action)
            {
                _action = action;
                _scheduledItem = root.schedule.Execute(Flush);
            }

            public void Schedule()
            {
                _isPending = true;
                _scheduledItem.ExecuteLater(OptionsChangedDelayMs);
            }

            public void Flush()
            {
                if (!_isPending)
                    return;

                _isPending = false;
                _action.Invoke();
            }
        }

        private sealed class PropertyFieldChangeHandler
        {
            private readonly MagicLODEditorComponentEditor _editor;
            private Action _onChanged;
            private string _propertyPath;
            private object _lastValue;


            public PropertyFieldChangeHandler(MagicLODEditorComponentEditor editor)
            {
                _editor = editor;
            }

            public void SetProperty(SerializedProperty property, Action onChanged)
            {
                _propertyPath = property.propertyPath;
                _lastValue = property.boxedValue;
                _onChanged = onChanged;
            }

            public void OnPropertyChanged(SerializedPropertyChangeEvent evt)
            {
                SerializedProperty property = evt.changedProperty;

                if (_editor._isUpdatingControls ||
                    property == null ||
                    property.propertyPath != _propertyPath)
                {
                    return;
                }

                object currentValue = property.boxedValue;

                if (Equals(_lastValue, currentValue))
                    return;

                _lastValue = currentValue;
                _onChanged?.Invoke();
                _editor.RefreshControls();
            }
        }

        private abstract class InspectorModule
        {
            protected MagicLODEditorComponentEditor Editor { get; }
            private VisualElement Root { get; }

            protected InspectorModule(MagicLODEditorComponentEditor editor, VisualElement root)
            {
                Editor = editor;
                Root = root;
            }

            protected T Element<T>(string name) where T : VisualElement => Root.Q<T>(name);

            protected void BindAndTrack(string elementName, SerializedProperty property, Action onChanged) =>
                Editor.BindAndTrack(Root, elementName, property, onChanged);

            protected void BindTrackedField(string elementName, string propertyName, SerializedProperty rootProperty, Action onChanged) =>
                Editor.BindTrackedField(Root, elementName, propertyName, rootProperty, onChanged);

            protected static string FormatTriangles(int triangles) => Mathf.Max(0, triangles).ToString("N0");

            protected static string FormatReduction(int originalTriangles, int simplifiedTriangles)
            {
                float reduction = originalTriangles <= 0 ? 0f : (1f - (float)simplifiedTriangles / originalTriangles) * 100f;

                return $"{Mathf.Clamp(reduction, 0f, 100f):0.#}% REDUCTION";
            }
        }

        private sealed class CoreSettingsModule : InspectorModule
        {
            private readonly Button _generateLodsButton;
            private readonly Button _simplifyButton;
            private readonly Toggle _exportToggle;
            private readonly PropertyField _syncGroupField;
            private readonly PropertyField _maxErrorField;
            private readonly VisualElement _lodSettingsFoldout;
            private readonly VisualElement _exportFolderRow;
            private readonly Button _browseExportButton;

            public CoreSettingsModule(MagicLODEditorComponentEditor editor, VisualElement root, SerializedObject so) : base(editor, root)
            {
                SerializedProperty optionsProperty = so.FindProperty("_options");
                SerializedProperty decimationSettingsProperty = optionsProperty?.FindPropertyRelative("_decimationSettings");

                _generateLodsButton = Element<Button>("generate-lods-toggle");
                _simplifyButton = Element<Button>("simplify-toggle");
                _exportToggle = Element<Toggle>("export-toggle");
                _syncGroupField = Element<PropertyField>("sync-group-field");
                _maxErrorField = Element<PropertyField>("max-error-field");
                _lodSettingsFoldout = Element<Foldout>("lod-settings-foldout");
                _exportFolderRow = Element<VisualElement>("export-folder-row");
                _browseExportButton = Element<Button>("browse-export-button");

                BindAndTrack("sync-mode-field", optionsProperty?.FindPropertyRelative("_syncMode"), Editor.NotifySyncOptionsChanged);
                BindAndTrack("sync-group-field", optionsProperty?.FindPropertyRelative("_syncGroupID"), Editor.NotifySyncOptionsChanged);
                BindAndTrack("include-children-field", optionsProperty?.FindPropertyRelative("_includeChildren"), Editor.NotifyDecimationOptionsChanged);
                BindAndTrack("max-error-field", optionsProperty?.FindPropertyRelative("_maxSimplificationError"), Editor.NotifyDecimationOptionsChanged);
                BindAndTrack("export-folder-field", optionsProperty?.FindPropertyRelative("_exportFolder"), Editor.NotifyExportOptionsChanged);

                BindTrackedField("borders-penalty-field", "bordersPenaltyWeight", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("uv-seams-penalty-field", "uvSeamsPenaltyWeight", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("normal-seams-penalty-field", "normalSeamsPenaltyWeight", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("uv-mode-field", "uvMode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("uv2-mode-field", "uv2Mode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("uv3-mode-field", "uv3Mode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("uv4-mode-field", "uv4Mode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("normals-mode-field", "normalsMode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("tangents-mode-field", "tangentsMode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("colors-mode-field", "colorsMode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("bone-weights-mode-field", "boneWeightsInterpolationMode", decimationSettingsProperty, Editor.NotifyDecimationOptionsChanged);

                _generateLodsButton.clicked += () => SetOperationMode(OperationMode.GenerateLODs);
                _simplifyButton.clicked += () => SetOperationMode(OperationMode.Simplify);
                _exportToggle.RegisterValueChangedCallback(OnExportChanged);
                _browseExportButton.clicked += BrowseExportFolder;
            }

            public void RefreshVisibility(bool generateLods, bool exportMeshes, SyncMode syncMode)
            {
                _generateLodsButton.EnableInClassList(ActiveModeClassName, generateLods);
                _simplifyButton.EnableInClassList(ActiveModeClassName, !generateLods);
                _exportToggle.SetValueWithoutNotify(exportMeshes);

                SetVisible(_syncGroupField, syncMode == SyncMode.WithSameGroupID);
                SetVisible(_lodSettingsFoldout, generateLods);
                SetVisible(_maxErrorField, !generateLods);
                SetVisible(_exportFolderRow, exportMeshes);
            }

            private void SetOperationMode(OperationMode mode) =>
                ModifyOptions("Change MagicLOD Operation Mode", component => component.Options.OperationMode = mode);

            private void SetExportMeshes(bool value) =>
                ModifyOptions("Toggle MagicLOD Export", component => component.Options.ExportMeshes = value);

            private void ModifyOptions(string undoName, Action<MagicLODEditorComponent> action)
            {
                Editor.ModifyTargets(undoName, action);
                Editor.RefreshControls();
            }

            private void OnExportChanged(ChangeEvent<bool> evt)
            {
                if (!Editor._isUpdatingControls)
                    SetExportMeshes(evt.newValue);
            }

            private void BrowseExportFolder()
            {
                if (!Editor.TryGetInspectedComponent(out MagicLODEditorComponent component))
                    return;

                string absoluteFolder = string.IsNullOrEmpty(component.Options.ExportFolder)
                    ? Application.dataPath
                    : Path.GetFullPath(component.Options.ExportFolder);
                string selectedFolder = EditorUtility.OpenFolderPanel("Select MagicLOD export folder", absoluteFolder, string.Empty);

                if (string.IsNullOrEmpty(selectedFolder))
                    return;

                string normalizedDataPath = Application.dataPath.Replace('\\', '/');
                string normalizedSelection = selectedFolder.Replace('\\', '/');
                string projectPath = normalizedSelection.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase)
                    ? $"Assets{normalizedSelection.Substring(normalizedDataPath.Length)}"
                    : normalizedSelection;

                Editor.ModifyTargets("Change MagicLOD Export Folder", item => item.Options.ExportFolder = projectPath);
            }
        }

        private sealed class LODSettingsModule : InspectorModule
        {
            private readonly SerializedObject _serializedObject;
            private readonly Toggle _configuratorToggle;
            private readonly VisualElement _screenSpaceSettings;
            private readonly VisualElement _manualSettings;
            private readonly ManualLODModule _manualLOD;
            private bool? _isManual;

            private bool HasMixedState
            {
                get
                {
                    if (Editor.targets == null || Editor.targets.Length == 0)
                        return false;

                    bool firstValue = (Editor.targets[0] as MagicLODEditorComponent)?.IsManualLODConfigurator ?? false;
                    for (int i = 1; i < Editor.targets.Length; i++)
                    {
                        if (Editor.targets[i] is MagicLODEditorComponent component && component.IsManualLODConfigurator != firstValue)
                            return true;
                    }

                    return false;
                }
            }

            public LODSettingsModule(MagicLODEditorComponentEditor editor, VisualElement root, SerializedObject serializedObject) : base(editor, root)
            {
                _serializedObject = serializedObject;
                _configuratorToggle = Element<Toggle>("manual-configurator-toggle");
                _screenSpaceSettings = Element<VisualElement>("screen-space-settings");
                _manualSettings = Element<VisualElement>("manual-lod-settings");
                _manualLOD = new ManualLODModule(editor, root);

                _configuratorToggle.RegisterValueChangedCallback(OnConfiguratorChanged);
            }

            public void RefreshControls()
            {
                if (!Editor.TryGetInspectedComponent(out MagicLODEditorComponent component))
                    return;

                bool isManual = component.IsManualLODConfigurator;
                if (_isManual != isManual)
                {
                    _isManual = isManual;
                    _manualLOD.Invalidate();

                    using (new UpdateScope(Editor))
                        _serializedObject.UpdateIfRequiredOrScript();

                    BindScreenSpaceFields();
                }

                using (new UpdateScope(Editor))
                {
                    _configuratorToggle.showMixedValue = HasMixedState;
                    _configuratorToggle.SetValueWithoutNotify(isManual);
                }

                SetVisible(_screenSpaceSettings, !isManual);
                SetVisible(_manualSettings, isManual);

                if (isManual && component.Options.LODGroupConfigurator is ManualLODConfigurator configurator)
                {
                    _manualLOD.Refresh(configurator);
                }
            }

            private void BindScreenSpaceFields()
            {
                SerializedProperty configurator = _serializedObject.FindProperty("_options")?.FindPropertyRelative("_lodConfigurator");
                BindTrackedField("lod-fade-mode-field", "_fadeMode", configurator, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("lod-target-triangles-field", "_targetTrianglesPer100Pixels", configurator, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("lod-min-mesh-quality-field", "_minMeshQuality", configurator, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("lod-min-reduction-field", "_minReductionPercentage", configurator, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("lod-max-count-field", "_maxLODs", configurator, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("lod-cull-height-field", "_cullHeight", configurator, Editor.NotifyDecimationOptionsChanged);
                BindTrackedField("lod-never-cull-size-field", "_neverCullMaxSize", configurator, Editor.NotifyDecimationOptionsChanged);
            }

            private void SetManualConfigurator(bool value)
            {
                Editor.ModifyTargets("Change MagicLOD LOD Configurator", component => component.IsManualLODConfigurator = value);
                _isManual = null;
                _manualLOD.ResetSelection();
                RefreshControls();
            }

            private void OnConfiguratorChanged(ChangeEvent<bool> evt)
            {
                if (!Editor._isUpdatingControls)
                    SetManualConfigurator(evt.newValue);
            }
        }

        private sealed class ManualLODModule : InspectorModule
        {
            private const int MinLODCount = 1;
            private const int MaxLODCount = 7;
            private const float ValuePrecision = 100f;
            private const float ValueEpsilon = 0.00001f;

            private readonly VisualElement _bar;
            private readonly EnumField _fadeModeField;
            private readonly Label _countLabel;
            private readonly Label _selectedLODLabel;
            private readonly Label _selectedRangeLabel;
            private readonly Label _transitionHint;
            private readonly Button _removeLODButton;
            private readonly Button _addLODButton;
            private readonly Slider _qualitySlider;
            private readonly Slider _transitionSlider;
            private readonly List<Button> _lodButtons = new(MaxLODCount);
            private readonly List<VisualElement> _transitionHandles = new(MaxLODCount);

            private Label _cullSegment;
            private int _selectedLOD;
            private int _renderedLODCount = -1;
            private int _draggedTransition = -1;
            private int _transitionUndoGroup = -1;

            private ManualLODConfigurator Configurator => Editor.TryGetInspectedComponent(out MagicLODEditorComponent component)
                ? component.Options.LODGroupConfigurator as ManualLODConfigurator
                : null;

            public ManualLODModule(MagicLODEditorComponentEditor editor, VisualElement root) : base(editor, root)
            {
                _fadeModeField = Element<EnumField>("manual-fade-mode-field");
                _countLabel = Element<Label>("manual-lod-count-label");
                _removeLODButton = Element<Button>("manual-remove-lod-button");
                _addLODButton = Element<Button>("manual-add-lod-button");
                _bar = Element<VisualElement>("manual-lod-bar");
                _selectedLODLabel = Element<Label>("manual-selected-lod-label");
                _selectedRangeLabel = Element<Label>("manual-selected-range-label");
                _qualitySlider = Element<Slider>("manual-quality-slider");
                _transitionSlider = Element<Slider>("manual-transition-slider");
                _transitionHint = Element<Label>("manual-transition-hint");

                _fadeModeField.Init(LODFadeMode.None);
                _fadeModeField.RegisterValueChangedCallback(OnFadeModeChanged);
                _qualitySlider.RegisterValueChangedCallback(OnQualityChanged);
                _transitionSlider.RegisterValueChangedCallback(OnTransitionHeightChanged);
                _removeLODButton.clicked += () => ChangeLODCount(-1);
                _addLODButton.clicked += () => ChangeLODCount(1);
            }

            public void Invalidate() => _renderedLODCount = -1;

            public void ResetSelection()
            {
                _selectedLOD = 0;
                Invalidate();
            }

            public void Refresh(ManualLODConfigurator configurator)
            {
                int lodCount = configurator.LODsCount;
                if (lodCount <= 0 || configurator.Quality?.Count != lodCount || configurator.TransitionHeights?.Count != lodCount)
                    return;

                _selectedLOD = Mathf.Clamp(_selectedLOD, 0, lodCount - 1);
                if (_renderedLODCount != lodCount)
                    RebuildBar(lodCount);

                using (new UpdateScope(Editor))
                {
                    _fadeModeField.SetValueWithoutNotify(configurator.FadeMode);
                    _countLabel.text = lodCount == 1 ? "1 LEVEL" : $"{lodCount} LEVELS";
                    _removeLODButton.SetEnabled(lodCount > MinLODCount);
                    _addLODButton.SetEnabled(lodCount < MaxLODCount);
                    UpdateBar(configurator);
                    UpdateSelectedLOD(configurator);
                }
            }

            private void UpdateSelectedLOD(ManualLODConfigurator configurator)
            {
                int index = _selectedLOD;
                float minQuality = index < configurator.Quality.Count - 1 ? configurator.Quality[index + 1] : 0f;
                float maxQuality = index > 0 ? configurator.Quality[index - 1] : 1f;
                float minTransition = index < configurator.TransitionHeights.Count - 1 ? configurator.TransitionHeights[index + 1] : 0f;
                float maxTransition = index > 0 ? configurator.TransitionHeights[index - 1] : 1f;

                RefreshSlider(_qualitySlider, configurator.Quality[index], minQuality, maxQuality);
                RefreshSlider(_transitionSlider, configurator.TransitionHeights[index], minTransition, maxTransition);

                _selectedLODLabel.text = $"LOD {index}";
                _selectedRangeLabel.text = $"{FormatPercent(maxTransition)} – " + $"{FormatPercent(configurator.TransitionHeights[index])}";
                _transitionHint.text =
                    index == configurator.LODsCount - 1 ? "Cull the object below this screen height" : $"Switch to LOD {index + 1} below this screen height";
            }

            private void RebuildBar(int lodCount)
            {
                _bar.Clear();
                _lodButtons.Clear();
                _transitionHandles.Clear();

                for (int i = 0; i < lodCount; i++)
                {
                    int lodIndex = i;
                    Button button = new Button(() => SelectLOD(lodIndex)) { name = $"manual-lod-{lodIndex}-button" };
                    button.AddToClassList("manual-lod-segment");
                    button.AddToClassList($"manual-lod-segment--{lodIndex}");
                    _lodButtons.Add(button);
                    _bar.Add(button);
                }

                _cullSegment = new Label("CULLED") { pickingMode = PickingMode.Ignore };
                _cullSegment.AddToClassList("manual-lod-cull-segment");
                _bar.Add(_cullSegment);

                for (int i = 0; i < lodCount; i++)
                {
                    VisualElement handle = CreateTransitionHandle(i);
                    _transitionHandles.Add(handle);
                    _bar.Add(handle);
                }

                _renderedLODCount = lodCount;
            }

            private void UpdateBar(ManualLODConfigurator configurator)
            {
                float previousTransition = 1f;
                for (int i = 0; i < _lodButtons.Count; i++)
                {
                    float quality = Mathf.Clamp01(configurator.Quality[i]);
                    float transition = Mathf.Clamp01(configurator.TransitionHeights[i]);

                    _lodButtons[i].text = $"LOD {i}\nH {FormatPercent(transition)} " + $"(Q {quality.ToString("0.##", CultureInfo.InvariantCulture)})";
                    _lodButtons[i].style.width = Length.Percent(Mathf.Max(0f, previousTransition - transition) * 100f);
                    _lodButtons[i].EnableInClassList("manual-lod-segment--selected", i == _selectedLOD);

                    _transitionHandles[i].style.left = Length.Percent((1f - transition) * 100f);
                    _transitionHandles[i].EnableInClassList("manual-lod-transition-handle--dragging", i == _draggedTransition);
                    previousTransition = transition;
                }

                int lastIndex = configurator.TransitionHeights.Count - 1;
                _cullSegment.style.width = Length.Percent(Mathf.Clamp01(configurator.TransitionHeights[lastIndex]) * 100f);
            }

            private VisualElement CreateTransitionHandle(int lodIndex)
            {
                VisualElement handle = new VisualElement { name = $"manual-lod-{lodIndex}-transition-handle" };
                handle.AddToClassList("manual-lod-transition-handle");

                VisualElement line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("manual-lod-transition-handle__line");

                VisualElement grip = new VisualElement { pickingMode = PickingMode.Ignore };
                grip.AddToClassList("manual-lod-transition-handle__grip");

                handle.Add(line);
                handle.Add(grip);
                handle.RegisterCallback<PointerDownEvent>(evt => BeginDrag(lodIndex, handle, evt));
                handle.RegisterCallback<PointerMoveEvent>(evt => Drag(lodIndex, evt));
                handle.RegisterCallback<PointerUpEvent>(evt => EndDrag(handle, evt));
                handle.RegisterCallback<PointerCaptureOutEvent>(
                    _ => FinishDrag(handle));
                return handle;
            }

            private void ChangeLODCount(int delta)
            {
                ManualLODConfigurator configurator = Configurator;
                if (configurator == null)
                    return;

                int newCount = Mathf.Clamp(configurator.LODsCount + delta, MinLODCount, MaxLODCount);
                if (newCount == configurator.LODsCount)
                    return;

                _selectedLOD = delta > 0 ? newCount - 1 : Mathf.Min(_selectedLOD, newCount - 1);
                ModifyConfigurators("Change Manual LOD Count", item =>
                {
                    int previousCount = item.LODsCount;
                    item.SetLODsCount(newCount);

                    if (newCount <= previousCount)
                        return;

                    float previousTransition = previousCount > 1
                        ? item.TransitionHeights[previousCount - 2]
                        : 1f;
                    float lastTransition = item.TransitionHeights[previousCount - 1];

                    item.SetTransitionHeight(previousCount - 1, Mathf.Lerp(previousTransition, lastTransition, 0.5f));
                    item.SetQuality(newCount - 1, item.Quality[previousCount - 1] * 0.5f);
                });
            }

            private void SetQuality(float value)
            {
                int lodIndex = _selectedLOD;
                ModifyConfigurators("Change LOD Quality", item =>
                {
                    if (lodIndex < 0 || lodIndex >= item.LODsCount)
                        return;

                    float min = lodIndex < item.LODsCount - 1 ? item.Quality[lodIndex + 1] : 0f;
                    float max = lodIndex > 0 ? item.Quality[lodIndex - 1] : 1f;
                    item.SetQuality(lodIndex, SnapValue(value, min, max));
                });
            }

            private void SetTransitionHeight(float value)
            {
                int lodIndex = _selectedLOD;
                ModifyConfigurators("Change LOD Transition", item =>
                {
                    if (lodIndex < 0 || lodIndex >= item.LODsCount)
                        return;

                    float min = lodIndex < item.LODsCount - 1 ? item.TransitionHeights[lodIndex + 1] : 0f;
                    float max = lodIndex > 0 ? item.TransitionHeights[lodIndex - 1] : 1f;
                    item.SetTransitionHeight(lodIndex, SnapValue(value, min, max));
                });
            }

            private void ModifyConfigurators(string undoName, Action<ManualLODConfigurator> action)
            {
                Editor.ModifyTargets(undoName, component =>
                {
                    if (component.Options.LODGroupConfigurator is ManualLODConfigurator configurator)
                        action(configurator);
                });
                Editor.NotifyDecimationOptionsChanged();

                ManualLODConfigurator configurator = Configurator;
                if (configurator != null)
                    Refresh(configurator);
            }

            private void SelectLOD(int lodIndex)
            {
                ManualLODConfigurator configurator = Configurator;
                int lodCount = configurator?.LODsCount ?? 0;
                _selectedLOD = Mathf.Clamp(lodIndex, 0, Mathf.Max(0, lodCount - 1));

                if (configurator != null)
                    Refresh(configurator);
            }

            private void OnFadeModeChanged(ChangeEvent<Enum> evt)
            {
                if (Editor._isUpdatingControls || evt.newValue is not LODFadeMode fadeMode)
                    return;

                ModifyConfigurators("Change Fade Mode", configurator => configurator.SetFadeMode(fadeMode));
            }

            private void OnQualityChanged(ChangeEvent<float> evt)
            {
                if (!Editor._isUpdatingControls)
                    SetQuality(evt.newValue);
            }

            private void OnTransitionHeightChanged(ChangeEvent<float> evt)
            {
                if (!Editor._isUpdatingControls)
                    SetTransitionHeight(evt.newValue);
            }

            private void BeginDrag(int lodIndex, VisualElement handle, PointerDownEvent evt)
            {
                ManualLODConfigurator configurator = Configurator;
                if (evt.button != 0 || configurator == null)
                    return;

                _selectedLOD = lodIndex;
                _draggedTransition = lodIndex;
                _transitionUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Change Manual LOD Transition");

                handle.AddToClassList("manual-lod-transition-handle--dragging");
                handle.CapturePointer(evt.pointerId);
                Refresh(configurator);
                evt.StopPropagation();
            }

            private void Drag(int lodIndex, PointerMoveEvent evt)
            {
                ManualLODConfigurator configurator = Configurator;
                if (_draggedTransition != lodIndex || configurator == null)
                    return;

                float barWidth = _bar.contentRect.width;
                if (barWidth <= 0f)
                    return;

                float pointerPosition = _bar.WorldToLocal(evt.position).x;
                float min = lodIndex < configurator.LODsCount - 1 ? configurator.TransitionHeights[lodIndex + 1] : 0f;
                float max = lodIndex > 0 ? configurator.TransitionHeights[lodIndex - 1] : 1f;
                float transition = SnapValue(1f - Mathf.Clamp01(pointerPosition / barWidth), min, max);

                if (!Mathf.Approximately(configurator.TransitionHeights[lodIndex], transition))
                {
                    SetTransitionHeight(transition);
                }

                evt.StopPropagation();
            }

            private void EndDrag(VisualElement handle, PointerUpEvent evt)
            {
                if (_draggedTransition < 0)
                    return;

                if (handle.HasPointerCapture(evt.pointerId))
                    handle.ReleasePointer(evt.pointerId);
                else
                    FinishDrag(handle);

                evt.StopPropagation();
            }

            private void FinishDrag(VisualElement handle)
            {
                if (_draggedTransition < 0)
                    return;

                if (_transitionUndoGroup >= 0)
                    Undo.CollapseUndoOperations(_transitionUndoGroup);

                _draggedTransition = -1;
                _transitionUndoGroup = -1;
                handle.RemoveFromClassList("manual-lod-transition-handle--dragging");
            }

            private static void RefreshSlider(Slider slider, float value, float minValue, float maxValue)
            {
                GetRoundedRange(minValue, maxValue, out float roundedMin, out float roundedMax);
                float displayedValue = SnapValue(value, roundedMin, roundedMax);

                // Expand before assigning the value. Otherwise UI Toolkit can
                // clamp an old value and write that temporary value back.
                if (!Mathf.Approximately(slider.lowValue, 0f))
                    slider.lowValue = 0f;
                if (!Mathf.Approximately(slider.highValue, 1f))
                    slider.highValue = 1f;

                slider.SetValueWithoutNotify(displayedValue);

                if (!Mathf.Approximately(slider.lowValue, roundedMin))
                    slider.lowValue = roundedMin;

                if (!Mathf.Approximately(slider.highValue, roundedMax))
                    slider.highValue = roundedMax;
            }

            private static float SnapValue(float value, float min, float max)
            {
                GetRoundedRange(min, max, out float roundedMin, out float roundedMax);
                float rounded = Mathf.Round(Mathf.Clamp01(value) * ValuePrecision) / ValuePrecision;
                return Mathf.Clamp(rounded, roundedMin, roundedMax);
            }

            private static void GetRoundedRange(float minValue, float maxValue, out float roundedMin, out float roundedMax)
            {
                minValue = Mathf.Clamp01(minValue);
                maxValue = Mathf.Clamp(maxValue, minValue, 1f);
                roundedMin = Mathf.Ceil((minValue - ValueEpsilon) * ValuePrecision) / ValuePrecision;
                roundedMax = Mathf.Floor((maxValue + ValueEpsilon) * ValuePrecision) / ValuePrecision;

                if (roundedMin > roundedMax)
                {
                    roundedMin = minValue;
                    roundedMax = maxValue;
                }
            }

            private static string FormatPercent(float value)
            {
                float percent = Mathf.Clamp01(value) * 100f;
                return percent < 10f ? $"{percent:0.#}%" : $"{percent:0}%";
            }
        }

        private sealed class PreservationsModule : InspectorModule
        {
            private readonly SerializedProperty _preservationsProp;

            private readonly VisualElement _preservationEmpty;
            private readonly ListView _preservationList;
            private readonly Foldout _decimationFoldout;
            private readonly Foldout _preservationsFoldout;
            private readonly Toggle _editToggle;
            private readonly Button _removeButton;
            private readonly List<int> _indices = new();
            private int _displayedVolumeCount = -1;

            public PreservationsModule(MagicLODEditorComponentEditor editor, VisualElement root, SerializedObject so) : base(editor, root)
            {
                _preservationsProp = so.FindProperty("_options")?.FindPropertyRelative("_decimationSettings")?.FindPropertyRelative("preservationVolumes");

                _preservationEmpty = Element<VisualElement>("preservation-empty");
                _preservationList = Element<ListView>("preservation-list");
                _decimationFoldout = Element<Foldout>("decimation-foldout");
                _preservationsFoldout = Element<Foldout>("preservations-foldout");
                _editToggle = Element<Toggle>("edit-preservations-toggle");
                _removeButton = Element<Button>("remove-preservation-button");

                if (_preservationList != null)
                {
                    _preservationList.itemsSource = _indices;
                    _preservationList.selectionType = SelectionType.Single;
                    _preservationList.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
                    _preservationList.makeItem = () => new PropertyField();
                    _preservationList.bindItem = BindListItem;
                    _preservationList.RegisterCallback<SerializedPropertyChangeEvent>(
                        _ =>
                        {
                            using (new UpdateScope(Editor))
                                so.ApplyModifiedProperties();

                            Editor.NotifyDecimationOptionsChanged();
                        });
                }

                _preservationList.selectionChanged +=
                    _ => RefreshControls();
                _decimationFoldout.RegisterValueChangedCallback(
                    _ => RefreshControls());
                _preservationsFoldout.RegisterValueChangedCallback(
                    _ => RefreshControls());
                _editToggle.RegisterValueChangedCallback(
                    _ => RefreshControls());

                Element<Button>("add-preservation-button").clicked += AddVolume;
                _removeButton.clicked += RemoveSelectedVolume;
            }

            private int SelectedIndex => _preservationList?.selectedIndex is int index && index >= 0 && index < _indices.Count
                ? _indices[index]
                : -1;

            public void RefreshList(bool force = true)
            {
                int volumeCount = _preservationsProp?.arraySize ?? 0;

                if (!force && _displayedVolumeCount == volumeCount)
                    return;

                int previousIndex = SelectedIndex;

                _displayedVolumeCount = volumeCount;
                _indices.Clear();

                for (int i = 0; i < volumeCount; i++)
                    _indices.Add(i);

                _preservationList?.Rebuild();
                bool hasItems = _indices.Count > 0;

                SetVisible(_preservationList, hasItems);
                SetVisible(_preservationEmpty, !hasItems);
                SetVisible(_editToggle, hasItems);
                SetVisible(_removeButton, hasItems);

                if (!hasItems)
                    _editToggle.SetValueWithoutNotify(false);

                if (_preservationList != null)
                {
                    if (previousIndex >= 0 && previousIndex < _indices.Count)
                        _preservationList.selectedIndex = previousIndex;
                    else
                        _preservationList.ClearSelection();
                }

                RefreshControls();
            }

            public void RefreshControls()
            {
                bool shouldEdit = _indices.Count > 0 && _decimationFoldout.value && _preservationsFoldout.value && _editToggle.value;
                Editor.ForEachTarget(component =>
                {
                    if (component.IsPreservationsEditing == shouldEdit)
                        return;

                    if (shouldEdit)
                        component.StartPreservationsEditing();
                    else
                        component.EndPreservationsEditing();
                });

                _removeButton.SetEnabled(SelectedIndex >= 0);
            }

            public void CollapseForPreview()
            {
                if (_preservationsFoldout.value)
                    _preservationsFoldout.value = false;
                else
                    RefreshControls();
            }

            private void BindListItem(VisualElement element, int visualIndex)
            {
                PropertyField field = element as PropertyField;

                if (field == null)
                    return;

                field.Unbind();

                if (_preservationsProp == null || visualIndex < 0 || visualIndex >= _indices.Count)
                    return;

                int propertyIndex = _indices[visualIndex];

                if (propertyIndex < 0 || propertyIndex >= _preservationsProp.arraySize)
                    return;

                field.label = $"Volume {propertyIndex + 1}";
                field.BindProperty(_preservationsProp.GetArrayElementAtIndex(propertyIndex));
            }

            private void AddVolume()
            {
                Editor.ModifyTargets("Add Preservation Volume", component => component.AddPreservationVolume());
                RefreshList();

                if (_indices.Count == 0)
                    return;

                _preservationList.selectedIndex = _indices.Count - 1;
                _preservationList.ScrollToItem(_preservationList.selectedIndex);
            }

            private void RemoveSelectedVolume()
            {
                int selectedIndex = SelectedIndex;

                if (selectedIndex < 0)
                    return;

                Editor.ModifyTargets("Remove Preservation Volume", component => component.RemovePreservationVolume(selectedIndex));
                RefreshList();

                if (_indices.Count > 0)
                    _preservationList.selectedIndex = Mathf.Min(selectedIndex, _indices.Count - 1);
            }
        }

        private sealed class WorkflowModule : InspectorModule
        {
            private readonly VisualElement _idleView;
            private readonly VisualElement _progressView;
            private readonly VisualElement _resultView;
            private readonly VisualElement _errorBanner;
            private readonly Label _errorLabel;
            private readonly Button _decimateButton;
            private readonly PreviewModule _preview;
            private readonly ProgressModule _progress;
            private readonly ResultModule _result;

            private bool? _wasInProgress;
            private bool? _wasDecimated;
            private bool? _hadErrors;
            private OperationMode? _mode;
            private string _errorText;

            public WorkflowModule(MagicLODEditorComponentEditor editor, VisualElement root) : base(editor, root)
            {
                _idleView = Element<VisualElement>("idle-view");
                _progressView = Element<VisualElement>("progress-view");
                _resultView = Element<VisualElement>("result-view");
                _errorBanner = Element<VisualElement>("error-banner");
                _errorLabel = Element<Label>("error-label");
                _decimateButton = Element<Button>("decimate-button");

                _preview = new PreviewModule(editor, root);
                _progress = new ProgressModule(editor, root);
                _result = new ResultModule(editor, root);

                _decimateButton.clicked += () => Editor.ForEachTarget(component => component.DecimateThreaded());
            }

            public void RefreshVisibility(bool generateLods)
            {
                _decimateButton.text = generateLods ? "GENERATE LODS" : "SIMPLIFY MESH";
                _preview.RefreshMode(generateLods);
            }

            public void Tick(bool force = false)
            {
                if (Editor.serializedObject?.targetObject == null || !Editor.TryGetInspectedComponent(out MagicLODEditorComponent component))
                    return;

                using (new UpdateScope(Editor))
                    Editor.serializedObject.UpdateIfRequiredOrScript();

                bool inProgress = component.IsDecimationInProgress;
                bool decimated = component.IsDecimated;
                bool hasErrors = component.HasErrors;
                bool phaseChanged = force || _wasInProgress != inProgress || _wasDecimated != decimated || _hadErrors != hasErrors;

                if (phaseChanged)
                {
                    SetVisible(_idleView, !inProgress && !decimated);
                    SetVisible(_progressView, inProgress);
                    SetVisible(_resultView, decimated && !inProgress);
                    SetVisible(_errorBanner, hasErrors);

                    _wasInProgress = inProgress;
                    _wasDecimated = decimated;
                    _hadErrors = hasErrors;
                }

                string errorText = hasErrors ? component.ErrorsText ?? string.Empty : string.Empty;
                if (force || !string.Equals(_errorText, errorText, StringComparison.Ordinal))
                {
                    _errorText = errorText;
                    _errorLabel.text = errorText;
                }

                if (!inProgress && !decimated)
                {
                    OperationMode mode = component.Options.OperationMode;
                    bool modeChanged = _mode != mode;
                    if (force || modeChanged)
                    {
                        _mode = mode;
                        Editor.RefreshControls();
                    }

                    _preview.Refresh(component, force || phaseChanged || modeChanged);
                    Editor.RefreshPreservationListIfNeeded();
                    return;
                }

                if (inProgress)
                    _progress.Refresh(component, force || phaseChanged);
                else
                    _result.Refresh(component, force || phaseChanged);
            }
        }

        private sealed class PreviewModule : InspectorModule
        {
            private readonly VisualElement _stats;
            private readonly Label _lodLabel;
            private readonly Label _description;
            private readonly Label _originalTrianglesLabel;
            private readonly Label _simplifiedTrianglesLabel;
            private readonly Label _simplifiedStatLabel;
            private readonly Label _reductionLabel;
            private readonly Button _previewButton;
            private readonly SliderInt _previewSlider;

            private int _currentLevel = -1;
            private int _maxLevel = -1;
            private int _originalTriangles = -1;
            private int _simplifiedTriangles = -1;
            private bool? _isStarted;

            public PreviewModule(MagicLODEditorComponentEditor editor, VisualElement root) : base(editor, root)
            {
                _stats = Element<VisualElement>("preview-stats");
                _lodLabel = Element<Label>("preview-lod-label");
                _description = Element<Label>("preview-description");
                _originalTrianglesLabel = Element<Label>("preview-original-triangles-label");
                _simplifiedTrianglesLabel = Element<Label>("preview-simplified-triangles-label");
                _simplifiedStatLabel = Element<Label>("preview-simplified-stat-label");
                _reductionLabel = Element<Label>("preview-reduction-label");
                _previewButton = Element<Button>("preview-button");
                _previewSlider = Element<SliderInt>("preview-slider");

                _previewSlider.RegisterValueChangedCallback(OnPreviewLevelChanged);
                _previewButton.clicked += TogglePreview;
            }

            public void RefreshMode(bool generateLods)
            {
                _description.text = GetDescription(generateLods);

                bool showLodSelector = generateLods && Editor.TryGetInspectedComponent(out MagicLODEditorComponent component) && component.IsPreviewStarted;
                SetVisible(_previewSlider, showLodSelector);
                SetVisible(_lodLabel, showLodSelector);
            }

            public void Refresh(MagicLODEditorComponent component, bool force = false)
            {
                bool generateLods = component.Options.OperationMode == OperationMode.GenerateLODs;
                int maxLevel = Mathf.Max(0, component.MaxPreviewLevel);
                int currentLevel = Mathf.Clamp(component.CurrentPreviewLevel, 0, maxLevel);
                bool isStarted = component.IsPreviewStarted;
                int originalTriangles = isStarted ? component.OriginalTrianglesCount : 0;
                int simplifiedTriangles = isStarted ? GetTriangles(component, generateLods, currentLevel) : 0;

                if (!force && _currentLevel == currentLevel && _maxLevel == maxLevel && _isStarted == isStarted && _originalTriangles == originalTriangles &&
                    _simplifiedTriangles == simplifiedTriangles)
                    return;

                _currentLevel = currentLevel;
                _maxLevel = maxLevel;
                _isStarted = isStarted;
                _originalTriangles = originalTriangles;
                _simplifiedTriangles = simplifiedTriangles;

                using (new UpdateScope(Editor))
                {
                    _previewSlider.lowValue = 0;
                    _previewSlider.highValue = maxLevel;
                    _previewSlider.SetValueWithoutNotify(currentLevel);
                }

                bool showLodSelector = generateLods && isStarted;
                SetVisible(_previewSlider, showLodSelector);
                SetVisible(_lodLabel, showLodSelector);
                SetVisible(_stats, isStarted);

                _lodLabel.text = $"LOD {currentLevel} / {maxLevel}";
                _description.text = GetDescription(generateLods);
                _previewButton.text = isStarted ? "END PREVIEW" : "START PREVIEW";
                _previewButton.EnableInClassList("button--active", isStarted);

                if (!isStarted)
                    return;

                _simplifiedStatLabel.text = generateLods ? $"LOD {currentLevel}" : "SIMPLIFIED";
                _originalTrianglesLabel.text = FormatTriangles(originalTriangles);
                _simplifiedTrianglesLabel.text = FormatTriangles(simplifiedTriangles);
                _reductionLabel.text = FormatReduction(originalTriangles, simplifiedTriangles);
            }

            private void OnPreviewLevelChanged(ChangeEvent<int> evt)
            {
                if (Editor._isUpdatingControls || !Editor.TryGetInspectedComponent(out MagicLODEditorComponent component))
                    return;

                Editor.ForEachTarget(item => item.CurrentPreviewLevel = evt.newValue);
                Refresh(component, force: true);
            }

            private void TogglePreview()
            {
                if (!Editor.TryGetInspectedComponent(out MagicLODEditorComponent component))
                    return;

                if (component.IsPreviewStarted)
                {
                    Editor.ForEachTarget(item => item.EndPreview());
                }
                else
                {
                    Editor.CollapsePreservationsForPreview();
                    Editor.ForEachTarget(item => item.StartPreview());
                }

                Refresh(component, force: true);
            }

            private static int GetTriangles(MagicLODEditorComponent component, bool generateLods, int currentLevel)
            {
                if (!generateLods)
                    return component.SimplifiedTrianglesCount;

                int[] lodTriangles = component.DecimatedTrianglesCount;
                if (lodTriangles == null || lodTriangles.Length == 0)
                    return 0;

                return lodTriangles[Mathf.Clamp(currentLevel, 0, lodTriangles.Length - 1)];
            }

            private static string GetDescription(bool generateLods) => generateLods
                ? "Inspect every generated LOD directly in Scene view"
                : "Inspect the simplified mesh directly in Scene view";
        }

        private sealed class ProgressModule : InspectorModule
        {
            private readonly Label _detailLabel;
            private readonly Label _percentLabel;
            private readonly ProgressBar _progressBar;

            private int _totalRequests = -1;
            private int _successfulRequests = -1;
            private int _failedRequests = -1;
            private float _progress = -1f;

            public ProgressModule(MagicLODEditorComponentEditor editor, VisualElement root) : base(editor, root)
            {
                _detailLabel = Element<Label>("progress-detail-label");
                _percentLabel = Element<Label>("progress-percent-label");
                _progressBar = Element<ProgressBar>("progress-bar");

                Element<Button>("complete-button").clicked += () => Editor.ForEachTarget(component => component.ForceCompleteDecimation());
                Element<Button>("cancel-button").clicked += () => Editor.ForEachTarget(component => component.CancelDecimation());
            }

            public void Refresh(MagicLODEditorComponent component, bool force = false)
            {
                int total = component.TotalDecimationRequests;
                int succeeded = component.SuccessfulDecimationRequests;
                int failed = component.FailedDecimationRequests;
                float progress = Mathf.Clamp01(EditorMeshDecimationProvider.ProgressPercentage) * 100f;
                int percent = Mathf.RoundToInt(progress);
                if (!force &&
                    _totalRequests == total &&
                    _successfulRequests == succeeded &&
                    _failedRequests == failed &&
                    Mathf.Approximately(_progress, progress))
                    return;

                _totalRequests = total;
                _successfulRequests = succeeded;
                _failedRequests = failed;
                _progress = progress;

                int completed = succeeded + failed;

                _progressBar.value = progress;
                _progressBar.title = $"{completed} / {total}";
                _percentLabel.text = $"{percent}%";
                _detailLabel.text = failed > 0 ? $"Completed {completed} of {total} requests · {failed} failed" : $"Completed {completed} of {total} requests";
            }
        }

        private sealed class ResultModule : InspectorModule
        {
            private readonly VisualElement _simplifyStats;
            private readonly VisualElement _lodStats;
            private readonly VisualElement _lodList;
            private readonly Label _description;
            private readonly Label _originalTrianglesLabel;
            private readonly Label _simplifiedTrianglesLabel;
            private readonly Label _lodOriginalTrianglesLabel;
            private readonly Label _reductionLabel;
            private readonly Button _replaceButton;

            private int _originalTriangles = -1;
            private int _simplifiedTriangles = -1;
            private int _displayedLodOriginalTriangles = -1;
            private int[] _displayedLodTriangles = Array.Empty<int>();
            private bool? _isSimplify;

            public ResultModule(MagicLODEditorComponentEditor editor, VisualElement root) : base(editor, root)
            {
                _simplifyStats = Element<VisualElement>("result-simplify-stats");
                _lodStats = Element<VisualElement>("result-lod-stats");
                _lodList = Element<VisualElement>("result-lod-list");
                _description = Element<Label>("result-description");
                _originalTrianglesLabel = Element<Label>("original-triangles-label");
                _simplifiedTrianglesLabel = Element<Label>("simplified-triangles-label");
                _lodOriginalTrianglesLabel = Element<Label>("result-lod-original-triangles-label");
                _reductionLabel = Element<Label>("reduction-label");
                _replaceButton = Element<Button>("replace-button");

                _replaceButton.clicked += () => Editor.ForEachTarget(component => component.ApplySimplifiedMesh());
                Element<Button>("revert-button").clicked += () => Editor.ForEachTarget(component => component.Revert());
            }

            public void Refresh(MagicLODEditorComponent component, bool force = false)
            {
                bool simplify = component.Options.OperationMode == OperationMode.Simplify;
                if (force || _isSimplify != simplify)
                {
                    _isSimplify = simplify;
                    SetVisible(_simplifyStats, simplify);
                    SetVisible(_lodStats, !simplify);
                    SetVisible(_reductionLabel, simplify);
                    SetVisible(_replaceButton, simplify);
                    _description.text = simplify ? "Simplification completed" : "LOD generation completed";
                }

                int originalTriangles = component.OriginalTrianglesCount;
                if (simplify)
                {
                    RefreshSimplifyResult(component, originalTriangles, force);
                    return;
                }

                if (force || _originalTriangles != originalTriangles)
                {
                    _originalTriangles = originalTriangles;
                    _lodOriginalTrianglesLabel.text = FormatTriangles(originalTriangles);
                }

                RefreshLodStatistics(originalTriangles, component.DecimatedTrianglesCount);
            }

            private void RefreshSimplifyResult(MagicLODEditorComponent component, int originalTriangles, bool force)
            {
                int simplifiedTriangles = component.SimplifiedTrianglesCount;
                if (force || _originalTriangles != originalTriangles || _simplifiedTriangles != simplifiedTriangles)
                {
                    _originalTriangles = originalTriangles;
                    _simplifiedTriangles = simplifiedTriangles;

                    _originalTrianglesLabel.text = FormatTriangles(originalTriangles);
                    _simplifiedTrianglesLabel.text = FormatTriangles(simplifiedTriangles);
                    _reductionLabel.text = FormatReduction(originalTriangles, simplifiedTriangles);
                }

                _replaceButton.SetEnabled(component.CanApplySimplifiedMesh && simplifiedTriangles > 0);
            }

            private void RefreshLodStatistics(int originalTriangles, int[] lodTriangles)
            {
                int lodCount = lodTriangles?.Length ?? 0;
                if (_displayedLodOriginalTriangles == originalTriangles && HaveSameValues(_displayedLodTriangles, lodTriangles))
                    return;

                _displayedLodOriginalTriangles = originalTriangles;
                _displayedLodTriangles = new int[lodCount];
                if (lodCount > 0)
                    Array.Copy(lodTriangles, _displayedLodTriangles, lodCount);

                _lodList.Clear();
                for (int i = 0; i < lodCount; i++)
                    _lodList.Add(CreateLodRow(i, lodCount, lodTriangles[i]));
            }

            private static VisualElement CreateLodRow(int lodIndex, int lodCount, int triangles)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("lod-stats__row");
                if (lodIndex == lodCount - 1)
                    row.AddToClassList("lod-stats__row--last");

                Label lodLabel = new Label($"LOD {lodIndex}");
                lodLabel.AddToClassList("lod-stats__row-label");

                VisualElement values = new VisualElement();
                values.AddToClassList("lod-stats__row-values");

                Label trianglesLabel = new Label(FormatTriangles(triangles));
                trianglesLabel.AddToClassList("lod-stats__row-value");

                Label unitLabel = new Label(" tris");
                unitLabel.AddToClassList("lod-stats__row-unit");

                values.Add(trianglesLabel);
                values.Add(unitLabel);
                row.Add(lodLabel);
                row.Add(values);
                return row;
            }

            private static bool HaveSameValues(int[] left, int[] right)
            {
                int rightLength = right?.Length ?? 0;
                if (left.Length != rightLength)
                    return false;

                for (int i = 0; i < left.Length; i++)
                {
                    if (left[i] != right[i])
                        return false;
                }

                return true;
            }
        }
    }
}
