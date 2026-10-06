using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MotoSquid.Audio
{
    public static class AudioBurstLog
    {
        const int   MinBurst         = 5;
        const float Window           = 0.5f;
        const float QuietAfterReport = 2f;

        static readonly Queue<(float time, string what, string who, float distance)> s_recent =
            new Queue<(float, string, string, float)>();
        static float s_reportedAt = -99f;
        static AudioListener s_listener;

        public static void Note(string what, Component source)
        {
            float now = Time.unscaledTime;
            if (s_listener == null) s_listener = Object.FindFirstObjectByType<AudioListener>();
            float distance = s_listener != null && source != null
                ? Vector3.Distance(s_listener.transform.position, source.transform.position) : -1f;

            s_recent.Enqueue((now, what, source != null ? source.transform.root.name : "?", distance));
            while (s_recent.Count > 0 && now - s_recent.Peek().time > Window) s_recent.Dequeue();

            if (s_recent.Count < MinBurst || now - s_reportedAt < QuietAfterReport) return;
            s_reportedAt = now;

            var report = new StringBuilder($"[AudioBurst] {s_recent.Count} sounds in {Window} s:");
            foreach (var e in s_recent)
                report.Append($"\n  {now - e.time:F2} s ago  {e.what}  on {e.who}  {e.distance:F0} m from listener");
            Debug.LogWarning(report.ToString());
        }
    }
}
