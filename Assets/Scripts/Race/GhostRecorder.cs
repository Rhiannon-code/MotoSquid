using MotoSquid.Core;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MotoSquid.Race
{
    public class GhostRecorder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private Transform target;
        [SerializeField] private GameObject ghostObject;

        [Header("Recording")]
        [SerializeField] private float sampleInterval = 0.05f;   // 20 Hz

        [System.Serializable] struct GhostSample { public float t; public Vector3 pos; public Quaternion rot; }
        [System.Serializable] class GhostData { public float runTime = -1f; public List<GhostSample> samples = new List<GhostSample>(); }

        GhostData _recording;
        GhostData _playback;
        bool   _recording_active;
        bool   _playing;
        float  _startTime;
        float  _sampleAccumulator;
        int    _playIndex;

        string SavePath => Path.Combine(Application.persistentDataPath,
                                        $"ghost_{SceneManager.GetActiveScene().name}.json");

        void Start()
        {
            if (raceManager == null) raceManager = FindObjectOfType<RaceManager>();
            if (target == null && raceManager != null) target = raceManager.playerTransform;

            _playback = LoadGhost();
            if (ghostObject != null) ghostObject.SetActive(false);

            if (raceManager != null)
            {
                raceManager.OnRaceStart      += BeginRace;
                raceManager.OnRacerFinished  += OnRacerFinished;
            }
        }

        void OnDestroy()
        {
            if (raceManager != null)
            {
                raceManager.OnRaceStart      -= BeginRace;
                raceManager.OnRacerFinished  -= OnRacerFinished;
            }
        }

        void BeginRace()
        {
            if (target == null && raceManager != null) target = raceManager.playerTransform;

            _recording = new GhostData();
            _recording_active = target != null;
            _startTime = Time.time;
            _sampleAccumulator = 0f;

            _playIndex = 0;
            bool timeTrial = raceManager != null && raceManager.ActiveGameMode == GameMode.TimeTrial;
            _playing = timeTrial && _playback != null && _playback.samples.Count > 1 && ghostObject != null;
            if (_playing)
            {
                ghostObject.SetActive(true);
                ApplyPlaybackSample(0f);
            }
        }

        void Update()
        {
            float elapsed = Time.time - _startTime;

            if (_recording_active && target != null)
            {
                _sampleAccumulator += Time.deltaTime;
                if (_sampleAccumulator >= sampleInterval)
                {
                    _sampleAccumulator = 0f;
                    _recording.samples.Add(new GhostSample
                    {
                        t = elapsed, pos = target.position, rot = target.rotation
                    });
                }
            }

            if (_playing) ApplyPlaybackSample(elapsed);
        }

        // Interpolate the ghost object along the saved samples by elapsed race time
        void ApplyPlaybackSample(float elapsed)
        {
            var s = _playback.samples;
            while (_playIndex < s.Count - 2 && s[_playIndex + 1].t < elapsed) _playIndex++;

            if (_playIndex >= s.Count - 1)
            {
                ghostObject.transform.SetPositionAndRotation(s[s.Count - 1].pos, s[s.Count - 1].rot);
                _playing = false;   // Ghost finished its run
                return;
            }

            GhostSample a = s[_playIndex], b = s[_playIndex + 1];
            float span = Mathf.Max(0.0001f, b.t - a.t);
            float f = Mathf.Clamp01((elapsed - a.t) / span);
            ghostObject.transform.SetPositionAndRotation(
                Vector3.Lerp(a.pos, b.pos, f), Quaternion.Slerp(a.rot, b.rot, f));
        }

        void OnRacerFinished(RaceManager.RacerInfo racer)
        {
            // Only the player's run is recorded/saved.
            if (racer == null || target == null || racer.transform != target) return;
            if (!_recording_active) return;

            _recording_active = false;
            _recording.runTime = Time.time - _startTime;

            // Save when there's no stored ghost yet or this run was faster
            bool improved = _playback == null || _playback.runTime < 0f ||
                            _recording.runTime < _playback.runTime;
            if (improved && _recording.samples.Count > 1)
            {
                SaveGhost(_recording);
                _playback = _recording;
            }
        }

        GhostData LoadGhost()
        {
            try
            {
                if (!File.Exists(SavePath)) return null;
                return JsonUtility.FromJson<GhostData>(File.ReadAllText(SavePath));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GhostRecorder] Failed to load ghost: {e.Message}");
                return null;
            }
        }

        void SaveGhost(GhostData data)
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data)); }
            catch (System.Exception e) { Debug.LogWarning($"[GhostRecorder] Failed to save ghost: {e.Message}"); }
        }
    }
}
