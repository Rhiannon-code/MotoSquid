using MotoSquid.Race;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Audio
{
    public class VoiceBarkRaceDirector : MonoBehaviour
    {
        [SerializeField] RaceManager raceManager;

        [Header("Staggering")]
        [SerializeField] float gridBarkStagger = 0.6f;

        // On top of the ~0.37 s of silence the taunt clips already carry, so the fly-in has visibly
        // settled before anyone speaks. The countdown starts ~4 s after the blend, so there is room
        [SerializeField] float startTauntDelay = 1f;

        // Neck and neck racers swap position continuously, so a bark on every change is a running
        // commentary. A change has to hold this long before it counts as a real overtake
        [SerializeField] float overtakeHoldTime = 1.5f;

        struct PositionWatch
        {
            public int   settled;
            public int   pending;
            public float pendingSince;
        }

        readonly Dictionary<Transform, VoiceBarkController> _controllers = new();
        readonly Dictionary<Transform, PositionWatch> _watch = new();

        bool _subscribed;

        void Start()
        {
            if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();
            if (raceManager == null)
            {
                // Silent here means five of the eight bark categories never fire and nothing says why
                Debug.LogWarning($"{name}: no RaceManager in the scene, so start taunts, overtakes " +
                                 "and race results will never bark.", this);
                return;
            }

            raceManager.OnGridWarmedUp += OnGridWarmedUp;
            raceManager.OnRaceStart    += OnRaceStart;
            raceManager.OnRaceComplete += OnRaceComplete;
            _subscribed = true;

            // Covers being enabled after the fly in has already landed, e.g. skipCountdown
            if (raceManager.GridWarmedUp) OnGridWarmedUp();
        }

        void OnDestroy()
        {
            if (_subscribed && raceManager != null)
            {
                raceManager.OnGridWarmedUp -= OnGridWarmedUp;
                raceManager.OnRaceStart    -= OnRaceStart;
                raceManager.OnRaceComplete -= OnRaceComplete;
            }
        }

        void Update()
        {
            if (raceManager == null || raceManager.State != RaceManager.RaceState.Racing) return;
            PollOvertakes();
        }

        // The fly in has landed on the player and the grid is awake, but the countdown has not
        // started. Taunting on OnRaceStart put it on "GO!", over the launch
        void OnGridWarmedUp()
        {
            StartCoroutine(StartTaunts());
        }

        IEnumerator StartTaunts()
        {
            if (startTauntDelay > 0f) yield return new WaitForSecondsRealtime(startTauntDelay);
            yield return StaggeredBarks(SnapshotRacers(), BarkCategory.StartTaunt);
        }

        void OnRaceStart()
        {
            _watch.Clear();
            foreach (var r in raceManager.Standings)
                _watch[r.transform] = new PositionWatch
                    { settled = r.position, pending = r.position };
        }

        void OnRaceComplete(List<RaceManager.RacerInfo> finishOrder)
        {
            StopAllCoroutines(); 
            StartCoroutine(ResultBarks(finishOrder));
        }

        IEnumerator ResultBarks(List<RaceManager.RacerInfo> finishOrder)
        {
            for (int i = 0; i < finishOrder.Count; i++)
            {
                var ctrl = GetController(finishOrder[i].transform);
                if (ctrl == null) continue;   // Silent racers must not spend the stagger slot

                ctrl.PlayBark(i == 0 ? BarkCategory.WinRace : BarkCategory.LoseRace);
                yield return new WaitForSecondsRealtime(gridBarkStagger);
            }
        }

        IEnumerator StaggeredBarks(List<Transform> racers, BarkCategory category)
        {
            foreach (var t in racers)
            {
                var ctrl = GetController(t);
                if (ctrl == null) continue;   // Silent racers must not spend the stagger slot

                ctrl.PlayBark(category);
                yield return new WaitForSecondsRealtime(gridBarkStagger);
            }
        }

        void PollOvertakes()
        {
            foreach (var r in raceManager.Standings)
            {
                // Position 0 means never sorted, treating that as a change barked on the first
                // sorted frame for everyone
                if (r.position <= 0) continue;

                if (!_watch.TryGetValue(r.transform, out var w) || w.settled <= 0)
                {
                    _watch[r.transform] = new PositionWatch
                        { settled = r.position, pending = r.position };
                    continue;
                }

                if (r.position != w.pending)
                {
                    w.pending           = r.position;
                    w.pendingSince      = Time.time;
                    _watch[r.transform] = w;
                    continue;
                }

                if (r.position == w.settled) continue;
                if (Time.time - w.pendingSince < overtakeHoldTime) continue;

                GetController(r.transform)?.PlayBark(r.position < w.settled
                    ? BarkCategory.Overtaking
                    : BarkCategory.BeingOvertaken);

                w.settled           = r.position;
                _watch[r.transform] = w;
            }
        }

        List<Transform> SnapshotRacers()
        {
            var list = new List<Transform>();
            foreach (var r in raceManager.Standings) list.Add(r.transform);
            return list;
        }

        VoiceBarkController GetController(Transform racer)
        {
            if (racer == null) return null;
            if (_controllers.TryGetValue(racer, out var ctrl)) return ctrl;

            ctrl = racer.GetComponentInChildren<VoiceBarkController>();
            _controllers[racer] = ctrl;
            return ctrl;
        }
    }
}
