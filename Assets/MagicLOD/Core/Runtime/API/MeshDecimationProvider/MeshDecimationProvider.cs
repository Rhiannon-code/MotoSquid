using System;
using System.Collections.Generic;
using UnityEngine;


namespace NGS.MagicLOD.Runtime
{
    public class MeshDecimationProvider
    {
        public int TotalRequests
        {
            get
            {
                return _requestsQueue.TotalRequests;
            }
        }
        public int MaxActiveRequests
        {
            get
            {
                return _requestsQueue.MaxActiveRequests;
            }
            set
            {
                _requestsQueue.MaxActiveRequests = value;
            }
        }
        public int CacheSize
        {
            get
            {
                return _requestsCache.CacheSize;
            }
        }

        private RequestsCache _requestsCache;
        private RequestsCallbacks _requestsCallbacks;
        private RequestsQueue _requestsQueue;


        public MeshDecimationProvider(int maxActiveRequests = 4)
        {
            _requestsCache = new RequestsCache();
            _requestsCallbacks = new RequestsCallbacks(_requestsCache);
            _requestsQueue = new RequestsQueue(_requestsCache, _requestsCallbacks, maxActiveRequests);
        }

        public bool IsCompleted(DecimationRequest request)
        {
            return _requestsQueue.IsCompleted(request);
        }

        public void RequestDecimation(DecimationRequest request, Action<DecimationResult> callback)
        {
            _requestsQueue.EnqueueRequest(request);
            _requestsCallbacks.AddCallback(request, callback);
        }

        public DecimationResult CompleteRequest(DecimationRequest request)
        {
            return _requestsQueue.CompleteRequest(request);
        }

        public void Tick()
        {
            _requestsQueue.Tick();
            _requestsCallbacks.Tick();
        }

        public void Cancel()
        {
            _requestsQueue.Cancel();
            _requestsCallbacks.Cancel();
        }

        public void Cancel(DecimationRequest request)
        {
            _requestsQueue.Cancel(request);
            _requestsCallbacks.Cancel(request);
        }

        public void CancelImmediate()
        {
            _requestsQueue.CancelImmediate();
            _requestsCallbacks.Cancel();
        }

        public void ClearCache()
        {
            _requestsCache.Clear();
        }



        public class RequestsCache
        {
            public int CacheSize
            {
                get
                {
                    return _cache.Count;
                }
            }

            private Dictionary<DecimationRequest, DecimationResult> _cache;


            public RequestsCache()
            {
                _cache = new Dictionary<DecimationRequest, DecimationResult>();
            }

            public void PutInCache(DecimationRequest request, DecimationResult result)
            {
                _cache[request] = result;
            }

            public bool TryLoadFromCache(DecimationRequest request, out DecimationResult result)
            {
                if (!_cache.TryGetValue(request, out result))
                    return false;

                if (!IsResultValid(result))
                {
                    _cache.Remove(request);

                    result = default;
                    return false;
                }

                return true;
            }

            public bool Contains(DecimationRequest request)
            {
                if (_cache.TryGetValue(request, out DecimationResult result))
                {
                    if (IsResultValid(result))
                        return true;

                    _cache.Remove(request);
                }

                return false;
            }

            public void Clear()
            {
                _cache.Clear();
            }


            private bool IsResultValid(DecimationResult result)
            {
                if (result.taskStatus == DecimationStatus.Failed)
                    return true;

                if (result.decimatedMeshes == null)
                    return false;

                foreach (var mesh in result.decimatedMeshes)
                {
                    if (mesh == null)
                        return false;
                }

                return true;
            }
        }

        public class RequestsQueue
        {
            public int TotalRequests
            {
                get
                {
                    return _requestsQueue.Count + _activeRequests.Count;
                }
            }
            public int ActiveRequests
            {
                get
                {
                    return _activeRequests.Count;
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
                }
            }

            private Queue<DecimationRequest> _requestsQueue;
            private List<DecimationRequest> _activeRequests;
            private List<MeshDecimator.Handle> _activeRequestsHandles;
            private int _maxActiveRequests;

            private RequestsCache _requestsCache;
            private RequestsCallbacks _requestsCallbacks;


            public RequestsQueue(RequestsCache cache, RequestsCallbacks callbacks, int maxRequestsInWork)
            {
                _requestsCache = cache;
                _requestsCallbacks = callbacks;

                MaxActiveRequests = maxRequestsInWork;

                _requestsQueue = new Queue<DecimationRequest>();
                _activeRequests = new List<DecimationRequest>();
                _activeRequestsHandles = new List<MeshDecimator.Handle>();
            }

            public bool IsInQueue(DecimationRequest request)
            {
                return _requestsQueue.Contains(request) || _activeRequests.Contains(request);
            }

            public bool IsCompleted(DecimationRequest request)
            {
                return _requestsCache.Contains(request);
            }

            public void EnqueueRequest(DecimationRequest request)
            {
                _requestsCallbacks.SetDirty();

                if (IsCompleted(request))
                    return;

                if (IsInQueue(request))
                    return;

                _requestsQueue.Enqueue(request);
            }

