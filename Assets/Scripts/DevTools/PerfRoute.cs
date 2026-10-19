using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MotoSquid.Roads;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace MotoSquid.DevTools
{
    // A repeatable benchmark ride: the main camera follows the listed roads at rider eye height and a fixed speed, then
    // holds a high view over the whole city, logging every frame (PerfHud's numbers) to a CSV and the Console summary.
    // F5 starts and stops it. Same roads, same speed, same views every time, so runs compare.
    [DefaultExecutionOrder(10000)]   // after the camera rig, so the ride wins
    public class PerfRoute : MonoBehaviour
    {
        public string[] roads = { "Zone 8 boulevard", "Zone 3 boulevard", "Zone 4 boulevard", "Princes / Outer Loop", "Zone 10 boulevard" };
        public float speed = 80f;
        public float maxPerRoad = 2500f;
        public float eyeHeight = 1.6f;
        public int settleFrames = 30;
        public Vector3 overviewPosition = new Vector3(0f, 300f, -6500f);
        public Vector3 overviewTarget = Vector3.zero;
        public float overviewSeconds = 6f;
        public bool runOnStart;

        readonly List<(SplineContainer path, float length, string name)> legs = new List<(SplineContainer, float, string)>();
        readonly List<(string part, int settle, PerfHud.Sample s)> rows = new List<(string, int, PerfHud.Sample)>();
        PerfHud hud;
        Camera cam;
        Behaviour brain;
        bool running;
        int leg, settle;
        float along, overviewLeft;

        void Start()
        {
            hud = FindAnyObjectByType<PerfHud>() ?? gameObject.AddComponent<PerfHud>();
            if (runOnStart) Begin();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            {
                if (running) Finish("stopped");
                else Begin();
            }
        }

        void Begin()
        {
            cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Perf] No main camera."); return; }
            var all = FindObjectsByType<Road>(FindObjectsSortMode.None);
            legs.Clear();
            foreach (var name in roads)
            {
                var road = all.FirstOrDefault(r => r.name == name);
                if (road == null) { Debug.LogWarning($"[Perf] No road called '{name}' in the scene; skipped."); continue; }
                var path = road.GetComponent<SplineContainer>();
                legs.Add((path, Mathf.Min(path.CalculateLength(), maxPerRoad), name));
            }
            if (legs.Count == 0) { Debug.LogWarning("[Perf] None of the route's roads are in the scene."); return; }
            brain = cam.GetComponents<Behaviour>().FirstOrDefault(b => b.GetType().Name == "CinemachineBrain");
            if (brain != null) brain.enabled = false;
            rows.Clear();
            leg = 0; along = 0f; settle = settleFrames; overviewLeft = overviewSeconds;
            running = true;
            Debug.Log($"[Perf] Benchmark ride started: {legs.Count} roads, {legs.Sum(l => l.length) / 1000f:0.0} km at {speed:0} m/s, then {overviewSeconds:0} s overview.");
        }

        void LateUpdate()
        {
            if (!running) return;
            string part;
            if (leg < legs.Count)
            {
                var (path, length, name) = legs[leg];
                float t = Mathf.Clamp01(path.Spline.ConvertIndexUnit(along, PathIndexUnit.Distance, PathIndexUnit.Normalized));
                Vector3 at = path.EvaluatePosition(t), ahead = path.EvaluateTangent(t);
                cam.transform.SetPositionAndRotation(at + Vector3.up * eyeHeight, Quaternion.LookRotation(new Vector3(ahead.x, 0f, ahead.z).normalized, Vector3.up));
                part = name;
                along += speed * Time.unscaledDeltaTime;
                if (along > length) { leg++; along = 0f; settle = settleFrames; }
            }
            else
            {
                cam.transform.SetPositionAndRotation(overviewPosition, Quaternion.LookRotation(overviewTarget - overviewPosition, Vector3.up));
                part = "overview";
                overviewLeft -= Time.unscaledDeltaTime;
            }
            rows.Add((part, settle > 0 ? 1 : 0, hud.Latest));
            if (settle > 0) settle--;
            if (leg >= legs.Count && overviewLeft <= 0f) Finish("finished");
        }

        void Finish(string how)
        {
            running = false;
            if (brain != null) brain.enabled = true;
            var csv = new StringBuilder("part,settling,frame_ms,cpu_main_ms,cpu_render_ms,gpu_ms,draw_calls,batches,set_pass,shadow_casters,triangles\n");
            foreach (var (part, settling, s) in rows)
                csv.AppendLine($"\"{part}\",{settling},{s.frameMs:0.00},{s.cpuMainMs:0.00},{s.cpuRenderMs:0.00},{s.gpuMs:0.00},{s.drawCalls},{s.batches},{s.setPass},{s.shadowCasters},{s.triangles}");
            string file = Path.Combine(Application.persistentDataPath, $"perf-{System.DateTime.Now:yyyyMMdd-HHmmss}.csv");
            File.WriteAllText(file, csv.ToString());

            var report = new StringBuilder($"[Perf] Benchmark ride {how} at {Screen.width}x{Screen.height}: {rows.Count} frames, log {file}\n");
            foreach (var group in rows.Where(r => r.settle == 0).GroupBy(r => r.part == "overview" ? "overview" : "riding"))
            {
                var frame = group.Select(r => r.s.frameMs).OrderBy(v => v).ToList();
                var gpu = group.Select(r => r.s.gpuMs).Where(v => v > 0f).OrderBy(v => v).ToList();
                float P(List<float> v, float q) => v.Count == 0 ? 0f : v[Mathf.Min(v.Count - 1, (int)(q * v.Count))];
                report.AppendLine($"  {group.Key}: frame avg {frame.Average():0.0} ms (p95 {P(frame, 0.95f):0.0}, worst {frame.Last():0.0}), " +
                                  $"CPU main avg {group.Average(r => r.s.cpuMainMs):0.0}, GPU avg {(gpu.Count > 0 ? gpu.Average() : 0f):0.0} (p95 {P(gpu, 0.95f):0.0}), " +
                                  $"draw calls avg {group.Average(r => r.s.drawCalls):0}, shadow casters avg {group.Average(r => r.s.shadowCasters):0}");
            }
            Debug.Log(report.ToString());
        }
    }
}
