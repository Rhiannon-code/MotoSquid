using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.MagicLOD.Runtime.Components
{
    public enum OperationMode { Simplify, GenerateLODs }
    public enum OrderingMode { None, ByDistanceNear, ByDistanceFar }

    public class MagicLODRuntimeController : MonoBehaviour
    {
        private static Dictionary<int, MagicLODRuntimeController> _controllers;


        public int ControllerID
        {
            get
            {
                return _controllerID;
            }
            set
            {
                UnregisterController();
                
                _controllerID = value;

                RegisterController();
            }
        }
        public int MaxActiveRequests
        {
            get
            {
                return _maxActiveRequests;
            }
            set
            {
                _maxActiveRequests = Mathf.Max(1, value);

                if (_provider != null)
                    _provider.MaxActiveRequests = _maxActiveRequests;
            }
        }
        public int MaxCacheSize
        {
            get
            {
                return _maxCacheSize;
            }
            set
            {
                _maxCacheSize = Mathf.Max(1, value);
            }
        }
        public bool IsInProgress
        {
            get
            {
                if (_pendingRequests != null && _pendingRequests.Count > 0)
                    return true;

                if (_provider == null)
                    return false;

                return _provider.TotalRequests > 0;
            }
        }
        public DecimationSettings DecimationSettings
        {
            get
            {
                return _decimationSettings;
            }
            set
            {
                _decimationSettings = value;
            }
        }
        public ILODGroupConfigurator LODGroupConfigurator
        {
            get
            {
                return _lodGroupConfigurator;
            }
        }
        public float MaxSimplificationError
        {
            get
            {
                return _maxSimplificationError;
            }
            set
            {
                _maxSimplificationError = Mathf.Max(0.000001f, value);
            }
        }
        public OperationMode OperationMode
        {
            get
            {
                return _operationMode;
            }
        }
        public OrderingMode OrderingMode
        {
            get
            {
                return _orderingMode;
            }
        }
        public GameObject OrderingTarget
        {
            get
            {
                return _orderingTarget;
            }
        }
        public bool DonotDestroyOnLoad
        {
            get
            {
                return _dontDestroyOnLoad;
            }
            set
            {
                if (_dontDestroyOnLoad == value)
                    return;

                if (value == false)
                {
                    Debug.Log("MagicLODRuntimeController::DonotDestroyOnLoad can't reset don't destroy on load flag. You should destroy object manually");
                    return;
                }

                _dontDestroyOnLoad = value;

                DontDestroyOnLoad(gameObject);
            }
        }


        [SerializeField]
        private int _controllerID;

        [SerializeField]
        private int _maxActiveRequests;

        [SerializeField]
        private int _maxCacheSize;

        [SerializeField]
        private DecimationSettings _decimationSettings;

        [SerializeReference]
        private ILODGroupConfigurator _lodGroupConfigurator;

        [SerializeField]
        private float _maxSimplificationError;

        [SerializeField]
        private OperationMode _operationMode;

        [SerializeField]
        private OrderingMode _orderingMode;

        [SerializeField]
        private GameObject _orderingTarget;

        [SerializeField]
        private bool _dontDestroyOnLoad;

        private List<PendingRequest> _pendingRequests;
        private MeshDecimationProvider _provider;


        private void Reset()
        {
            InitializeDefaults();
        }

        private void Awake()
        {
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            _provider = new MeshDecimationProvider(_maxActiveRequests);
            _pendingRequests = new List<PendingRequest>(128);

            RegisterController();
        }

        private void Update()
        {
            if (_pendingRequests.Count > 0)
            {
                if (_orderingMode != OrderingMode.None)
                    _pendingRequests.Sort();

                for (int i = 0; i < _pendingRequests.Count; i++)
                    _provider.RequestDecimation(_pendingRequests[i].Request, _pendingRequests[i].Callback);

                _pendingRequests.Clear();
            }

            _provider.Tick();

            if (_provider.CacheSize > _maxCacheSize)
                _provider.ClearCache();
        }

        private void OnDestroy()
        {
            CancelImmediate();
            UnregisterController();
        }


        public static MagicLODRuntimeController GetInstance(int controllerID)
        {
            if (_controllers == null)
                _controllers = new Dictionary<int, MagicLODRuntimeController>();

            if (_controllers.TryGetValue(controllerID, out MagicLODRuntimeController registeredController))
                return registeredController;

            Debug.Log($"MagicLODRuntimeController::GetInstance({controllerID}) not found. Will be created default controller");

            return CreateNewController(controllerID);
        }

        private static MagicLODRuntimeController CreateNewController(int controllerID)
        {
            GameObject go = new GameObject($"MagicLODRuntimeController({controllerID})");
            go.SetActive(false);

            MagicLODRuntimeController controller = go.AddComponent<MagicLODRuntimeController>();

            controller._controllerID = controllerID;
            controller.InitializeDefaults();

            go.SetActive(true);
            return controller;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadDomain()
        {
            _controllers = null;
        }


        public void SetOperationModeGenerateLODs(ILODGroupConfigurator lodGroupConfigurator)
        {
            if (lodGroupConfigurator == null)
            {
                Debug.Log($"MagicLODRuntimeController{_controllerID}::SetOperationModeGenerateLODs, lodGroupConfigurator is null");
                return;
            }

            _operationMode = OperationMode.GenerateLODs;
            _lodGroupConfigurator = lodGroupConfigurator;
        }

        public void SetOperationModeSimplify(float maxSimplificationError = 0.001f)
        {
            _operationMode = OperationMode.Simplify;
            MaxSimplificationError = maxSimplificationError;
        }

        public void SetManualLODConfigurator(ManualLODConfigurator manualConfigurator = null)
        {
            if (manualConfigurator == null)
            {
                _lodGroupConfigurator = new ManualLODConfigurator();
                return;
            }

            _lodGroupConfigurator = manualConfigurator;
        }

        public void SetScreenSpaceLODConfigurator(ScreenSpaceLODConfigurator screenSpaceConfigurator = null)
        {
            if (screenSpaceConfigurator == null)
            {
                _lodGroupConfigurator = new ScreenSpaceLODConfigurator();
                return;
            }

            _lodGroupConfigurator = screenSpaceConfigurator;
        }

        public void SetOrderingNone()
        {
            _orderingMode = OrderingMode.None;
            _orderingTarget = null;
        }

        public void SetOrdering(OrderingMode orderingMode, GameObject orderingTarget)
        {
            if (orderingMode == OrderingMode.None || orderingTarget == null)
            {
                SetOrderingNone();
                return;
            }

            _orderingMode = orderingMode;
            _orderingTarget = orderingTarget;
        }

        public void RequestDecimation(MagicLODRuntimeComponent runtimeComponent, DecimationRequest request, Action<DecimationResult> callback)
        {
            if (runtimeComponent == null)
            {
                Debug.Log("MagicLODRuntimeController::RequestDecimation() trying to request decimation with empty runtime component");

                callback?.Invoke(new DecimationResult("runtimeComponent is null"));
                return;
            }

            float sortKey = 0f;

            if (_orderingMode != OrderingMode.None && _orderingTarget != null)
            {
                sortKey = (runtimeComponent.transform.position - _orderingTarget.transform.position).sqrMagnitude;

                if (_orderingMode == OrderingMode.ByDistanceFar)
                    sortKey = -sortKey;
            }

            _pendingRequests.Add(new PendingRequest
            {
                Request = request,
                Callback = callback,
                SortKey = sortKey
            });
        }

        public void CancelImmediate()
        {
            _provider.CancelImmediate();

            if (_pendingRequests == null)
                return;

            foreach (var pendingRequest in _pendingRequests)
                pendingRequest.Callback?.Invoke(new DecimationResult("Decimation Cancelled"));

            _pendingRequests.Clear();
        }


        private void InitializeDefaults()
        {
            _maxActiveRequests = 4;
            _maxCacheSize = 50;
            _decimationSettings = DecimationSettings.Default;
            _maxSimplificationError = 0.001f;
            _operationMode = OperationMode.GenerateLODs;
            _lodGroupConfigurator = new ScreenSpaceLODConfigurator();
            _orderingMode = OrderingMode.None;
            _dontDestroyOnLoad = false;
        }

        private void UnregisterController()
        {
            if (_controllers == null)
                return;

            if (_controllers.TryGetValue(_controllerID, out MagicLODRuntimeController registeredController))
            {
                if (registeredController == this)
                    _controllers.Remove(_controllerID);
            }
        }

        private void RegisterController()
        {
            if (_controllers == null)
                _controllers = new Dictionary<int, MagicLODRuntimeController>();

            _controllers[_controllerID] = this;
        }


        private struct PendingRequest : IComparable<PendingRequest>
        {
            public DecimationRequest Request;
            public Action<DecimationResult> Callback;
            public float SortKey;

            public int CompareTo(PendingRequest other)
            {
                return SortKey.CompareTo(other.SortKey);
            }
        }
    }
}