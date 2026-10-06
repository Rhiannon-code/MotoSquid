using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NGS.MagicLOD.Editors.Components;
using NGS.MagicLOD.Editors.API;

namespace NGS.MagicLOD.Editors.UI
{
    [Overlay(typeof(SceneView), OverlayId, DisplayName)]
    public sealed class MagicLODOverlayEditor : Overlay
    {
        private const string OverlayId = "NGS.MagicLOD.SceneOverlay";
        private const string DisplayName = "MagicLOD";
        private const string OverlayLayoutGuid = "03d5414e84998206bc1c011a38ab5ca0";
        private const string OverlayStylesGuid = "ccb7e9ce1eef45889158b07092577d5a";
        private const string SharedStylesGuid = "69624a372953f764abde072ebabb4a8f";
        private const string SelectionTabIconGuid = "82f9b13c4c0b4e84a7752e9b04b32f6a";
        private const string ManagementTabIconGuid = "e6a2f7c1b3d94f3f9c4b1e8a7d0c52ab";
        private const string ObjectTabIconGuid = "f1d8c3a7e5b249c98a6f0b4d27e13c5a";
        private const string OverlayLayoutRelativePath = "MagicLODSceneOverlay.uxml";
        private const string OverlayStylesRelativePath = "MagicLODSceneOverlay.uss";
        private const string SharedStylesRelativePath = "../MagicLODEditorComponent/MagicLODEditorComponentEditor.uss";
        private const string SelectionTabIconRelativePath = "Icons/Selection.png";
        private const string ManagementTabIconRelativePath = "Icons/Management.png";
        private const string ObjectTabIconRelativePath = "Icons/Object.png";
        private const string HiddenClassName = "is-hidden";
        private const string ActiveTabClassName = "overlay-tab--active";
        private const string NarrowLayoutClassName = "magic-lod-overlay--narrow";
        private const string ExtraNarrowLayoutClassName = "magic-lod-overlay--extra-narrow";
        private const string ShortLayoutClassName = "magic-lod-overlay--short";
        private const int RefreshIntervalMs = 200;
        private const double SceneDataRefreshInterval = 0.5d;
        private const float NarrowLayoutWidth = 320f;
        private const float ExtraNarrowLayoutWidth = 240f;
        private const float ShortLayoutHeight = 360f;
        private const float CanvasHorizontalMargin = 12f;
        private const float CanvasVerticalMargin = 48f;
        private static readonly Vector2 MinimumOverlaySize = new Vector2(220f, 180f);

        private VisualElement _root;
        private MagicLODOverlay _overlay;
        private SerializedObject _serializedOverlay;
        private TabModule[] _tabs;
        private SettingsTabModule _settingsTab;
        private SelectionTabModule _selectionTab;
        private ManagementTabModule _managementTab;
        private ObjectTabModule _objectTab;
        private TabModule _activeTab;
        private double _nextSceneDataRefreshTime;
        private bool _attached;
        private Vector2 _lastCanvasSize;

        public override void OnCreated()
        {
            base.OnCreated();
            minSize = MinimumOverlaySize;
        }

        [MenuItem("Tools/NGSTools/MagicLOD/Overlay")]
        public static void ShowOverlayFromMenu()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView == null)
                sceneView = EditorWindow.GetWindow<SceneView>();

            if (sceneView == null)
            {
                Debug.LogWarning("MagicLOD Overlay requires an open Scene view.");
                return;
            }

            if (!sceneView.TryGetOverlay(OverlayId, out UnityEditor.Overlays.Overlay sceneOverlay))
            {
                Debug.LogWarning("MagicLOD Overlay was not found in the active Scene view.");
                return;
            }

