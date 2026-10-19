using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoSquid.DevTools
{
    // On-screen frame cost: frame time, CPU main / render thread and GPU time (FrameTimingManager, always on in
    // Development builds; GPU time can read 0 where the platform doesn't report it), and the render counters. F3 shows
    // and hides it. PerfRoute reads the same numbers for its log.
    public class PerfHud : MonoBehaviour
    {
        public bool visible = true;

        public struct Sample
        {
            public float frameMs, cpuMainMs, cpuRenderMs, gpuMs;
            public long drawCalls, batches, setPass, shadowCasters, triangles;
        }

        public Sample Latest { get; private set; }

        readonly FrameTiming[] timings = new FrameTiming[1];
        ProfilerRecorder drawCalls, batches, setPass, shadowCasters, triangles;
        float smoothedMs;
        string text = "";
        GUIStyle style;

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        void OnDisable()
        {
            drawCalls.Dispose(); batches.Dispose(); setPass.Dispose(); shadowCasters.Dispose(); triangles.Dispose();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;

            FrameTimingManager.CaptureFrameTimings();
            bool timed = FrameTimingManager.GetLatestTimings(1, timings) > 0;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            smoothedMs = smoothedMs <= 0f ? frameMs : Mathf.Lerp(smoothedMs, frameMs, 0.1f);
            Latest = new Sample
            {
                frameMs = frameMs,
                cpuMainMs = timed ? (float)timings[0].cpuMainThreadFrameTime : 0f,
                cpuRenderMs = timed ? (float)timings[0].cpuRenderThreadFrameTime : 0f,
                gpuMs = timed ? (float)timings[0].gpuFrameTime : 0f,
                drawCalls = drawCalls.LastValue, batches = batches.LastValue, setPass = setPass.LastValue,
                shadowCasters = shadowCasters.LastValue, triangles = triangles.LastValue,
            };
            if (Time.frameCount % 10 == 0)
            {
                var s = Latest;
                text = $"{1000f / smoothedMs:0} fps  {smoothedMs:0.0} ms   ({Screen.width}x{Screen.height})\n" +
                       $"CPU main {s.cpuMainMs:0.0}  render {s.cpuRenderMs:0.0}  GPU {(s.gpuMs > 0f ? s.gpuMs.ToString("0.0") : "n/a")} ms\n" +
                       $"draw calls {s.drawCalls}  batches {s.batches}  set-pass {s.setPass}\n" +
                       $"shadow casters {s.shadowCasters}  triangles {s.triangles / 1000}k";
            }
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
            GUI.Box(new Rect(10, 10, 470, 96), text, style);
        }
    }
}
