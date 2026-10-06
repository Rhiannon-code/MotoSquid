using MotoSquid.Bike;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MotoSquid.Rider
{
    public class WheelieProbe : MonoBehaviour
    {
        public BikeController bike;
        public bool pauseAtEnd = true;
        public float maxSeconds = 6f;

        struct Sample { public float t, physics, art, pitch; public bool grounded; }
        readonly List<Sample> _samples = new List<Sample>();
        bool _recording, _done;
        float _t0;

        void Reset() { bike = GetComponent<BikeController>(); }

        void LateUpdate()
        {
            if (bike == null || _done) return;

            if (bike.isDoingWheelie)
            {
                if (!_recording) { _recording = true; _t0 = Time.time; _samples.Clear(); }
                if (Time.time - _t0 <= maxSeconds) Record();
                else Finish();
                return;
            }

            if (_recording) Finish();
        }

        void Record()
        {
            var r = bike.bikeReferences;
            if (r == null) return;
            var parent = r.FrontWheelParent;
            var wheel = r.FrontWheel;
            var art = FindDeep(transform, "Front_Wheel");
            if (parent == null) return;

            _samples.Add(new Sample
            {
                t = Time.time - _t0,
                physics = wheel != null ? Vector3.Distance(parent.position, wheel.position) : -1f,
                art = art != null ? Vector3.Distance(parent.position, art.position) : -1f,
                pitch = r.WheelieTransform != null ? r.WheelieTransform.localRotation.eulerAngles.x : 0f,
                grounded = bike.frontWheelIsGrounded
            });
        }

        void Finish()
        {
            _recording = false;
            _done = true;
            if (_samples.Count == 0) { Debug.Log("[WheelieProbe] wheelie ended with no samples.", bike); return; }

            var sb = new System.Text.StringBuilder();
            sb.Append("[WheelieProbe] ").Append(bike.name).Append(" - ").Append(_samples.Count)
              .Append(" frame(s) over ").Append(_samples[_samples.Count - 1].t.ToString("0.00")).Append("s\n");
            sb.Append("   Distance from Front Wheel Parent (the axle). Growing = drifting each frame,\n");
            sb.Append("   flat but large = placed wrong from the first frame.\n\n");
            sb.Append("     time   pitch   physics   art     frontGrounded\n");

            // Every frame is noise; a dozen rows across the whole thing shows the shape
            int step = Mathf.Max(1, _samples.Count / 12);
            for (int i = 0; i < _samples.Count; i += step) Row(sb, _samples[i]);
            if ((_samples.Count - 1) % step != 0) Row(sb, _samples[_samples.Count - 1]);

            float firstArt = _samples[0].art, lastArt = _samples[_samples.Count - 1].art;
            float maxArt = 0f;
            foreach (var s in _samples) if (s.art > maxArt) maxArt = s.art;
            sb.Append("\n   art wheel: started ").Append(firstArt.ToString("0.000"))
              .Append(" m from the axle, ended ").Append(lastArt.ToString("0.000"))
              .Append(" m, worst ").Append(maxArt.ToString("0.000")).Append(" m");

            try
            {
                var dir = System.IO.Path.Combine(Application.dataPath, "..", "Logs");
                System.IO.Directory.CreateDirectory(dir);
                var file = System.IO.Path.Combine(dir, "wheelie-probe.txt");
                System.IO.File.WriteAllText(file, sb.ToString());
                sb.Append("\n\n   written to Logs/wheelie-probe.txt");
            }
            catch (System.Exception e) { sb.Append("\n   could not write the log file: ").Append(e.Message); }

            Debug.Log(sb.ToString(), bike);
            if (pauseAtEnd) Debug.Break();
        }

        static void Row(System.Text.StringBuilder sb, Sample s)
        {
            sb.Append("    ").Append(s.t.ToString("0.00").PadLeft(5))
              .Append(s.pitch.ToString("0.0").PadLeft(8))
              .Append(s.physics.ToString("0.000").PadLeft(10))
              .Append(s.art.ToString("0.000").PadLeft(8))
              .Append("     ").Append(s.grounded).Append('\n');
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindDeep(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Rig Lab/41. Add Wheelie Probe To Review Bikes", false, 410)]
        static void AddToBikes()
        {
            var bikes = Object.FindObjectsByType<BikeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bikes.Length == 0) { Debug.LogWarning("Rig Lab: no bikes in the open scene."); return; }
            foreach (var b in bikes)
            {
                var probe = b.GetComponent<WheelieProbe>() ?? Undo.AddComponent<WheelieProbe>(b.gameObject);
                probe.bike = b;
                EditorUtility.SetDirty(probe);
            }
            Debug.Log("Rig Lab: wheelie probe on " + bikes.Length + " bike(s).\n" +
                      "Play, do a wheelie, let it drop, the whole wheelie is printed and the editor pauses.");
        }
#endif
    }
}
