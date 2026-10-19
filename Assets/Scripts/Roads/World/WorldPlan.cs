using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Roads
{
    public enum WorldSiteKind { Cloverstack, T, MergeSplit, Continuation, Diamond, HalfDiamond }

    // Freeways are 3+3 with 120 m corners; zone arterials 2+2 and streets 1+1, both with the street corner rule.
    public enum WorldRoadKind { Freeway, Arterial, Street }

    [Serializable]
    public class WorldRoad
    {
        public string name;
        public WorldRoadKind kind;
        [Tooltip("The route's corner points in map metres (x east, z north; the CBD at 0, 0). Each corner is rounded as far as its legs allow.")]
        public List<Vector2> points = new List<Vector2>();
        [Tooltip("Stretches built as tunnels, in metres along the road (x from, y to).")]
        public List<Vector2> tunnels = new List<Vector2>();
        [Tooltip("Not open in the real city yet (North East Link).")]
        public bool planned;
        [Tooltip("Long straights between interchanges get sweeping bends that return to their own line.")]
        public bool sweeps = true;
        [Tooltip("A tight technical bend set goes on the free straight nearest each of these points (map metres).")]
        public List<Vector2> technical = new List<Vector2>();
        [Tooltip("Stretches the road climbs to and holds, as on a mountain road: it climbs at its own grade to reach each height where it passes `from`, and comes back down to street level from `to`.")]
        public List<WorldLevel> levels = new List<WorldLevel>();
        [Tooltip("Steepest grade for the road's own climbs, as rise over run. Interchange level changes keep the freeway grade.")]
        public float grade = RoadDesign.FreewayGrade;
    }

    [Serializable]
    public class WorldLevel
    {
        public Vector2 from;
        public Vector2 to;
        public float height;
    }

    [Serializable]
    public class WorldSite
    {
        [Tooltip("The number on the approved skeleton review image; 0 for junctions added after it.")]
        public int number;
        public WorldSiteKind kind;
        public string name;
        [Tooltip("For a T or a merge / split, the road that ends here, then the road it ends on. For a cloverstack, the road that stays at street level, then the one that drops into a cutting. For a diamond, the freeway (it drops into a cutting), then the cross street; for a half diamond, the freeway, then the street that ends beside it.")]
        public List<string> roads = new List<string>();
        public Vector2 at;
    }

    // The open world's freeway skeleton: the source the world's roads are built from (ADR 0012). Edit the routes and
    // sites here; Build World Freeways Scene and Update Roads lay the roads out from it.
    [CreateAssetMenu(menuName = "MotoSquid/Roads/World Plan")]
    public class WorldPlan : ScriptableObject
    {
        public List<WorldRoad> roads = new List<WorldRoad>();
        public List<WorldSite> sites = new List<WorldSite>();
    }
}
