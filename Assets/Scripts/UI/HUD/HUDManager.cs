using MotoSquid.Bike;
using MotoSquid.Core;
using MotoSquid.Race;
using MotoSquid.Rider;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MotoSquid.UI
{
    public class HUDManager : MonoBehaviour
    {
        [Header("HUD panels (children of the Game Canvas)")]
        [SerializeField] private RectTransform p1Panel;
        [SerializeField] private RectTransform p2Panel;
        [SerializeField] private float splitScreenHudScale = 0.5f;
        // 1 keeps KERS the same on-screen width as single player; raise it to span more of the gap
        [SerializeField] private float kersWidthScale = 1f;

        [Header("Race data")]
        [SerializeField] private RaceManager    raceManager;
        [SerializeField] private BikeController p1Bike;
        [SerializeField] private BikeController p2Bike;
        [SerializeField] private BoostSystem             p1BoostSystem;
        [SerializeField] private BoostSystem             p2BoostSystem;

        [Header("P1 HUD elements")]
        [SerializeField] private Image    p1PositionImage;
        [SerializeField] private TMP_Text p1Pos1Text;
        [SerializeField] private TMP_Text p1Pos2Text;
        [SerializeField] private TMP_Text p1Pos3Text;
        [SerializeField] private Image    p1SpeedImage;
        [SerializeField] private TMP_Text p1SpeedNumber;
        [SerializeField] private Image    p1GearImage;
        [SerializeField] private TMP_Text p1GearNumber;

        [Header("P2 HUD elements")]
        [SerializeField] private Image    p2PositionImage;
        [SerializeField] private TMP_Text p2Pos1Text;
        [SerializeField] private TMP_Text p2Pos2Text;
        [SerializeField] private TMP_Text p2Pos3Text;
        [SerializeField] private Image    p2SpeedImage;
        [SerializeField] private TMP_Text p2SpeedNumber;
        [SerializeField] private Image    p2GearImage;
        [SerializeField] private TMP_Text p2GearNumber;

        [Header("Speed sprites (Speedo_1 to Speedo_11, assign in order)")]
        [SerializeField] private Sprite[] speedSprites;

        [Header("Gear sprites (Gear_1 to Gear_6, assign in order)")]
        [SerializeField] private Sprite[] gearSprites;

        [Header("Boost bars")]
        [SerializeField] private Image p1BoostLeft;
        [SerializeField] private Image p1BoostRight;
        [SerializeField] private Image p2BoostLeft;
        [SerializeField] private Image p2BoostRight;

        [Header("Boost colours")]
        [SerializeField] private Color boostIdleColour  = Color.white;
        [SerializeField] private Color boostGainColour  = Color.cyan;
        [SerializeField] private Color boostDrainColour = Color.red;
        [Range(1f, 10f)]
        [SerializeField] private float boostColourSpeed = 4f;

        [Header("Speed indicator smoothing")]
        [SerializeField] private float speedSmoothingRate = 3f;
        [SerializeField] private float redlineFlashPeriod = 0.16f;

        [Header("Off track respawn warning (per player, children of each panel)")]
        [SerializeField] private GameObject p1RespawnWarningRoot;
        [SerializeField] private TMP_Text   p1RespawnWarningText;
        [SerializeField] private GameObject p2RespawnWarningRoot;
        [SerializeField] private TMP_Text   p2RespawnWarningText;

        [Header("Wrong way warning (per player, children of each panel)")]
        [SerializeField] private GameObject p1WrongWayWarningRoot;
        [SerializeField] private TMP_Text   p1WrongWayWarningText;
        [SerializeField] private GameObject p2WrongWayWarningRoot;
        [SerializeField] private TMP_Text   p2WrongWayWarningText;

        [Header("Simplified/reduced clutter HUD (optional, non essential elements)")]
        [SerializeField] private GameObject[] clutterElements;

        [Header("Lap/split time popups (per player, children of each panel)")]
        [SerializeField] private TMP_Text lapTimeText;      // P1 lap time popup
        [SerializeField] private TMP_Text p2LapTimeText;    // P2 lap time popup (split screen)
        [SerializeField] private float    lapTimePopupDuration = 3.5f;

        [Header("Race timer (Race-mode stopwatch; Time Trial uses the countdown instead)")]
        [SerializeField] private TMP_Text raceTimerText;
        [SerializeField] private float raceTimerCharWidthEm = 0.6f;

        float _p1LapPopupTimer, _p2LapPopupTimer;

        bool _isSplitScreen;

        int   _p1LastGear = -1, _p1LastSpeedKmh = -1;
        int   _p2LastGear = -1, _p2LastSpeedKmh = -1;
        float _p1SmoothedSpeedT, _p2SmoothedSpeedT;
        float _p1LastBoostMeter, _p2LastBoostMeter;
        Color _p1BoostColour = Color.white;
        Color _p2BoostColour = Color.white;

        // Cached top 3 transforms for dirty checking standings display
        Transform _lastTop1, _lastTop2, _lastTop3;

        void Start()
        {
            _isSplitScreen = GameSession.Instance != null && GameSession.Instance.IsSplitScreen;

            ConfigurePanels();

            if (p1RespawnWarningRoot != null) p1RespawnWarningRoot.SetActive(false);
            if (p2RespawnWarningRoot != null) p2RespawnWarningRoot.SetActive(false);
            if (p1WrongWayWarningRoot != null) p1WrongWayWarningRoot.SetActive(false);
            if (p2WrongWayWarningRoot != null) p2WrongWayWarningRoot.SetActive(false);

            if (lapTimeText   != null) lapTimeText.gameObject.SetActive(false);
            if (p2LapTimeText != null) p2LapTimeText.gameObject.SetActive(false);

            if (raceManager != null) raceManager.OnLapCompleted += OnLapCompleted;
        }

        void OnDestroy()
        {
            if (raceManager != null) raceManager.OnLapCompleted -= OnLapCompleted;
        }

        void OnLapCompleted(RaceManager.RacerInfo racer, float lapTime, bool isBest)
        {
            if (racer == null || raceManager == null) return;

            TMP_Text target = null;
            if (racer.transform == raceManager.playerTransform)       target = lapTimeText;
            else if (racer.transform == raceManager.playerTransform2) target = p2LapTimeText;
            if (target == null) return;

            string best = isBest ? "   <color=#FFD400>BEST!</color>" : "";
            target.text = $"LAP {racer.laps}   {FormatLapTime(lapTime)}{best}";
            target.gameObject.SetActive(true);

            if (target == lapTimeText) _p1LapPopupTimer = lapTimePopupDuration;
            else                       _p2LapPopupTimer = lapTimePopupDuration;
        }

        static string FormatLapTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return $"{m}:{s:00.00}";
        }

        void TickLapPopups()
        {
            if (_p1LapPopupTimer > 0f)
            {
                _p1LapPopupTimer -= Time.deltaTime;
                if (_p1LapPopupTimer <= 0f && lapTimeText != null)
                    lapTimeText.gameObject.SetActive(false);
            }
            if (_p2LapPopupTimer > 0f)
            {
                _p2LapPopupTimer -= Time.deltaTime;
                if (_p2LapPopupTimer <= 0f && p2LapTimeText != null)
                    p2LapTimeText.gameObject.SetActive(false);
            }
        }
        bool IsP2(BikeController bike) => bike != null && bike == p2Bike;

        static void SetWarning(GameObject root, TMP_Text text, bool show, string message = null)
        {
            if (root != null && root.activeSelf != show) root.SetActive(show);
            if (show && text != null && message != null) text.text = message;
        }

        public void ShowRespawnWarning(BikeController bike, int secondsRemaining)
        {
            bool p2 = IsP2(bike);
            SetWarning(p2 ? p2RespawnWarningRoot : p1RespawnWarningRoot,
                       p2 ? p2RespawnWarningText : p1RespawnWarningText,
                       true, $"RETURN TO TRACK!\nRespawning in {Mathf.Max(0, secondsRemaining)}...");
        }

        public void HideRespawnWarning(BikeController bike)
        {
            bool p2 = IsP2(bike);
            SetWarning(p2 ? p2RespawnWarningRoot : p1RespawnWarningRoot,
                       p2 ? p2RespawnWarningText : p1RespawnWarningText, false);
        }

        // Called by ResetBike while the player is driving back down the track
        public void ShowWrongWayWarning(BikeController bike)
        {
            bool p2 = IsP2(bike);
            SetWarning(p2 ? p2WrongWayWarningRoot : p1WrongWayWarningRoot,
                       p2 ? p2WrongWayWarningText : p1WrongWayWarningText,
                       true, "WRONG WAY!\nTurn around");
        }

        public void HideWrongWayWarning(BikeController bike)
        {
            bool p2 = IsP2(bike);
            SetWarning(p2 ? p2WrongWayWarningRoot : p1WrongWayWarningRoot,
                       p2 ? p2WrongWayWarningText : p1WrongWayWarningText, false);
        }

        void ConfigurePanels()
        {
            if (_isSplitScreen)
            {
                float aspect    = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
                bool  topBottom = aspect <= 2.0f;

                if (topBottom)
                {
                    SetAnchors(p1Panel, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
                    SetAnchors(p2Panel, new Vector2(0f, 0f),   new Vector2(1f, 0.5f));
                }
                else
                {
                    SetAnchors(p1Panel, new Vector2(0f,   0f), new Vector2(0.5f, 1f));
                    SetAnchors(p2Panel, new Vector2(0.5f, 0f), new Vector2(1f,   1f));
                }

                p2Panel?.gameObject.SetActive(true);
                if (p2BoostLeft  != null) p2BoostLeft.gameObject.SetActive(true);
                if (p2BoostRight != null) p2BoostRight.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                FitBar(p1Panel, p1SpeedNumber, splitScreenHudScale);
                FitBar(p2Panel, p2SpeedNumber, splitScreenHudScale);
                WidenKers(p1BoostLeft, splitScreenHudScale);
                WidenKers(p2BoostLeft, splitScreenHudScale);
                MirrorInPanel(p1WrongWayWarningRoot, p1Panel, p2WrongWayWarningRoot, p2Panel);
            }
            else
            {
                SetAnchors(p1Panel, Vector2.zero, Vector2.one);
                FitBar(p1Panel, p1SpeedNumber, 1f);
                p2Panel?.gameObject.SetActive(false);
                if (p2BoostLeft  != null) p2BoostLeft.gameObject.SetActive(false);
                if (p2BoostRight != null) p2BoostRight.gameObject.SetActive(false);
            }
        }

        readonly System.Collections.Generic.Dictionary<RectTransform, Vector2> _authoredSize =
            new System.Collections.Generic.Dictionary<RectTransform, Vector2>();

        void WidenKers(Component boostElement, float scale)
        {
            if (boostElement == null || scale <= 0f) return;
            if (boostElement.transform.parent is not RectTransform group) return;

            if (!_authoredSize.TryGetValue(group, out Vector2 authored))
            {
                authored = group.sizeDelta;
                _authoredSize[group] = authored;
            }

            group.sizeDelta = new Vector2(authored.x * kersWidthScale / scale, authored.y);
        }

        static void MirrorInPanel(GameObject from, RectTransform fromPanel,
                                  GameObject to,   RectTransform toPanel)
        {
            if (from == null || to == null) return;
            if (from.transform is not RectTransform src || to.transform is not RectTransform dst) return;

            // Only safe while both sit directly under their own half: otherwise the offsets are measured
            // against different parents and copying them would move P2's panel somewhere arbitrary.
            if (src.parent != fromPanel || dst.parent != toPanel)
            {
                Debug.LogWarning($"[HUDManager] '{to.name}' is not a direct child of '{toPanel?.name}', " +
                                 "so its placement cannot be mirrored from P1.", to);
                return;
            }

            dst.anchorMin        = src.anchorMin;
            dst.anchorMax        = src.anchorMax;
            dst.pivot            = src.pivot;
            dst.anchoredPosition = src.anchoredPosition;
            dst.sizeDelta        = src.sizeDelta;
            dst.localScale       = src.localScale;
        }

        static void FitBar(RectTransform panel, Component element, float scale)
        {
            if (panel == null || element == null) return;

            RectTransform bar = null;
            for (Transform t = element.transform; t != null; t = t.parent)
                if (t.parent == panel) { bar = t as RectTransform; break; }

            if (bar == null)
            {
                Debug.LogWarning($"[HUDManager] '{element.name}' is not under panel '{panel.name}', so " +
                                 "that half of the HUD cannot be placed.", panel);
                return;
            }

            bar.anchorMin        = new Vector2(0f, 0f);
            bar.anchorMax        = new Vector2(1f, 0f);
            bar.pivot            = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;

            // Scaling a stretched bar shrinks it about its pivot, so the positions, KERS and speed groups
            // all collapsed into the middle of the viewport with dead space either side. The bar is
            // widened by the inverse of the scale first, so once scaled it still spans the full width.
            float width = panel.rect.width;
            if (width < 1f)
            {
                var canvas = panel.GetComponentInParent<Canvas>();
                if (canvas != null) width = ((RectTransform)canvas.transform).rect.width;
            }

            float extraX = scale > 0f ? width * (1f / scale - 1f) : 0f;

            bar.sizeDelta  = new Vector2(extraX, bar.sizeDelta.y);
            bar.localScale = Vector3.one * scale;
        }

        public void SetSimplified(bool simplified)
        {
            if (clutterElements == null) return;
            foreach (var go in clutterElements)
                if (go != null && go.activeSelf == simplified) go.SetActive(!simplified);
        }

        public void SetSplitScreen(bool splitScreen)
        {
            _isSplitScreen = splitScreen;
            ConfigurePanels();
        }

        public void BindPlayer1(BikeController bike, BoostSystem boost)
        {
            p1Bike = bike;
            p1BoostSystem = boost;
        }

        // Split screen spawns P2 at runtime just like P1, so its HUD has to be rebound the same way
        // The end screen goes up over the race view, in single player and split screen alike
        bool _raceHudHidden;
        public void HideRaceHud()
        {
            _raceHudHidden = true;
            if (p1Panel       != null) p1Panel.gameObject.SetActive(false);
            if (p2Panel       != null) p2Panel.gameObject.SetActive(false);
            if (raceTimerText != null) raceTimerText.gameObject.SetActive(false);
            // Not children of the panels, ConfigurePanels switches them on separately
            foreach (var bar in new Component[] { p1BoostLeft, p1BoostRight, p2BoostLeft, p2BoostRight })
                if (bar != null) bar.gameObject.SetActive(false);
        }

        public void BindPlayer2(BikeController bike, BoostSystem boost)
        {
            p2Bike = bike;
            p2BoostSystem = boost;
        }

        static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max,
                               Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        void Update()
        {
            if (p1Bike != null)
                UpdateIndicators(p1Bike, p1BoostSystem,
                                 p1SpeedImage, p1SpeedNumber,
                                 p1GearImage, p1GearNumber,
                                 ref _p1SmoothedSpeedT, ref _p1LastSpeedKmh, ref _p1LastGear);

            if (_isSplitScreen && p2Bike != null)
                UpdateIndicators(p2Bike, p2BoostSystem,
                                 p2SpeedImage, p2SpeedNumber,
                                 p2GearImage, p2GearNumber,
                                 ref _p2SmoothedSpeedT, ref _p2LastSpeedKmh, ref _p2LastGear);

            UpdateBoost(p1BoostSystem, p1BoostLeft, p1BoostRight, ref _p1BoostColour, ref _p1LastBoostMeter);
            UpdateBoost(p2BoostSystem, p2BoostLeft, p2BoostRight, ref _p2BoostColour, ref _p2LastBoostMeter);

            UpdateStandingsDisplay();
            TickLapPopups();
            TickRaceTimer();
        }

        void TickRaceTimer()
        {
            if (raceTimerText == null || raceManager == null) return;

            bool show = !_raceHudHidden && raceManager.ActiveGameMode == GameMode.Race;
            if (raceTimerText.gameObject.activeSelf != show) raceTimerText.gameObject.SetActive(show);
            if (!show) return;

            if (raceManager.State != RaceManager.RaceState.Finished)
                // Fixed width characters so the readout doesn't shuffle sideways as the digits change
                raceTimerText.text = $"<mspace={raceTimerCharWidthEm}em>{FormatLapTime(raceManager.RaceElapsedTime)}</mspace>";
        }

        void UpdateIndicators(BikeController bike, BoostSystem boostSystem,
                              Image speedImage, TMP_Text speedNumber,
                              Image gearImage,  TMP_Text gearNumber,
                              ref float smoothedSpeedT, ref int lastSpeedKmh, ref int lastGear)
        {
            float speedKmh = BikeGauge.SpeedKmh(bike);

            if (speedImage != null && speedSprites != null &&
                speedSprites.Length == BikeGauge.FrameCount)
            {
                // Lerp the display value towards the target, this is what makes it smooth
                smoothedSpeedT = Mathf.Lerp(smoothedSpeedT,
                                            BikeGauge.TargetFrame(bike, boostSystem),
                                            Time.deltaTime * speedSmoothingRate);

                speedImage.sprite = speedSprites[
                    BikeGauge.DisplayFrame(smoothedSpeedT, bike, redlineFlashPeriod)];
                speedImage.color  = Color.white;
            }

            int speedKmhInt = (int)speedKmh;
            if (speedNumber != null && speedKmhInt != lastSpeedKmh)
            {
                speedNumber.text = speedKmhInt.ToString();
                lastSpeedKmh = speedKmhInt;
            }

            if (gearImage != null && gearSprites != null && gearSprites.Length > 0)
            {
                int gear = bike.currentGear;
                if (gear != lastGear)
                {
                    gearImage.sprite = gearSprites[Mathf.Clamp(gear - 1, 0, gearSprites.Length - 1)];
                    if (gearNumber != null) gearNumber.text = gear.ToString();
                    lastGear = gear;
                }
            }
        }

        void UpdateStandingsDisplay()
        {
            if (raceManager == null) return;

            var standings = raceManager.Standings;
            if (standings.Count == 0) return;

            var top1 = standings.Count > 0 ? standings[0].transform : null;
            var top2 = standings.Count > 1 ? standings[1].transform : null;
            var top3 = standings.Count > 2 ? standings[2].transform : null;

            // Only rebuild when the top 3 order changes
            if (top1 == _lastTop1 && top2 == _lastTop2 && top3 == _lastTop3) return;
            _lastTop1 = top1; _lastTop2 = top2; _lastTop3 = top3;

            SetSlot(p1Pos1Text, p2Pos1Text, RacerDisplayName(standings.Count > 0 ? standings[0] : null));
            SetSlot(p1Pos2Text, p2Pos2Text, RacerDisplayName(standings.Count > 1 ? standings[1] : null));
            SetSlot(p1Pos3Text, p2Pos3Text, RacerDisplayName(standings.Count > 2 ? standings[2] : null));
        }

        void ClearStandings()
        {
            SetSlot(p1Pos1Text, p2Pos1Text, "");
            SetSlot(p1Pos2Text, p2Pos2Text, "");
            SetSlot(p1Pos3Text, p2Pos3Text, "");
            _lastTop1 = _lastTop2 = _lastTop3 = null;
        }

        static void SetSlot(TMP_Text t1, TMP_Text t2, string value)
        {
            if (t1 != null) t1.text = value;
            if (t2 != null) t2.text = value;
        }

        string RacerDisplayName(RaceManager.RacerInfo racer)
        {
            if (racer == null) return "";
            if (racer.transform == raceManager.playerTransform)  return _isSplitScreen ? "P1" : "YOU";
            if (racer.transform == raceManager.playerTransform2) return "P2";
            var root = racer.ai != null ? racer.ai.gameObject : racer.transform.gameObject;
            var switcher = root.GetComponentInChildren<RiderSwitch>(true);
            string character = switcher != null ? switcher.CharacterName : null;
            return string.IsNullOrEmpty(character) ? root.name : character;
        }

        void UpdateBoost(BoostSystem boost, Image left, Image right, ref Color currentColour, ref float lastMeter)
        {
            if (boost == null) return;

            float meter   = boost.boostMeter;
            bool  draining = boost.isBoosting;
            bool  gaining  = meter > lastMeter + 0.001f;
            lastMeter = meter;

            Color target  = draining ? boostDrainColour : gaining ? boostGainColour : boostIdleColour;
            currentColour = Color.Lerp(currentColour, target, Time.deltaTime * boostColourSpeed);

            if (left  != null) { left.fillAmount  = meter; left.color  = currentColour; }
            if (right != null) { right.fillAmount = meter; right.color = currentColour; }
        }
    }
}
