using System.Collections.Generic;
using System.Linq;

namespace MotoSquid.Roads
{
    // The World Plan laid out as roads: every route rounded into straights and arcs, both directions on one surface,
    // with each site's interchange placed on it (WorldInterchanges) and the routes straightened through them.
    public static class WorldPlanLayout
    {
        public static List<RoadPocLayout.RoadDefinition> Roads(IReadOnlyList<WorldRoad> roads, IReadOnlyList<WorldSite> sites, List<string> report = null)
        {
            var routes = roads.Where(r => r.points.Count >= 2).ToDictionary(r => r.name, r => new WorldRoute(r));
            foreach (var road in roads.Where(r => r.points.Count < 2)) report?.Add($"{road.name} has fewer than two points; skipped.");
            var features = new List<RoadPocLayout.RoadDefinition>();
            foreach (var site in sites) WorldInterchanges.Place(site, routes, features, report);
            // Sweeps keep clear of the other freeways and of every interchange's ramps (a sweep hugging a ramp joins its
            // junction surface), but not of zone streets, which are laid inside the freeways' final lines.
            var freeways = routes.Values.Where(r => r.Kind == WorldRoadKind.Freeway).ToList();
            var ramps = features.Select(f => (IReadOnlyList<UnityEngine.Vector2>)f.Knots.Select(k => new UnityEngine.Vector2(k.Position.x, k.Position.z)).ToList()).ToList();
            var built = routes.Values.Select(r => r.Build(report, freeways.Where(o => o != r).Select(o => o.Points).Concat(ramps).ToList())).ToList();
            built.AddRange(features);
            return built;
        }
    }
}