            public DecimationResult CompleteRequest(DecimationRequest request)
            {
                _requestsCallbacks.SetDirty();

                if (_requestsCache.TryLoadFromCache(request, out DecimationResult result))
                    return result;

                int index = _activeRequests.IndexOf(request);

                if (index == -1)
                {
                    StartRequest(request);
                    index = _activeRequests.Count - 1;
                }

                return CompleteActiveRequest(index);
            }

            public void Tick()
            {
                if (_activeRequests != null && _activeRequests.Count > 0)
                {
                    int i = 0;
                    while (i < _activeRequests.Count)
                    {
                        MeshDecimator.Handle handle = _activeRequestsHandles[i];

                        if (handle.IsCompleted())
                        {
                            CompleteActiveRequest(i);
                            continue;
                        }

                        i++;
                    }
                }

                if (_requestsQueue != null)
                {
                    while (_requestsQueue.Count > 0 && _activeRequests.Count < _maxActiveRequests)
                    {
                        DecimationRequest request = _requestsQueue.Dequeue();

                        if (IsCompleted(request))
                            continue;

                        StartRequest(request);
                    }
                }
            }

            public void Cancel()
            {
                _requestsQueue.Clear();
            }

            public void Cancel(DecimationRequest request)
            {
                int count = _requestsQueue.Count;

                for (int i = 0; i < count; i++)
                {
                    DecimationRequest current = _requestsQueue.Dequeue();

                    if (current.Equals(request))
                        continue;

                    _requestsQueue.Enqueue(current);
                }
            }

            public void CancelImmediate()
            {
                while (_activeRequests.Count > 0)
                {
                    CompleteActiveRequest(0);
                }

                _requestsQueue.Clear();
            }


            private void StartRequest(DecimationRequest request)
            {
                MeshDecimator.Handle handle = default;

                if (request.quality == null)
                {
                    handle = MeshDecimator.StartSimplificationTask(request.originalMesh, request.settings, request.maxSimplificationError);
                }
                else
                {
                    handle = MeshDecimator.StartDecimationTask(request.originalMesh, request.settings, request.quality);
                }

                _activeRequests.Add(request);
                _activeRequestsHandles.Add(handle);
            }

            private DecimationResult CompleteActiveRequest(int index)
            {
                _requestsCallbacks.SetDirty();

                DecimationResult result = _activeRequestsHandles[index].CompleteAndGetResult();

                _requestsCache.PutInCache(_activeRequests[index], result);

                _activeRequests.RemoveAt(index);
                _activeRequestsHandles.RemoveAt(index);

                return result;
            }
        }

        public class RequestsCallbacks
        {
            private List<DecimationRequest> _requests;
            private Dictionary<DecimationRequest, Action<DecimationResult>> _callbacks;

            private RequestsCache _requestsCache;
            private bool _requestsCompletedDirty;

            public RequestsCallbacks(RequestsCache requestsCache)
            {
                _requests = new List<DecimationRequest>();
                _callbacks = new Dictionary<DecimationRequest, Action<DecimationResult>>();
                _requestsCache = requestsCache;
            }

            public void SetDirty()
            {
                _requestsCompletedDirty = true;
            }

            public void AddCallback(DecimationRequest request, Action<DecimationResult> callback)
            {
                if (_callbacks.TryGetValue(request, out Action<DecimationResult> action))
                {
                    _callbacks[request] = action + callback;
                }
                else
                {
                    _requests.Add(request);
                    _callbacks.Add(request, callback);
                }
            }

            public void Tick()
            {
                if (!_requestsCompletedDirty || _requests.Count == 0)
                    return;

                int i = 0; 
                while (i < _requests.Count)
                {
                    DecimationRequest request = _requests[i];

                    if (_requestsCache.TryLoadFromCache(request, out DecimationResult result))
                    {
                        Action<DecimationResult> callback = _callbacks[request];

                        try
                        {
                            callback.Invoke(result);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }

                        _callbacks.Remove(request);
                        _requests.RemoveAt(i);
                        continue;
                    }

                    i++;
                }

                _requestsCompletedDirty = false;
            }

            public void Cancel()
            {
                SetDirty();
                Tick();

                int i = 0;
                while (i < _requests.Count)
                {
                    DecimationRequest request = _requests[i];
                    Action<DecimationResult> callback = _callbacks[request];

                    try
                    {
                        callback.Invoke(new DecimationResult("Decimation Process Cancelled"));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    _callbacks.Remove(request);
                    _requests.RemoveAt(i);
                }
            }

            public void Cancel(DecimationRequest request)
            {
                SetDirty();
                Tick();

                if (!_callbacks.TryGetValue(request, out Action<DecimationResult> callback))
                    return;

                try
                {
                    callback.Invoke(new DecimationResult("Decimation Process Cancelled"));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                _callbacks.Remove(request);
                _requests.Remove(request);
            }
        }
    }
}