            sceneOverlay.displayed = true;
            sceneOverlay.collapsed = false;
            sceneView.Focus();
            sceneView.Repaint();
        }

        public override VisualElement CreatePanelContent()
        {
            _objectTab?.Dispose();
            _root = null;
            _overlay = null;
            _serializedOverlay = null;
            _tabs = null;
            _settingsTab = null;
            _selectionTab = null;
            _managementTab = null;
            _objectTab = null;
            _activeTab = null;

            if (!EnsureOverlayState())
                return new HelpBox("MagicLOD Overlay API could not be created.", HelpBoxMessageType.Error);

            _root = LoadLayout(out string assetError);
            if (_root == null)
                return CreateAssetLoadingError(assetError);

            _root.focusable = true;
            CreateTabModules();
            BindOverlayProperties();
            ConfigureTabIcons();
            RegisterPanelCallbacks();
            SelectTab(_settingsTab, force: true);
            return _root;
        }

        private void CreateTabModules()
        {
            _settingsTab = new SettingsTabModule(this, _root);
            _selectionTab = new SelectionTabModule(this, _root);
            _managementTab = new ManagementTabModule(this, _root);
            _objectTab = new ObjectTabModule(this, _root);
            _tabs = new TabModule[]
            {
                _settingsTab,
                _selectionTab,
                _managementTab,
                _objectTab
            };

            foreach (TabModule tab in _tabs)
                tab.Button.clicked += () => SelectTab(tab);
        }

        private void RegisterPanelCallbacks()
        {
            _root.RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            _root.RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            _root.RegisterCallback<FocusOutEvent>(OnRootFocusOut);
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _root.schedule.Execute(Tick).Every(RefreshIntervalMs);
        }

        private bool EnsureOverlayState()
        {
            if (_overlay != null && _serializedOverlay != null)
                return true;

            _overlay = MagicLODOverlay.GetInstance();
            if (_overlay == null)
            {
                _serializedOverlay = null;
                return false;
            }

            _serializedOverlay = new SerializedObject(_overlay);
            BindOverlayProperties();
            _serializedOverlay.UpdateIfRequiredOrScript();
            return true;
        }

        private void BindOverlayProperties()
        {
            if (_serializedOverlay == null || _tabs == null)
                return;

            foreach (TabModule tab in _tabs)
                tab.BindProperties(_serializedOverlay);
        }

        private void Tick()
        {
            if (_root == null || !EnsureOverlayState())
                return;

            RefreshPopupConstraints();
            _serializedOverlay.UpdateIfRequiredOrScript();

            if (_activeTab?.UsesSceneData == true &&
                EditorApplication.timeSinceStartup >= _nextSceneDataRefreshTime)
            {
                RefreshActiveTabData();
            }

            _activeTab?.Tick();
            ApplyGizmoState();
            _activeTab?.Refresh();
        }

        private void SelectTab(TabModule tab, bool force = false)
        {
            if (tab == null || (!force && _activeTab == tab))
                return;

            _activeTab?.Deactivate();
            _activeTab = tab;

            foreach (TabModule candidate in _tabs)
            {
                bool selected = candidate == tab;
                SetVisible(candidate.Page, selected);
                candidate.Button.EnableInClassList(ActiveTabClassName, selected);
            }

            tab.Activate();
            ApplyGizmoState();
            RefreshActiveTabData();
            tab.Refresh();
        }

        private void RefreshActiveTabData()
        {
            if (!_attached || _activeTab?.UsesSceneData != true || !EnsureOverlayState())
                return;

            _nextSceneDataRefreshTime =
                EditorApplication.timeSinceStartup + SceneDataRefreshInterval;
            _overlay.Statistic?.Refresh();

            if (_overlay.Gizmos != null &&
                (_overlay.Gizmos.DrawPending || _overlay.Gizmos.DrawStatistic))
            {
                _overlay.Gizmos.Refresh();
                SceneView.RepaintAll();
            }
        }

        private void ApplyGizmoState()
        {
            if (_attached && !EnsureOverlayState())
                return;

            if (_overlay?.Gizmos == null)
                return;

            bool drawPending = _attached &&
                _activeTab == _selectionTab &&
                _selectionTab.DrawGizmos;
            bool drawStatistic = _attached &&
                _activeTab == _managementTab &&
                _managementTab.DrawGizmos;

            if (_overlay.Gizmos.DrawPending == drawPending &&
                _overlay.Gizmos.DrawStatistic == drawStatistic)
            {
                return;
            }

            _overlay.Gizmos.DrawPending = drawPending;
            _overlay.Gizmos.DrawStatistic = drawStatistic;
            SceneView.RepaintAll();
        }

        private void OnGizmoPreferenceChanged()
        {
            ApplyGizmoState();
            RefreshActiveTabData();
        }

        private void ExecuteOverlayAction(string undoName, Action<MagicLODOverlay> action)
        {
            if (action == null || !EnsureOverlayState())
                return;

            Undo.RecordObject(_overlay, undoName);
            action.Invoke(_overlay);
            EditorUtility.SetDirty(_overlay);
            _serializedOverlay.UpdateIfRequiredOrScript();
            ApplyGizmoState();
            RefreshActiveTabData();
            _activeTab?.Refresh();
            SceneView.RepaintAll();
        }

        private void ExecuteConfirmedAction(
            string title,
            string message,
            string confirmText,
            string undoName,
            Action<MagicLODOverlay> action)
        {
            if (EditorUtility.DisplayDialog(title, message, confirmText, "Cancel"))
                ExecuteOverlayAction(undoName, action);
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            _attached = true;

            if (_root?.parent is VisualElement contentRoot)
            {
                contentRoot.style.flexGrow = 1f;
                contentRoot.style.flexShrink = 1f;
                contentRoot.style.minHeight = 0f;
            }

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            // Panel geometry and toolbar focus are not stable inside AttachToPanelEvent.
            _root?.schedule.Execute(() =>
            {
                if (!_attached || _root?.panel != evt.destinationPanel)
                    return;

                RefreshPopupConstraints();
                if (isInToolbar)
                    FocusRootForPersistentPopup();

                ApplyGizmoState();
                RefreshActiveTabData();
                OnSelectionChanged();
            });
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            _attached = false;
            Selection.selectionChanged -= OnSelectionChanged;
            _activeTab?.OnPanelDetached();
            ApplyGizmoState();
        }

        private void OnSelectionChanged()
        {
            _activeTab?.OnSelectionChanged();
            _activeTab?.Refresh();
        }

        private void FocusRootForPersistentPopup()
        {
            if (_attached && isInToolbar && _root?.panel != null)
                _root.Focus();
        }

        private void OnRootFocusOut(FocusOutEvent evt)
        {
            if (!isInToolbar || _root == null)
                return;

            if (evt.relatedTarget is VisualElement target &&
                (target == _root || _root.Contains(target)))
            {
                return;
            }

            evt.StopImmediatePropagation();
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (_root == null)
                return;

            _root.EnableInClassList(NarrowLayoutClassName, evt.newRect.width < NarrowLayoutWidth);
            _root.EnableInClassList(ExtraNarrowLayoutClassName, evt.newRect.width < ExtraNarrowLayoutWidth);
            _root.EnableInClassList(ShortLayoutClassName, evt.newRect.height < ShortLayoutHeight);
        }

        private void RefreshPopupConstraints()
        {
            VisualElement canvasRoot = containerWindow?.rootVisualElement;
            if (canvasRoot == null)
                return;

            Vector2 canvasSize = canvasRoot.contentRect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f ||
                float.IsNaN(canvasSize.x) || float.IsNaN(canvasSize.y) ||
                float.IsInfinity(canvasSize.x) || float.IsInfinity(canvasSize.y))
            {
                return;
            }

            if ((_lastCanvasSize - canvasSize).sqrMagnitude < 0.25f)
                return;

            _lastCanvasSize = canvasSize;
            Vector2 availableSize = new Vector2(
                Mathf.Max(1f, canvasSize.x - CanvasHorizontalMargin),
                Mathf.Max(1f, canvasSize.y - CanvasVerticalMargin));
            minSize = new Vector2(
                Mathf.Min(MinimumOverlaySize.x, availableSize.x),
                Mathf.Min(MinimumOverlaySize.y, availableSize.y));
            maxSize = availableSize;
        }

        private void ConfigureTabIcons()
        {
            List<string> errors = new List<string>();
            Texture settingsIcon = EditorGUIUtility.IconContent("Settings")?.image;
            if (settingsIcon == null)
            {
                errors.Add(
                    "Unity's built-in Settings icon could not be loaded. The 'S' text fallback is active.");
            }

            ConfigureTabIcon(_settingsTab.Button, "Settings", settingsIcon, "S");
            ConfigureCustomTabIcon(
                _selectionTab.Button,
                "Scene selection",
                SelectionTabIconGuid,
                SelectionTabIconRelativePath,
                "Selection tab icon",
                "+",
                errors);
            ConfigureCustomTabIcon(
                _managementTab.Button,
                "Scene management",
                ManagementTabIconGuid,
                ManagementTabIconRelativePath,
                "Management tab icon",
                "M",
                errors);
            ConfigureCustomTabIcon(
                _objectTab.Button,
                "Selected object",
                ObjectTabIconGuid,
                ObjectTabIconRelativePath,
                "Selected Object tab icon",
                "O",
                errors);
            ShowAssetLoadingWarnings(errors);
        }

        private void ConfigureCustomTabIcon(
            Button button,
            string tooltip,
            string guid,
            string relativePath,
            string displayName,
            string fallback,
            List<string> errors)
        {
            Texture icon = MagicLODUIAssetLoader.Load<Texture2D>(
                typeof(MagicLODOverlayEditor),
                guid,
                relativePath,
                displayName,
                out string error);
            AddAssetError(errors, error);
            ConfigureTabIcon(button, tooltip, icon, fallback);
        }

        private VisualElement LoadLayout(out string error)
        {
            VisualTreeAsset visualTree = MagicLODUIAssetLoader.Load<VisualTreeAsset>(
                typeof(MagicLODOverlayEditor),
                OverlayLayoutGuid,
                OverlayLayoutRelativePath,
                "MagicLOD Overlay layout",
                out string layoutError);
            StyleSheet sharedStyles = MagicLODUIAssetLoader.Load<StyleSheet>(
                typeof(MagicLODOverlayEditor),
                SharedStylesGuid,
                SharedStylesRelativePath,
                "MagicLOD shared styles",
                out string sharedStylesError);
            StyleSheet overlayStyles = MagicLODUIAssetLoader.Load<StyleSheet>(
                typeof(MagicLODOverlayEditor),
                OverlayStylesGuid,
                OverlayStylesRelativePath,
                "MagicLOD Overlay styles",
                out string overlayStylesError);

            if (visualTree == null || sharedStyles == null || overlayStyles == null)
            {
                List<string> errors = new List<string>();
                AddAssetError(errors, layoutError);
                AddAssetError(errors, sharedStylesError);
                AddAssetError(errors, overlayStylesError);
                error = string.Join("\n\n", errors);
                return null;
            }

            TemplateContainer root = visualTree.CloneTree();
            root.AddToClassList("magic-lod-overlay");
            root.styleSheets.Add(sharedStyles);
            root.styleSheets.Add(overlayStyles);
            error = null;
            return root;
        }

        private static VisualElement CreateAssetLoadingError(string details)
        {
            string message = string.IsNullOrEmpty(details)
                ? "MagicLOD Overlay UI assets could not be loaded."
                : $"MagicLOD Overlay UI assets could not be loaded.\n\n{details}";
            HelpBox helpBox = new HelpBox(message, HelpBoxMessageType.Error);
            helpBox.style.marginTop = 8;
            helpBox.style.marginBottom = 8;
            return helpBox;
        }

        private void ShowAssetLoadingWarnings(List<string> errors)
        {
            if (errors == null || errors.Count == 0)
                return;

            string message =
                "Some MagicLOD Overlay resources could not be loaded. Text fallbacks are active.\n\n" +
                string.Join("\n\n", errors);
            HelpBox helpBox = new HelpBox(message, HelpBoxMessageType.Warning);
            helpBox.style.marginBottom = 6;

            VisualElement panel = _root?.Q<VisualElement>("overlay-panel");
            if (panel != null)
                panel.Insert(panel.childCount == 0 ? 0 : 1, helpBox);

            Debug.LogWarning(message);
        }

        private static void AddAssetError(List<string> errors, string error)
        {
            if (errors != null && !string.IsNullOrEmpty(error))
                errors.Add(error);
        }

        private static void ConfigureTabIcon(Button button, string tooltip, Texture icon, string fallback)
        {
            button.Clear();
            button.text = fallback;
            button.tooltip = tooltip;
            if (icon == null)
                return;

            button.text = string.Empty;
            Image image = new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.AddToClassList("overlay-tab__icon");
            button.Add(image);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element?.EnableInClassList(HiddenClassName, !visible);
        }


        private abstract class TabModule
        {
            protected MagicLODOverlayEditor Owner { get; }
            private VisualElement Root { get; }
            public VisualElement Page { get; }
            public Button Button { get; }
            public virtual bool UsesSceneData => false;

            protected TabModule(
                MagicLODOverlayEditor owner,
                VisualElement root,
                string pageName,
                string buttonName)
            {
                Owner = owner;
                Root = root;
                Page = root.Q<VisualElement>(pageName);
                Button = root.Q<Button>(buttonName);
            }

            public virtual void BindProperties(SerializedObject serializedOverlay) { }
            public virtual void Activate() { }
            public virtual void Deactivate() { }
            public virtual void Tick() { }
            public virtual void Refresh() { }
            public virtual void OnSelectionChanged() { }
            public virtual void OnPanelDetached() { }

            protected T Element<T>(string name) where T : VisualElement
            {
                return Root.Q<T>(name);
            }

            protected PropertyField BindField(string elementName, SerializedProperty property)
            {
                PropertyField field = Element<PropertyField>(elementName);
                if (field == null || property == null)
                    return field;

                field.Unbind();
                field.BindProperty(property);
                return field;
            }

            protected PropertyField BindRelativeField(
                string elementName,
                SerializedProperty parent,
                string propertyName)
            {
                return BindField(elementName, parent?.FindPropertyRelative(propertyName));
            }

            protected PropertyField BindBackingField(
                string elementName,
                SerializedProperty parent,
                string propertyName)
            {
                return BindField(elementName, FindBackingProperty(parent, propertyName));
            }

            protected static SerializedProperty FindBackingProperty(
                SerializedProperty parent,
                string propertyName)
            {
                return parent?.FindPropertyRelative($"<{propertyName}>k__BackingField");
            }

            protected static void SetLabel(Label label, int value)
            {
                if (label != null)
                    label.text = value.ToString("N0", CultureInfo.InvariantCulture);
            }

            protected void RegisterAction(
                string buttonName,
                string undoName,
                Action<MagicLODOverlay> action)
            {
                Element<Button>(buttonName).clicked +=
                    () => Owner.ExecuteOverlayAction(undoName, action);
            }
        }

        private sealed class SettingsTabModule : TabModule
        {
            private const string ActiveModeClassName = "mode-toolbar__item--active";

            private readonly Button _generateModeButton;
            private readonly Button _simplifyModeButton;
            private readonly VisualElement _autoLODSettingsCard;
            private readonly VisualElement _simplifySettingsCard;
            private readonly Toggle _applyToAllPendingToggle;
            private PropertyField _exportFolderField;
            private SerializedProperty _operationModeProperty;
            private SerializedProperty _exportMeshesProperty;
            private SerializedProperty _exportFolderProperty;

            public SettingsTabModule(MagicLODOverlayEditor owner, VisualElement root)
                : base(owner, root, "settings-page", "settings-tab")
            {
                _generateModeButton = Element<Button>("generate-mode-button");
                _simplifyModeButton = Element<Button>("simplify-mode-button");
                _autoLODSettingsCard = Element<VisualElement>("default-autolod-card");
                _simplifySettingsCard = Element<VisualElement>("default-simplify-card");
                _applyToAllPendingToggle = Element<Toggle>("apply-to-all-pending-toggle");

                _generateModeButton.clicked += () => SetSelectionMode(OperationMode.GenerateLODs);
                _simplifyModeButton.clicked += () => SetSelectionMode(OperationMode.Simplify);
                Element<Button>("browse-export-button").clicked += BrowseExportFolder;
            }

            public override void BindProperties(SerializedObject serializedOverlay)
            {
                Page.UnregisterCallback<SerializedPropertyChangeEvent>(OnSerializedSettingChanged);
                _applyToAllPendingToggle.UnregisterValueChangedCallback(OnApplyToAllPendingChanged);
                _applyToAllPendingToggle.Unbind();

                SerializedProperty settings = serializedOverlay.FindProperty("<Settings>k__BackingField");
                SerializedProperty applyToAllPendingProperty = FindBackingProperty(settings, "ApplyToAllPending");
                _operationModeProperty = FindBackingProperty(settings, "Mode");
                _exportMeshesProperty = FindBackingProperty(settings, "ExportMeshes");
                _exportFolderProperty = FindBackingProperty(settings, "ExportFolder");

                SerializedProperty globalSettings = FindBackingProperty(settings, "GlobalSettings");
                BindRelativeField("default-borders-penalty-field", globalSettings, "bordersPenaltyWeight");
                BindRelativeField("default-uv-seams-penalty-field", globalSettings, "uvSeamsPenaltyWeight");
                BindRelativeField("default-normal-seams-penalty-field", globalSettings, "normalSeamsPenaltyWeight");
                BindRelativeField("default-uv-field", globalSettings, "uvMode");
                BindRelativeField("default-uv2-field", globalSettings, "uv2Mode");
                BindRelativeField("default-uv3-field", globalSettings, "uv3Mode");
                BindRelativeField("default-uv4-field", globalSettings, "uv4Mode");
                BindRelativeField("default-normals-field", globalSettings, "normalsMode");
                BindRelativeField("default-tangents-field", globalSettings, "tangentsMode");
                BindRelativeField("default-colors-field", globalSettings, "colorsMode");
                BindRelativeField("default-bone-weights-field", globalSettings, "boneWeightsInterpolationMode");

                SerializedProperty screenSpace = FindBackingProperty(settings, "ScreenSpaceConfigurator");
                BindRelativeField("default-fade-mode-field", screenSpace, "_fadeMode");
                BindRelativeField("default-target-triangles-field", screenSpace, "_targetTrianglesPer100Pixels");
                BindRelativeField("default-min-mesh-quality-field", screenSpace, "_minMeshQuality");
                BindRelativeField("default-min-reduction-field", screenSpace, "_minReductionPercentage");
                BindRelativeField("default-max-lods-field", screenSpace, "_maxLODs");
                BindRelativeField("default-cull-height-field", screenSpace, "_cullHeight");
                BindRelativeField("default-no-cull-size-field", screenSpace, "_neverCullMaxSize");
                BindBackingField("default-max-error-field", settings, "MaxSimplificationError");
                BindField("export-meshes-field", _exportMeshesProperty);

                _exportFolderField = BindField("export-folder-field", _exportFolderProperty);

                _applyToAllPendingToggle.BindProperty(applyToAllPendingProperty);
                _applyToAllPendingToggle.RegisterValueChangedCallback(OnApplyToAllPendingChanged);
                Page.RegisterCallback<SerializedPropertyChangeEvent>(OnSerializedSettingChanged);
            }

            public override void Refresh()
            {
                SetVisible(_exportFolderField, _exportMeshesProperty?.boolValue ?? false);

                OperationMode selectionMode = GetSelectionMode();
                bool generatesLODs = selectionMode == OperationMode.GenerateLODs;
                _generateModeButton.EnableInClassList(ActiveModeClassName, generatesLODs);
                _simplifyModeButton.EnableInClassList(
                    ActiveModeClassName, selectionMode == OperationMode.Simplify);
                SetVisible(_autoLODSettingsCard, generatesLODs);
                SetVisible(_simplifySettingsCard, selectionMode == OperationMode.Simplify);
            }

            private void SetSelectionMode(OperationMode mode)
            {
                if (!Owner.EnsureOverlayState() || _operationModeProperty == null)
                    return;

                Owner._serializedOverlay.Update();
                Undo.RecordObject(Owner._overlay, "Change MagicLOD Selection Mode");
                _operationModeProperty.enumValueIndex = (int)mode;
                Owner._serializedOverlay.ApplyModifiedProperties();
                EditorUtility.SetDirty(Owner._overlay);
                Owner._overlay.OnGlobalSettingsChanged();
                Refresh();
            }

            private OperationMode GetSelectionMode()
            {
                return _operationModeProperty == null
                    ? OperationMode.GenerateLODs
                    : (OperationMode)_operationModeProperty.enumValueIndex;
            }

            private void BrowseExportFolder()
            {
                if (!Owner.EnsureOverlayState())
                    return;

                string currentFolder = _exportFolderProperty?.stringValue;
                string absoluteFolder = string.IsNullOrEmpty(currentFolder)
                    ? Application.dataPath
                    : Path.GetFullPath(currentFolder);
                string selectedFolder = EditorUtility.OpenFolderPanel(
                    "Select MagicLOD export folder",
                    absoluteFolder,
                    string.Empty);

                if (string.IsNullOrEmpty(selectedFolder) || _exportFolderProperty == null)
                    return;

                string normalizedDataPath = Application.dataPath.Replace('\\', '/');
                string normalizedSelection = selectedFolder.Replace('\\', '/');
                string projectPath = normalizedSelection.StartsWith(
                    normalizedDataPath,
                    StringComparison.OrdinalIgnoreCase)
                    ? $"Assets{normalizedSelection.Substring(normalizedDataPath.Length)}"
                    : normalizedSelection;

                Owner._serializedOverlay.Update();
                Undo.RecordObject(Owner._overlay, "Change MagicLOD Export Folder");
                _exportFolderProperty.stringValue = projectPath;
                Owner._serializedOverlay.ApplyModifiedProperties();
                EditorUtility.SetDirty(Owner._overlay);
                Owner._overlay.OnGlobalSettingsChanged();
            }

            private void OnSerializedSettingChanged(SerializedPropertyChangeEvent evt)
            {
                NotifyGlobalSettingsChanged();
            }

            private void OnApplyToAllPendingChanged(ChangeEvent<bool> evt)
            {
                NotifyGlobalSettingsChanged();
            }

            private void NotifyGlobalSettingsChanged()
            {
                if (!Owner.EnsureOverlayState())
                    return;

                Owner._serializedOverlay.ApplyModifiedProperties();
                EditorUtility.SetDirty(Owner._overlay);
                Owner._overlay.OnGlobalSettingsChanged();
            }
        }

        private sealed class SelectionTabModule : TabModule
        {
            private const string GizmosSessionKey = "NGS.MagicLOD.Overlay.PendingGizmos";

            private readonly PropertyField _minTrianglesField;
            private readonly PropertyField _minMeshDensityField;
            private readonly Button _clearAutoAddedButton;
            private readonly Button _clearAllPendingButton;
            private readonly Label _pendingSimplificationLabel;
            private readonly Label _pendingLODGenerationLabel;
            private SerializedProperty _selectionFilterProperty;
            private SerializedProperty _lastAutoAddedProperty;

            public override bool UsesSceneData => true;
            public bool DrawGizmos { get; private set; }

            public SelectionTabModule(MagicLODOverlayEditor owner, VisualElement root)
                : base(owner, root, "selection-page", "selection-tab")
            {
                _minTrianglesField = Element<PropertyField>("min-triangles-field");
                _minMeshDensityField = Element<PropertyField>("min-mesh-density-field");
                _clearAutoAddedButton = Element<Button>("clear-auto-added-button");
                _clearAllPendingButton = Element<Button>("clear-all-pending-button");
                _pendingSimplificationLabel = Element<Label>("pending-simplification-value");
                _pendingLODGenerationLabel = Element<Label>("pending-lod-generation-value");

                DrawGizmos = SessionState.GetBool(GizmosSessionKey, true);
                Toggle gizmosToggle = Element<Toggle>("pending-gizmos-toggle");
                gizmosToggle.SetValueWithoutNotify(DrawGizmos);
                gizmosToggle.RegisterValueChangedCallback(evt =>
                {
                    DrawGizmos = evt.newValue;
                    SessionState.SetBool(GizmosSessionKey, evt.newValue);
                    Owner.OnGizmoPreferenceChanged();
                });

                RegisterAction("find-and-add-button", "Auto Find And Add MagicLOD",
                    overlay => overlay.AutoFindAndAdd());
                RegisterAction("clear-auto-added-button", "Clear Auto Added MagicLOD",
                    overlay => overlay.ClearAutoAdded());
                RegisterAction("add-selection-button", "Add MagicLOD To Selection", overlay => overlay.AddSelection());
                RegisterAction("remove-selection-button", "Remove MagicLOD From Selection",
                    overlay => overlay.RemoveSelection());
                RegisterAction("clear-all-pending-button", "Clear All Pending MagicLOD",
                    overlay => overlay.ClearAllPending());
            }

            public override void BindProperties(SerializedObject serializedOverlay)
            {
                SerializedProperty selection = serializedOverlay.FindProperty("_selection");
                _selectionFilterProperty = FindBackingProperty(selection, "Filter");
                _lastAutoAddedProperty = selection?.FindPropertyRelative("_lastAutoAdded");
                BindField("selection-filter-field", _selectionFilterProperty);
                BindBackingField("min-triangles-field", selection, "MinTrianglesCount");
                BindBackingField("min-mesh-density-field", selection, "MinTrianglesPerUnit");
                BindBackingField("consider-prefabs-field", selection, "ConsiderPrefabs");
            }

            public override void Refresh()
            {
                SelectionFilter filter = GetSelectionFilter();
                SetVisible(_minTrianglesField, filter == SelectionFilter.TrianglesCount);
                SetVisible(_minMeshDensityField, filter == SelectionFilter.MeshDensity);

                bool hasAutoAdded = _lastAutoAddedProperty != null &&
                    _lastAutoAddedProperty.arraySize > 0;
                SetVisible(_clearAutoAddedButton, hasAutoAdded);

                MagicLODOverlay.StatisticModule statistic = Owner._overlay.Statistic;
                bool hasPending = statistic != null &&
                    (statistic.PendingSimplification > 0 ||
                     statistic.PendingLODGeneration > 0);
                SetVisible(_clearAllPendingButton, hasPending);
                _clearAllPendingButton.SetEnabled(hasPending);

                if (statistic == null)
                    return;

                SetLabel(_pendingSimplificationLabel, statistic.PendingSimplification);
                SetLabel(_pendingLODGenerationLabel, statistic.PendingLODGeneration);
            }

            private SelectionFilter GetSelectionFilter()
            {
                return _selectionFilterProperty == null
                    ? SelectionFilter.TrianglesCount
                    : (SelectionFilter)_selectionFilterProperty.enumValueIndex;
            }
        }

        private sealed class ManagementTabModule : TabModule
        {
            private const string GizmosSessionKey = "NGS.MagicLOD.Overlay.StatisticGizmos";

            private readonly VisualElement _managementContent;
            private readonly VisualElement _managementProgress;
            private readonly VisualElement _bakingCard;
            private readonly VisualElement _replaceMeshesCard;
            private readonly VisualElement _backupCard;
            private readonly Button _revertWithErrorsButton;
            private readonly Label _createdLODGroupsLabel;
            private readonly Label _createdSimplifiedMeshesLabel;
            private readonly Label _pendingSimplificationLabel;
            private readonly Label _pendingLODGenerationLabel;
            private readonly Label _sourcesWithErrorsLabel;
            private readonly Label _simplifySourceTrianglesLabel;
            private readonly Label _simplifiedTrianglesLabel;
            private readonly Label _lodSourceTrianglesLabel;
            private readonly Label _lowestLODTrianglesLabel;
            private readonly ProgressBar _progressBar;
            private readonly Label _progressDetail;

            public override bool UsesSceneData => true;
            public bool DrawGizmos { get; private set; }

            public ManagementTabModule(MagicLODOverlayEditor owner, VisualElement root)
                : base(owner, root, "management-page", "management-tab")
            {
                _managementContent = Element<VisualElement>("management-content");
                _managementProgress = Element<VisualElement>("management-progress");
                _bakingCard = Element<VisualElement>("baking-card");
                _replaceMeshesCard = Element<VisualElement>("replace-meshes-card");
                _backupCard = Element<VisualElement>("backup-card");
                _revertWithErrorsButton = Element<Button>("revert-with-errors-button");
                _createdLODGroupsLabel = Element<Label>("created-lod-groups-value");
                _createdSimplifiedMeshesLabel = Element<Label>("created-simplified-meshes-value");
                _pendingSimplificationLabel = Element<Label>("management-pending-simplification-value");
                _pendingLODGenerationLabel = Element<Label>("management-pending-lod-generation-value");
                _sourcesWithErrorsLabel = Element<Label>("sources-with-errors-value");
                _simplifySourceTrianglesLabel = Element<Label>("simplify-source-triangles-value");
                _simplifiedTrianglesLabel = Element<Label>("simplified-triangles-value");
                _lodSourceTrianglesLabel = Element<Label>("lod-source-triangles-value");
                _lowestLODTrianglesLabel = Element<Label>("lowest-lod-triangles-value");
                _progressBar = Element<ProgressBar>("management-progress-bar");
                _progressDetail = Element<Label>("management-progress-detail");

                DrawGizmos = SessionState.GetBool(GizmosSessionKey, true);
                Toggle gizmosToggle = Element<Toggle>("statistic-gizmos-toggle");
                gizmosToggle.SetValueWithoutNotify(DrawGizmos);
                gizmosToggle.RegisterValueChangedCallback(evt =>
                {
                    DrawGizmos = evt.newValue;
                    SessionState.SetBool(GizmosSessionKey, evt.newValue);
                    Owner.OnGizmoPreferenceChanged();
                });

                RegisterAction("bake-all-button", "Bake All MagicLOD", overlay => overlay.DecimateAll());
                RegisterAction("bake-selected-button", "Bake Selected MagicLOD", overlay => overlay.DecimateSelected());
                RegisterAction("cancel-management-button", "Cancel MagicLOD Decimation",
                    overlay => overlay.CancelDecimation());
                Element<Button>("replace-all-button").clicked += ReplaceAll;
                Element<Button>("replace-selected-button").clicked += ReplaceSelected;
                Element<Button>("revert-selected-button").clicked += RevertSelected;
                _revertWithErrorsButton.clicked += RevertWithErrors;
                Element<Button>("revert-all-button").clicked += RevertAll;
            }

            public override void Refresh()
            {
                MagicLODOverlay.StatisticModule statistic = Owner._overlay.Statistic;
                bool hasPending = statistic != null &&
                    (statistic.PendingSimplification > 0 ||
                     statistic.PendingLODGeneration > 0);
                SetVisible(_bakingCard, hasPending);
                SetVisible(_revertWithErrorsButton, statistic != null && statistic.SourcesWithErrors > 0);

                if (statistic != null)
                {
                    SetLabel(_createdLODGroupsLabel, statistic.CreatedLODGroups);
                    SetLabel(_createdSimplifiedMeshesLabel, statistic.CreatedSimplifiedMeshes);
                    SetLabel(_pendingSimplificationLabel, statistic.PendingSimplification);
                    SetLabel(_pendingLODGenerationLabel, statistic.PendingLODGeneration);
                    SetLabel(_sourcesWithErrorsLabel, statistic.SourcesWithErrors);
                    SetLabel(_simplifySourceTrianglesLabel, statistic.SimplifySourcesTriangles);
                    SetLabel(_simplifiedTrianglesLabel, statistic.TotalSimplifiedTriangles);
                    SetLabel(_lodSourceTrianglesLabel, statistic.LODSourcesTriangles);
                    SetLabel(_lowestLODTrianglesLabel, statistic.TotalLowestLODTriangles);
                }

                bool inProgress = EditorMeshDecimationProvider.IsInProgress;
                SetVisible(_managementContent, !inProgress);
                SetVisible(_managementProgress, inProgress);

                if (inProgress)
                {
                    float progress = Mathf.Clamp01(EditorMeshDecimationProvider.ProgressPercentage) * 100f;
                    int roundedProgress = Mathf.RoundToInt(progress);
                    _progressBar.value = progress;
                    _progressBar.title = $"{roundedProgress}%";
                    _progressDetail.text = $"Overall progress: {roundedProgress}%";
                }

                bool hasSimplifiedMeshes = statistic != null && statistic.CreatedSimplifiedMeshes > 0;
                bool hasBakedResults = hasSimplifiedMeshes ||
                    (statistic != null && statistic.CreatedLODGroups > 0);
                SetVisible(_replaceMeshesCard, hasSimplifiedMeshes);
                SetVisible(_backupCard, hasBakedResults);
            }

            private void ReplaceAll()
            {
                Owner.ExecuteConfirmedAction(
                    "Apply All Simplified Meshes",
                    "This permanently applies every eligible simplified result to its source mesh.",
                    "Apply All",
                    "Apply All Simplified Meshes",
                    overlay => overlay.ReplaceSimplifiedAll());
            }

            private void ReplaceSelected()
            {
                Owner.ExecuteConfirmedAction(
                    "Apply Selected Simplified Meshes",
                    "This permanently applies eligible selected simplified results to their source meshes.",
                    "Apply Selected",
                    "Apply Selected Simplified Meshes",
                    overlay => overlay.ReplaceSimplifiedSelected());
            }

            private void RevertSelected()
            {
                Owner.ExecuteConfirmedAction(
                    "Revert Selected MagicLOD",
                    "Restore all selected baked MagicLOD sources?",
                    "Revert Selected",
                    "Revert Selected MagicLOD",
                    overlay => overlay.RevertSelected());
            }

            private void RevertWithErrors()
            {
                Owner.ExecuteConfirmedAction(
                    "Revert MagicLOD Errors",
                    "Restore every baked MagicLOD source that contains errors?",
                    "Revert Errors",
                    "Revert MagicLOD Sources With Errors",
                    overlay => overlay.RevertSourcesWithErrors());
            }

            private void RevertAll()
            {
                Owner.ExecuteConfirmedAction(
                    "Revert All MagicLOD",
                    "Restore every baked MagicLOD source in the loaded scenes?",
                    "Revert All",
                    "Revert All MagicLOD",
                    overlay => overlay.RevertAll());
            }
        }

        private sealed class ObjectTabModule : TabModule
        {
            private readonly VisualElement _overlayHeader;
            private readonly VisualElement _objectContent;
            private readonly VisualElement _emptyState;
            private readonly VisualElement _addView;
            private readonly Label _addHint;
            private readonly VisualElement _removeCard;
            private VisualElement _inspectorHost;
            private VisualElement _componentInspector;
            private MagicLODEditorComponent _component;
            private UnityEditor.Editor _componentEditor;
            private GameObject _selectedGameObject;

            public ObjectTabModule(MagicLODOverlayEditor owner, VisualElement root)
                : base(owner, root, "object-page", "object-tab")
            {
                _overlayHeader = Element<VisualElement>("overlay-header");
                _objectContent = Element<VisualElement>("object-content");
                _emptyState = Element<VisualElement>("object-empty-state");
                _addView = Element<VisualElement>("object-add-view");
                _addHint = Element<Label>("object-add-hint");
                _inspectorHost = Element<VisualElement>("object-inspector-host");
                _removeCard = Element<VisualElement>("object-remove-card");
                EnsureInspectorHost();

                Element<Button>("add-magiclod-button").clicked += AddMagicLOD;
                Element<Button>("remove-magiclod-button").clicked += RemoveMagicLOD;
            }

            public override void Activate() => Rebuild();

            public override void Deactivate()
            {
                DisposeEditor();
                RefreshHeaderVisibility();
            }

            public override void Tick() => RefreshSelectedObject();

            public override void OnSelectionChanged() => Rebuild();

            public override void OnPanelDetached()
            {
                DisposeEditor();
                RefreshHeaderVisibility();
            }

            public void Dispose() => DisposeEditor();

            private void Rebuild()
            {
                DisposeEditor();
                ShowState(null);
                RefreshRemoveAction(null);

                GameObject selectedGameObject = Selection.activeGameObject;
                _selectedGameObject = selectedGameObject;

                if (selectedGameObject == null)
                {
                    ShowState(_emptyState);
                    RefreshHeaderVisibility();
                    return;
                }

                _component = selectedGameObject.GetComponent<MagicLODEditorComponent>();
                RefreshRemoveAction(_component);

                if (_component == null)
                {
                    _addHint.text = $"{selectedGameObject.name} does not have a MagicLOD component.";
                    ShowState(_addView);
                    RefreshHeaderVisibility();
                    return;
                }

                if (!EnsureInspectorHost())
                {
                    Debug.LogError("MagicLOD Overlay object content host could not be created.");
                    RefreshHeaderVisibility();
                    return;
                }

                _componentEditor = UnityEditor.Editor.CreateEditor(_component, typeof(MagicLODEditorComponentEditor));
                VisualElement inspector = _componentEditor?.CreateInspectorGUI();
                if (inspector == null)
                {
                    inspector = new HelpBox(
                        "MagicLOD component editor could not be created.",
                        HelpBoxMessageType.Error);
                }

                _componentInspector = inspector;
                _inspectorHost.Insert(0, _componentInspector);
                ShowState(_inspectorHost);
                RefreshHeaderVisibility();
            }

            private void RefreshSelectedObject()
            {
                GameObject selectedGameObject = Selection.activeGameObject;
                MagicLODEditorComponent component = selectedGameObject == null
                    ? null
                    : selectedGameObject.GetComponent<MagicLODEditorComponent>();

                if (!ReferenceEquals(selectedGameObject, _selectedGameObject) ||
                    component != _component ||
                    (component == null && _componentEditor != null) ||
                    (component != null && _componentEditor == null))
                {
                    Rebuild();
                }

                RefreshRemoveAction(component);
            }

            private void ShowState(VisualElement activeState)
            {
                SetVisible(_emptyState, activeState == _emptyState);
                SetVisible(_addView, activeState == _addView);
                SetVisible(_inspectorHost, activeState == _inspectorHost);
            }

            private void RefreshHeaderVisibility()
            {
                bool inspectorHasOwnHeader = Owner._activeTab == this && _componentEditor != null;
                SetVisible(_overlayHeader, !inspectorHasOwnHeader);
            }

            private void RefreshRemoveAction(MagicLODEditorComponent component)
            {
                bool canRemove = component != null &&
                    !component.IsDecimated &&
                    !component.IsDecimationInProgress;
                SetVisible(_removeCard, canRemove);
            }

            private bool EnsureInspectorHost()
            {
                if (_inspectorHost != null)
                    return true;

                if (_objectContent == null)
                    return false;

                _inspectorHost = new VisualElement
                {
                    name = "object-inspector-host"
                };
                _inspectorHost.AddToClassList(HiddenClassName);
                _objectContent.Add(_inspectorHost);
                return true;
            }

            private void AddMagicLOD()
            {
                Owner.ExecuteOverlayAction(
                    "Add MagicLOD To Selected Object",
                    overlay => overlay.AddMagicLODToSelectedGameObject());
                Rebuild();
            }

            private void RemoveMagicLOD()
            {
                MagicLODEditorComponent component = _component;
                if (component == null ||
                    component.IsDecimated ||
                    component.IsDecimationInProgress)
                {
                    Rebuild();
                    return;
                }

                DisposeEditor();
                Owner.ExecuteOverlayAction(
                    "Remove MagicLOD From Selected Object",
                    overlay => overlay.RemoveMagicLODFromSelectedGameObjects());
                Rebuild();
            }

            private void DisposeEditor()
            {
                _componentInspector?.RemoveFromHierarchy();
                _componentInspector = null;

                if (_componentEditor != null)
                    UnityEngine.Object.DestroyImmediate(_componentEditor);

                _componentEditor = null;
                _component = null;
            }
        }
    }
}
