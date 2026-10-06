using MotoSquid.Track;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Race
{
public class CheckpointTracker : MonoBehaviour
{
    public WaypointPath waypointPath;

    [Header("Shortcut Awareness (optional)")]
    public RouteGraph routeGraph;

    public int                      CurrentRoad      { get; private set; } = -1;
    public int                      CurrentWaypoint  { get; private set; }
    public RaceWaypoint[]  CurrentWaypoints { get; private set; }
    public bool                     IsReady          => CurrentRoad >= 0 && CurrentWaypoints != null;

    private HashSet<int> _triggerOnlyRoadIndices;
    private int _minRoadIndex;
    private Rigidbody _rb;

    private float _transitionTimer;
    const float   TRANSITION_INTERVAL = 0.5f;

    void Start()
    {
        if (routeGraph == null) routeGraph = FindFirstObjectByType<RouteGraph>();

        if (waypointPath == null) waypointPath = FindFirstObjectByType<WaypointPath>();

        if (waypointPath == null)
            Debug.LogError("[CheckpointTracker] No WaypointGenerator in the scene. Respawns, off-track " +
                           "detection and the wrong-way warning are all dead on this racer.", this);

        _rb = GetComponentInParent<Rigidbody>();
        BuildTriggerOnlyRoads();
        StartCoroutine(InitialiseNextFrame());
    }

    private void BuildTriggerOnlyRoads()
    {
        _triggerOnlyRoadIndices = new HashSet<int>();
        if (routeGraph == null || routeGraph.segments == null) return;
        foreach (var seg in routeGraph.segments)
        {
            if (seg != null && seg.requiresTrigger
                && seg.source == RouteSegmentDef.SourceType.RoadWaypoints)
                _triggerOnlyRoadIndices.Add(seg.roadIndex);
        }
    }

    public void ForceRoad(int roadIndex)
    {
        var wps = waypointPath?.GetWaypoints(roadIndex);
        if (wps == null || wps.Length < 2) return;
        CurrentRoad      = roadIndex;
        CurrentWaypoints = wps;
        _transitionTimer = 0f;

        float bestSq = float.MaxValue;
        int   bestWp = 0;
        for (int i = 0; i < wps.Length; i++)
        {
            float sq = (transform.position - wps[i].position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; bestWp = i; }
        }
        CurrentWaypoint = bestWp;
    }

    void Update() => TryAdvance();

    public Vector3 TrackForwardDirection()
    {
        if (CurrentWaypoints == null || CurrentWaypoints.Length < 2) return Vector3.zero;
        int i = Mathf.Clamp(CurrentWaypoint, 0, CurrentWaypoints.Length - 2);
        Vector3 fwd = CurrentWaypoints[i + 1].position - CurrentWaypoints[i].position;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.zero;
    }

    public bool TryGetTrackHeight(Vector3 pos, out float height)
        {
            height = 0f;
            if (CurrentRoad < 0 || waypointPath == null) return false;
            if (!waypointPath.TryGetClosestPoint(CurrentRoad, pos, out Vector3 closest)) return false;
            height = closest.y;
            return true;
        }

        public float HorizontalDistanceToTrack(Vector3 pos)
    {
        int count = waypointPath?.roadWaypoints?.Count ?? 0;
        if (count == 0) return 0f;

        float best = float.MaxValue;
        for (int r = 0; r < count; r++)
        {
            if (IsTriggerOnlyRoad(r) && r != CurrentRoad) continue;

            float d = HorizontalDistanceToRoad(pos, r);
            if (d < best) best = d;
        }

        return best == float.MaxValue ? 0f : best;
    }

    private float HorizontalDistanceToRoad(Vector3 pos, int roadIndex)
        {
            if (!waypointPath.TryGetClosestPoint(roadIndex, pos, out Vector3 closest)) return float.MaxValue;
            return Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(closest.x, closest.z));
        }

        private IEnumerator InitialiseNextFrame()
    {
        yield return null;
        Initialise();
    }

    // Public so ResetBike can re snap the tracker after teleporting the bike on respawn
    public void Initialise()
    {
        FindNearestGlobal(transform.position, out int road, out int wp, _minRoadIndex);
        CurrentRoad      = road;
        CurrentWaypoint  = wp;
        CurrentWaypoints = road >= 0 ? waypointPath.GetWaypoints(road) : null;
        _transitionTimer = 0f;
    }

    private void TryAdvance()
    {
        if (CurrentWaypoints == null || CurrentWaypoints.Length < 2) return;

        int next = CurrentWaypoint + 1;

        if (next >= CurrentWaypoints.Length)
        {
            TryTransitionRoad();
            return;
        }

        Vector3 toNext = CurrentWaypoints[next].position - CurrentWaypoints[CurrentWaypoint].position;
        Vector3 toBike = transform.position - CurrentWaypoints[CurrentWaypoint].position;
        if (Vector3.Dot(toBike, toNext) >= toNext.sqrMagnitude)
            CurrentWaypoint = next;
    }

    private void TryTransitionRoad()
    {
        _transitionTimer -= Time.deltaTime;
        if (_transitionTimer > 0f) return;
        _transitionTimer = TRANSITION_INTERVAL;


        Vector3 junctionPos = CurrentWaypoints != null && CurrentWaypoints.Length > 0
            ? CurrentWaypoints[CurrentWaypoint].position
            : transform.position;

        FindNearestGlobal(junctionPos, out int road, out int wp, _minRoadIndex);
        if (road < 0) return;
        if (road == CurrentRoad)
        {
            FindNearestGlobal(transform.position, out road, out wp, _minRoadIndex);
            if (road < 0 || road == CurrentRoad) return;
        }

        int numRoads = waypointPath?.roadWaypoints?.Count ?? 0;
        bool isLapWrap = numRoads > 0 && CurrentRoad == numRoads - 1 && road == 0;
        _minRoadIndex = isLapWrap ? 0 : Mathf.Max(_minRoadIndex, road);

        CurrentRoad      = road;
        CurrentWaypoint  = wp;
        CurrentWaypoints = waypointPath.GetWaypoints(road);
    }

    // The bike root's rotation is frozen (heading lives on the Rotator), so its forward is only the spawn/respawn facing
    private Vector3 TravelDirection()
    {
        if (_rb != null)
        {
            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > 1f) return v;
        }
        return transform.forward;
    }

    private bool IsTriggerOnlyRoad(int r)
        => _triggerOnlyRoadIndices != null && _triggerOnlyRoadIndices.Contains(r);

    private void FindNearestGlobal(Vector3 pos, out int nearestRoad, out int nearestWp,
                                   int minRoad = 0)
    {
        nearestRoad = -1;
        nearestWp   = 0;
        float bestSq = float.MaxValue;
        Vector3 travelFwd = TravelDirection();

        int count = waypointPath?.roadWaypoints?.Count ?? 0;

        for (int r = 0; r < count; r++)
        {
            if (r < minRoad) continue;
            if (IsTriggerOnlyRoad(r) && r != CurrentRoad) continue;

            var wps = waypointPath.GetWaypoints(r);
            if (wps == null || wps.Length < 2) continue;
            for (int i = 0; i < wps.Length; i++)
            {
                float sq = (pos - wps[i].position).sqrMagnitude;
                if (sq >= bestSq) continue;

                int j = i < wps.Length - 1 ? i : i - 1;
                Vector3 roadFwd = wps[j + 1].position - wps[j].position;
                if (Vector3.Dot(roadFwd, travelFwd) < 0f) continue;

                bestSq = sq; nearestRoad = r; nearestWp = i;
            }
        }

        if (nearestRoad >= 0) return;

        for (int r = 0; r < count; r++)
        {
            if (r < minRoad) continue;
            if (IsTriggerOnlyRoad(r) && r != CurrentRoad) continue;

            var wps = waypointPath.GetWaypoints(r);
            if (wps == null || wps.Length < 2) continue;
            for (int i = 0; i < wps.Length; i++)
            {
                float sq = (pos - wps[i].position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; nearestRoad = r; nearestWp = i; }
            }
        }
    }
}

}
