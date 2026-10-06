using MotoSquid.Combat;
using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MotoSquid.DevTools
{
    public class RigLabCharacterSelect : MonoBehaviour
    {
        public RacerRoster roster;
        public string raceScene = "Main_Demo";
        public Transform[] previewAnchors = new Transform[0];
        public float previewSpin = 20f;
        public int aiRacers;

        int players = 1;
        readonly List<Pick> picks = new List<Pick>();
        readonly List<GameObject> previews = new List<GameObject>();
        Transform panelRoot;

        class Pick
        {
            public RacerRoster.Character character;
            public RacerRoster.Bike bike;
            public bool Complete => character != null && bike != null;
        }

        void Awake()
        {
            if (roster == null) { Debug.LogError("[Select] no roster assigned, nothing to pick from.", this); return; }
            BuildCanvas();
            ShowPlayerCount();
        }

        void Update()
        {
            if (previewSpin == 0f) return;
            foreach (var p in previews) if (p != null) p.transform.Rotate(0f, previewSpin * Time.deltaTime, 0f);
        }

        void ShowPlayerCount()
        {
            Clear();
            Heading("How many of you?");
            var row = Row();
            Button(row, "1 Player", true, () => { players = 1; ShowPicking(); });
            Button(row, "2 Players, split screen", true, () => { players = 2; ShowPicking(); });
        }

        void ShowPicking()
        {
            picks.Clear();
            for (int i = 0; i < players; i++) picks.Add(new Pick());
            Refresh();
        }

        void Refresh()
        {
            Clear();
            Heading(players == 1 ? "Pick your racer" : "Pick your racers");

            for (int i = 0; i < picks.Count; i++)
            {
                int p = i;
                if (players > 1) Label("Player " + (p + 1));

                var cRow = Row();
                foreach (var c in roster.characters)
                {
                    if (c == null) continue;
                    var chosen = c;
                    Button(cRow, c.displayName + (c.Ready ? "" : "\n(coming soon)"), c.Ready,
                           () => { picks[p].character = chosen; Refresh(); },
                           picks[p].character == chosen);
                }

                var bRow = Row();
                foreach (var b in roster.bikes)
                {
                    if (b == null) continue;
                    var chosen = b;
                    Button(bRow, b.displayName + (b.Ready ? "" : "\n(coming soon)"), b.Ready,
                           () => { picks[p].bike = chosen; Refresh(); },
                           picks[p].bike == chosen);
                }
            }

            bool ready = picks.All(p => p.Complete);
            var go = Row();
            Button(go, ready ? "RACE" : "Pick a character and a bike", ready, Start_);
            Button(go, "Back", true, ShowPlayerCount);

            ShowPreviews();
        }

        void Start_()
        {
            RaceSelection.Clear();
            for (int i = 0; i < picks.Count; i++)
                RaceSelection.SetPlayer(i, picks[i].character.displayName, picks[i].bike.displayName);
            RaceSelection.AiRacers = aiRacers > 0 ? aiRacers : roster.aiRacers;

            if (Application.CanStreamedLevelBeLoaded(raceScene)) SceneManager.LoadScene(raceScene);
            else Debug.LogError("[Select] '" + raceScene + "' is not in Build Settings, the race cannot start.", this);
        }

        void ShowPreviews()
        {
            foreach (var p in previews) if (p != null) Destroy(p);
            previews.Clear();
            if (previewAnchors == null) return;

            for (int i = 0; i < picks.Count && i < previewAnchors.Length; i++)
            {
                if (!picks[i].Complete || previewAnchors[i] == null) continue;

                var name = RacerRoster.PrefabName(picks[i].character, picks[i].bike, false);
                var prefab = Resources.Load<GameObject>("Racers/" + name);
                if (prefab == null) { Debug.LogWarning("[Select] no prefab 'Racers/" + name + "' to preview.", this); continue; }

                var go = Instantiate(prefab, previewAnchors[i].position, previewAnchors[i].rotation);
                go.name = name + " (preview)";
                Freeze(go);
                previews.Add(go);
            }
        }

        static void Freeze(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && !(b is RiderSwitch)) b.enabled = false;
            foreach (var cam in go.GetComponentsInChildren<Camera>(true)) cam.gameObject.SetActive(false);
            foreach (var l in go.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("Select Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }

            var panel = new GameObject("Panel", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(canvasGo.transform, false);

            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 40f);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            panel.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            panelRoot = panel.transform;
        }

        void Clear()
        {
            for (int i = panelRoot.childCount - 1; i >= 0; i--) Destroy(panelRoot.GetChild(i).gameObject);
        }

        void Heading(string text) => Text(text, 54);
        void Label(string text) => Text(text, 30);

        void Text(string text, int size)
        {
            var go = new GameObject("Text", typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(panelRoot, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = Font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            go.GetComponent<LayoutElement>().minHeight = size + 14;
        }

        Transform Row()
        {
            var go = new GameObject("Row", typeof(HorizontalLayoutGroup));
            go.transform.SetParent(panelRoot, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return go.transform;
        }

        void Button(Transform row, string text, bool usable, System.Action onClick, bool selected = false)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(row, false);

            var image = go.GetComponent<Image>();
            image.color = !usable ? new Color(0.18f, 0.18f, 0.2f, 0.85f)
                        : selected ? new Color(0.95f, 0.45f, 0.1f, 0.95f)
                                   : new Color(0.12f, 0.14f, 0.2f, 0.95f);

            var element = go.GetComponent<LayoutElement>();
            element.minWidth = 260f;
            element.minHeight = 90f;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = usable;
            if (usable) button.onClick.AddListener(() => onClick());

            var label = new GameObject("Label", typeof(Text));
            label.transform.SetParent(go.transform, false);
            var t = label.GetComponent<Text>();
            t.text = text;
            t.font = Font;
            t.fontSize = 26;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.35f);

            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 6f);
            lrt.offsetMax = new Vector2(-10f, -6f);
        }

        static Font _font;
        static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }
    }
}
