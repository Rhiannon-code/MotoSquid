#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;
using NGS.MagicLOD.Runtime;

namespace NGS.MagicLOD.Editors.API
{
    public static class EditorMeshDecimationProvider
    {
        public static int MaxActiveRequests
        {
            get
            {
                return _provider.MaxActiveRequests;
            }
            set
            {
                _provider.MaxActiveRequests = value;
            }
        }
        public static int MaxCacheSize
        {
            get
            {
                return _maxCacheSize;
            }
            set
            {
                _maxCacheSize = Mathf.Max(value, 10);
            }
        }

        public static bool IsInProgress
        {
            get
            {
                return _provider.TotalRequests > 0;
            }
        }
        public static float ProgressPercentage
        {
            get
            {
                if (!IsInProgress)
                    return 0;

                int processed = _sessionTotalTasks - _provider.TotalRequests;
                return (float)processed / _sessionTotalTasks;
            }
        }

        private static MeshDecimationProvider _provider;
        private static int _maxCacheSize;

        private static bool _isTicking;
        private static int _progressId;
        private static int _sessionTotalTasks;


        static EditorMeshDecimationProvider()
        {
            _provider = new MeshDecimationProvider();
            _maxCacheSize = 50;

            _progressId = -1;
            _sessionTotalTasks = 0;
            _isTicking = false;

            MaxActiveRequests = 10;

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void RequestDecimation(DecimationRequest request, Action<DecimationResult> callback)
        {
            _sessionTotalTasks++;
            _provider.RequestDecimation(request, callback);

            EnsureTicking(true);
        }

        public static DecimationResult CompleteRequest(DecimationRequest request)
        {
            return _provider.CompleteRequest(request);
        }

        public static void ForceTick()
        {
            EditorUpdateTick();
        }

        public static void Cancel()
        {
            _provider.Cancel();

            CancelProgress();
        }

        public static void Cancel(DecimationRequest request)
        {
            _provider.Cancel(request);
        }

        public static void CancelImmediate()
        {
            _provider.CancelImmediate();

            CancelProgress();

            EnsureTicking(false);
        }

        public static void ClearCache()
        {
            _provider.ClearCache();
        }


        private static void EditorUpdateTick()
        {
            if (_provider == null)
                return;

            _provider.Tick();

            if (_sessionTotalTasks > 0)
            {
                if (_progressId == -1)
                    StartProgress();

                UpdateProgress();
            }

            EnforceCacheLimit();

            if (_provider.TotalRequests == 0)
            {
                EnsureTicking(false);
            }
        }

        private static void EnsureTicking(bool state)
        {
            if (_isTicking == state)
                return;

            _isTicking = state;

            if (_isTicking)
            {
                EditorApplication.update += EditorUpdateTick;
            }
            else
            {
                EditorApplication.update -= EditorUpdateTick;
            }
        }

        private static void EnforceCacheLimit()
        {
            if (_provider.CacheSize > MaxCacheSize)
            {
                _provider.ClearCache();
            }
        }

        private static void StartProgress()
        {
            if (_progressId != -1)
                return;

            _progressId = Progress.Start("MagicLOD Decimation", "Processing meshes...");
            Progress.RegisterCancelCallback(_progressId, OnProgressCanceled);
        }

        private static void UpdateProgress()
        {
            if (_progressId == -1)
                return;

            if (_provider.TotalRequests > 0)
            {
                int processed = _sessionTotalTasks - _provider.TotalRequests;
                float progressPercentage = (float)processed / _sessionTotalTasks;

                Progress.Report(_progressId, progressPercentage, $"Processed {processed} / {_sessionTotalTasks}");
            }
            else
            {
                FinishProgress();
            }
        }

        private static void FinishProgress()
        {
            if (_progressId != -1)
            {
                Progress.Finish(_progressId, Progress.Status.Succeeded);

                _progressId = -1;
            }

            _sessionTotalTasks = 0;
        }

        private static void CancelProgress()
        {
            if (_progressId != -1)
            {
                Progress.Finish(_progressId, Progress.Status.Canceled);
                _progressId = -1;
            }

            _sessionTotalTasks = 0;
        }


        private static bool OnProgressCanceled()
        {
            Cancel();
            return true;
        }

        private static void OnBeforeAssemblyReload()
        {
            CancelImmediate();

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CancelImmediate();
            }
        }
    }
}

#endif
