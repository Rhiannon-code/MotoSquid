using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.Art
{
    // Art Test scene only: F4 cycles the street through Toon and Lit, each with and without the GPU Resident
    // Drawer, and shows median frame and main-thread times so they can be compared in Play mode
    public class ArtTestPerfProbe : MonoBehaviour
    {
        public Transform street;
        public Material[] toon;
        public Material[] lit;
        // SRP Core's DisallowGPUDrivenRendering on the street root; it is internal, so held as a Behaviour
        public Behaviour gpuDrivenOptOut;

        // Toon + GRD draws magenta unless the Toon Shader carries the SRP Batcher patch
        static readonly (string name, bool lit, bool grd)[] Modes =
        {
            ("Toon, GRD opted out", false, false),
            ("Toon + GRD", false, true),
            ("Lit + GRD", true, true),
            ("Lit, GRD opted out", true, false),
        };
        const int Window = 240;

        int m_Mode;
        bool m_Logged;
        Renderer[] m_Renderers;
        readonly Queue<float> m_Frame = new Queue<float>(), m_Main = new Queue<float>();
        ProfilerRecorder m_MainThread, m_Batches, m_DrawCalls;

        void OnEnable()
        {
            m_MainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            m_Batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            m_DrawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        }

        void OnDisable()
        {
            m_MainThread.Dispose();
            m_Batches.Dispose();
            m_DrawCalls.Dispose();
        }

        void Start() => m_Renderers = street.GetComponentsInChildren<Renderer>(true);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f4Key.wasPressedThisFrame) SetMode((m_Mode + 1) % Modes.Length);

            Push(m_Frame, Time.unscaledDeltaTime * 1000f);
            if (m_MainThread.Valid) Push(m_Main, m_MainThread.LastValue * 1e-6f);
            if (!m_Logged && m_Frame.Count == Window)
            {
                m_Logged = true;
                Debug.Log($"[ArtTestPerf] {Readout()} at {Screen.width}x{Screen.height}");
            }
        }

        void SetMode(int mode)
        {
            var from = Modes[m_Mode].lit ? lit : toon;
            var to = Modes[mode].lit ? lit : toon;
            if (from != to)
            {
                var map = Enumerable.Range(0, from.Length).ToDictionary(i => from[i], i => to[i]);
                foreach (var r in m_Renderers)
                    r.sharedMaterials = r.sharedMaterials.Select(m => m != null && map.TryGetValue(m, out var t) ? t : m).ToArray();
            }
            gpuDrivenOptOut.enabled = !Modes[mode].grd;
            m_Mode = mode;
            m_Frame.Clear();
            m_Main.Clear();
            m_Logged = false;
        }

        static void Push(Queue<float> q, float v)
        {
            q.Enqueue(v);
            if (q.Count > Window) q.Dequeue();
        }

        static string Median(Queue<float> q)
        {
            if (q.Count == 0) return "n/a";
            var s = q.OrderBy(v => v).ToArray();
            return $"{s[s.Length / 2]:0.00} ms";
        }

        string Readout()
        {
            string counts = m_Batches.Valid ? $"batches {m_Batches.LastValue}  draws {m_DrawCalls.LastValue}" : "batches n/a";
            return $"{Modes[m_Mode].name}   frame {Median(m_Frame)}   main thread {Median(m_Main)}   {counts}";
        }

        void OnGUI()
        {
            string settling = m_Frame.Count < Window ? "  (settling...)" : "";
            GUI.Label(new Rect(10f, 30f, 900f, 24f), $"[Perf] F4: {Readout()}{settling}");
        }
    }
}
