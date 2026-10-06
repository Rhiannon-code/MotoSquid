using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace MotoSquid.Race
{
    // The Profiler window keeps at most 2000 frames, about half a minute, so a race is streamed to disk
    // as it arrives instead. Reads whatever the Profiler is attached to: Play mode, or a Development
    // Build with Autoconnect Profiler.
    [InitializeOnLoad]
    public static class RaceProfileDump
    {
        const string MenuRoot = "MotoSquid/Profiler/";
        const string AutoKey  = "MotoSquid.RaceProfileDump.Auto";
        const float  SpikeMs  = 25f;
        const int    TopMarkersPerSpike = 15;

        static StreamWriter s_frames, s_spikes;
        static string s_dir;
        static int s_next, s_dropped;
        static RaceManager s_race;

        static readonly List<(int frame, float ms, string context)> s_all = new List<(int, float, string)>();
        static readonly Dictionary<string, (float selfMs, int frames)> s_spikeMarkers =
            new Dictionary<string, (float, int)>();
        static readonly List<int> s_children = new List<int>();

        static bool Recording => s_frames != null;

        static RaceProfileDump()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(AutoKey, false) && !Recording)
                    Begin();
                else if (state == PlayModeStateChange.ExitingPlayMode && Recording)
                    End();
            };
        }

        [MenuItem(MenuRoot + "Start Race Dump")]
        static void Begin()
        {
            s_dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "RaceProfiles",
                                 DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(s_dir);

            s_frames = new StreamWriter(Path.Combine(s_dir, "frames.csv"));
            s_frames.WriteLine("frame,ms,gc_alloc_bytes,batches,setpass,triangles,state,speed_kmh,x,y,z");
            s_spikes = new StreamWriter(Path.Combine(s_dir, "spikes.txt"));
            s_spikes.WriteLine($"Frames over {SpikeMs} ms, top {TopMarkersPerSpike} markers by self time " +
                               "(merged by name, main thread).\n");

            s_all.Clear();
            s_spikeMarkers.Clear();
            s_dropped = 0;
            s_race = null;

            ProfilerDriver.enabled = true;
            s_next = ProfilerDriver.lastFrameIndex + 1;
            EditorApplication.update += Pump;

            Debug.Log($"[RaceProfileDump] Recording to {s_dir}");
        }

        [MenuItem(MenuRoot + "Start Race Dump", true)]
        static bool CanBegin() => !Recording;

        [MenuItem(MenuRoot + "Stop Race Dump")]
        static void End()
        {
            EditorApplication.update -= Pump;
            Drain(ProfilerDriver.lastFrameIndex + 1);

            s_frames.Dispose();
            s_spikes.Dispose();
            s_frames = s_spikes = null;

            WriteSummary();
            Debug.Log($"[RaceProfileDump] {s_all.Count} frames written to {s_dir}" +
                      (s_dropped > 0 ? $" ({s_dropped} dropped: the editor fell behind the Profiler history)" : ""));
        }

        [MenuItem(MenuRoot + "Stop Race Dump", true)]
        static bool CanEnd() => Recording;

        [MenuItem(MenuRoot + "Auto-Dump Every Play Session")]
        static void ToggleAuto() => EditorPrefs.SetBool(AutoKey, !EditorPrefs.GetBool(AutoKey, false));

        [MenuItem(MenuRoot + "Auto-Dump Every Play Session", true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked(MenuRoot + "Auto-Dump Every Play Session", EditorPrefs.GetBool(AutoKey, false));
            return true;
        }

        // The newest frame can still be receiving samples, so it waits for the next update
        static void Pump() => Drain(ProfilerDriver.lastFrameIndex);

        static void Drain(int endExclusive)
        {
            int first = ProfilerDriver.firstFrameIndex;
            if (first < 0) return;
            if (s_next < first) { s_dropped += first - s_next; s_next = first; }

            string context = RaceContext();
            for (; s_next < endExclusive; s_next++)
                WriteFrame(s_next, context);
        }

        static void WriteFrame(int frame, string context)
        {
            float ms;
            using (var raw = ProfilerDriver.GetRawFrameDataView(frame, 0))
            {
                if (!raw.valid) return;
                ms = raw.frameTimeMs;
                long gc = Counter(raw, "GC Allocated In Frame"), batches = Counter(raw, "Batches Count");
                long setPass = Counter(raw, "SetPass Calls Count"), tris = Counter(raw, "Triangles Count");
                s_frames.WriteLine(FormattableString.Invariant($"{frame},{ms:F2},{gc},{batches},{setPass},{tris},{context}"));
            }

            s_all.Add((frame, ms, context));
            if (ms >= SpikeMs) WriteSpike(frame, ms, context);
        }

        static long Counter(FrameDataView view, string name)
        {
            int id = view.GetMarkerId(name);
            return id != FrameDataView.invalidMarkerId && view.HasCounterValue(id) ? view.GetCounterValueAsLong(id) : -1;
        }

        // Inverted, the top level is every marker ranked by its own time, which is what a spike needs
        static void WriteSpike(int frame, float ms, string context)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                       HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName |
                       HierarchyFrameDataView.ViewModes.InvertHierarchy,
                       HierarchyFrameDataView.columnSelfTime, false))
            {
                if (!view.valid) return;

                s_spikes.WriteLine(FormattableString.Invariant($"frame {frame}  {ms:F1} ms  [{context}]"));
                s_children.Clear();
                view.GetItemChildren(view.GetRootItemID(), s_children);

                foreach (int id in s_children.Take(TopMarkersPerSpike))
                {
                    string name = view.GetItemName(id);
                    float self  = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                    float gc    = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);
                    s_spikes.WriteLine(FormattableString.Invariant($"  {self,8:F2} ms  {gc,9:F0} B  {name}"));

                    s_spikeMarkers.TryGetValue(name, out var total);
                    s_spikeMarkers[name] = (total.selfMs + self, total.frames + 1);
                }
                s_spikes.WriteLine();
            }
        }

        // Sampled once per editor update, so it trails the frame it labels by a frame or two
        static string RaceContext()
        {
            if (!EditorApplication.isPlaying) return ",,,,";
            if (s_race == null) s_race = UnityEngine.Object.FindFirstObjectByType<RaceManager>();
            var bike = s_race != null ? s_race.playerBike : null;
            if (bike == null) return s_race != null ? $"{s_race.State},,,," : ",,,,";

            Vector3 p = bike.transform.position;
            return FormattableString.Invariant(
                $"{s_race.State},{bike.localBikeVelocity.magnitude * 3.6f:F0},{p.x:F0},{p.y:F0},{p.z:F0}");
        }

        static void WriteSummary()
        {
            using var w = new StreamWriter(Path.Combine(s_dir, "summary.md"));
            w.WriteLine($"# Race profile {Path.GetFileName(s_dir)}\n");
            if (s_all.Count == 0) { w.WriteLine("No frames captured. Was the Profiler recording?"); return; }

            var sorted = s_all.Select(f => f.ms).OrderBy(ms => ms).ToList();
            float Pct(float p) => sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];

            w.WriteLine(FormattableString.Invariant($"- Frames: {s_all.Count} (dropped {s_dropped})"));
            w.WriteLine(FormattableString.Invariant($"- Average: {sorted.Average():F2} ms ({1000f / sorted.Average():F0} fps)"));
            w.WriteLine(FormattableString.Invariant($"- p50 / p95 / p99 / max: {Pct(.5f):F2} / {Pct(.95f):F2} / {Pct(.99f):F2} / {sorted[^1]:F2} ms"));
            w.WriteLine($"- Over 16.7 ms: {sorted.Count(ms => ms > 16.7f)}, over 33.3 ms: {sorted.Count(ms => ms > 33.3f)}, " +
                        $"over {SpikeMs} ms (detailed in spikes.txt): {sorted.Count(ms => ms >= SpikeMs)}\n");

            w.WriteLine("## Markers summed over spike frames (self time)\n");
            w.WriteLine("| self ms | spikes | marker |\n|---:|---:|---|");
            foreach (var kv in s_spikeMarkers.OrderByDescending(kv => kv.Value.selfMs).Take(25))
                w.WriteLine(FormattableString.Invariant($"| {kv.Value.selfMs:F1} | {kv.Value.frames} | {kv.Key} |"));

            w.WriteLine("\n## Slowest 20 frames\n");
            w.WriteLine("| frame | ms | state, km/h, x, y, z |\n|---:|---:|---|");
            foreach (var f in s_all.OrderByDescending(f => f.ms).Take(20))
                w.WriteLine(FormattableString.Invariant($"| {f.frame} | {f.ms:F1} | {f.context} |"));
        }
    }
}
