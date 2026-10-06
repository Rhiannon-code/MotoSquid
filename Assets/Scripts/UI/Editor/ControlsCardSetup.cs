using MotoSquid.Core;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.UI
{
    public static class ControlsCardSetup
    {
        const string GamepadPath  = "Assets/Textures/UI/Controls_Gamepad.png";
        const string KeyboardPath = "Assets/Textures/UI/Controls_Keyboard.png";

        static readonly Vector2 CardAnchorMin = new Vector2(0.05f, 0.26f);
        static readonly Vector2 CardAnchorMax = new Vector2(0.95f, 0.95f);

        [MenuItem("MotoSquid/Loading/Set Up Controls Cards")]
        static void SetUp()
        {
            var loader = Object.FindFirstObjectByType<SceneLoader>();
            if (loader == null)
            {
                Debug.LogError("[ControlsCards] No SceneLoader in the open scene. Open LoadingScreen first.");
                return;
            }

            Sprite gamepad  = AsSprite(GamepadPath);
            Sprite keyboard = AsSprite(KeyboardPath);
            if (gamepad == null || keyboard == null) return;

            var canvas = loader.GetComponentInChildren<Canvas>()
                      ?? Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ControlsCards] No Canvas in the open scene.");
                return;
            }

            // Background carried the old backdrop art, so it becomes a plain black fill.
            Image backdrop = MakeBlackBackdrop(canvas);

            // The logo is NOT one image: it is a ten-layer parallax rig with a LogoCoverFit that refits it
            // to the screen every frame. Renaming its root and swapping the root's sprite (what this tool
            // used to do) changed nothing visible, because all ten child layers kept drawing over it. It
            // gets switched off whole, and the cards are built as their own objects.
            DisableLogoRig(canvas);

            Image gamepadCard  = EnsureCard(canvas, "ControlsCard_Gamepad",  gamepad);
            Image keyboardCard = EnsureCard(canvas, "ControlsCard_Keyboard", keyboard);
            if (gamepadCard == null || keyboardCard == null) return;

            HideStrays(canvas, backdrop, gamepadCard, keyboardCard);

            // Black, then the two cards, then the bar, tip and fade over the top
            Order(backdrop, 0);
            Order(gamepadCard, 1);
            Order(keyboardCard, 2);

            var show = canvas.GetComponent<ControlsCardShow>()
                    ?? Undo.AddComponent<ControlsCardShow>(canvas.gameObject);

            Undo.RecordObject(show, "Set Up Controls Cards");
            show.cards          = new[] { gamepadCard, keyboardCard };
            show.secondsPerCard = 4f;
            show.crossFade      = 0.5f;
            EditorUtility.SetDirty(show);

            var so   = new SerializedObject(loader);
            var prop = so.FindProperty("controlsCards");
            if (prop != null)
            {
                prop.objectReferenceValue = show;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(loader);
            }

            LayoutBottomStack(canvas, loader);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            WarnAboutScaler(canvas);

            Debug.Log("[ControlsCards] Gamepad then keyboard, 4 s each with a 0.5 s cross-fade " +
                      $"({show.TotalDuration} s total). The loading screen now holds for at least that " +
                      "long. Save the scene to persist.");
        }

        // The bar and the tip were authored against a Constant Pixel Size canvas and overlapped each
        // other: the bar sat at y 133-173 and the tip started at y 153. Both are re-anchored as a
        // fraction of the width so they scale with the canvas, keeping their own heights, and stacked so
        // they clear each other and sit under the card.
        static void LayoutBottomStack(Canvas canvas, SceneLoader loader)
        {
            var bar = FindUnder(canvas, "Progress Bar");
            if (bar != null) Place(bar, 0.20f, 0.80f, y: 60f, height: 40f);

            var so  = new SerializedObject(loader);
            var tip = so.FindProperty("loadingTipText")?.objectReferenceValue as Component;
            if (tip != null) Place((RectTransform)tip.transform, 0.10f, 0.90f, y: 120f, height: 110f);

            // Above the cards, below the fade panel
            if (bar != null) bar.SetAsLastSibling();
            if (tip != null) tip.transform.SetAsLastSibling();

            var fade = FindUnder(canvas, "FadePanel");
            if (fade != null) fade.SetAsLastSibling();

            Debug.Log("[ControlsCards] Bottom stack re-anchored: progress bar 20-80% of width at y 60, " +
                      "tip 10-90% at y 120, both drawn over the cards.");
        }

        static void Order(Image image, int index)
        {
            if (image != null) image.rectTransform.SetSiblingIndex(index);
        }

        static Image MakeBlackBackdrop(Canvas canvas)
        {
            var rt = canvas.transform.Find("Background") as RectTransform
                  ?? canvas.transform.Find("LoadingBackdrop") as RectTransform;

            if (rt == null)
            {
                var go = new GameObject("LoadingBackdrop", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Set Up Controls Cards");
                rt = (RectTransform)go.transform;
                rt.SetParent(canvas.transform, worldPositionStays: false);
            }

            Undo.RecordObject(rt.gameObject, "Set Up Controls Cards");
            rt.name = "LoadingBackdrop";

            Undo.RecordObject(rt, "Set Up Controls Cards");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var image = rt.GetComponent<Image>() ?? Undo.AddComponent<Image>(rt.gameObject);
            Undo.RecordObject(image, "Set Up Controls Cards");
            image.sprite        = null;
            image.color         = Color.black;
            image.raycastTarget = false;
            image.enabled       = true;
            rt.gameObject.SetActive(true);

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(rt);
            return image;
        }

        static void DisableLogoRig(Canvas canvas)
        {
            foreach (var rig in canvas.GetComponentsInChildren<LogoParallaxRig>(true))
            {
                Undo.RecordObject(rig.gameObject, "Set Up Controls Cards");
                rig.gameObject.name = "MotoSquidLogo";      // an earlier run may have renamed it
                rig.gameObject.SetActive(false);
                EditorUtility.SetDirty(rig.gameObject);
                Debug.LogWarning("[ControlsCards] Disabled the animated logo rig " +
                                 $"('{rig.name}', {rig.transform.childCount} layers).", rig);
            }
        }

        static Image EnsureCard(Canvas canvas, string name, Sprite sprite)
        {
            var rt = canvas.transform.Find(name) as RectTransform;
            if (rt == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Set Up Controls Cards");
                rt = (RectTransform)go.transform;
                rt.SetParent(canvas.transform, worldPositionStays: false);
            }

            Undo.RecordObject(rt, "Set Up Controls Cards");
            rt.anchorMin        = CardAnchorMin;
            rt.anchorMax        = CardAnchorMax;
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.offsetMin        = Vector2.zero;
            rt.offsetMax        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale       = Vector3.one;

            var image = rt.GetComponent<Image>() ?? Undo.AddComponent<Image>(rt.gameObject);
            Undo.RecordObject(image, "Set Up Controls Cards");
            image.sprite         = sprite;
            image.type           = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget  = false;
            image.color          = new Color(1f, 1f, 1f, 0f);   // the sequencer fades them up
            image.enabled        = true;
            rt.gameObject.SetActive(true);

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(rt);
            return image;
        }

        // Whitelist, not "has an Image": the logo rig's root carried no visible graphic of its own while
        // ten children did, so anything checking the child itself walked straight past it.
        static readonly string[] Keep =
        {
            "LoadingBackdrop", "ControlsCard_Gamepad", "ControlsCard_Keyboard",
            "Progress Bar", "TipsText", "PercentageText", "FadePanel"
        };

        static void HideStrays(Canvas canvas, params Image[] _)
        {
            foreach (Transform child in canvas.transform)
            {
                if (Keep.Contains(child.name) || !child.gameObject.activeSelf) continue;
                if (child.GetComponentInChildren<Graphic>(true) == null) continue;

                Debug.LogWarning($"[ControlsCards] Disabling leftover loading screen object '{child.name}'.", child);
                Undo.RecordObject(child.gameObject, "Set Up Controls Cards");
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
            }
        }

        static RectTransform FindUnder(Canvas canvas, string name) =>
            canvas.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);

        // Stretched horizontally, fixed height, measured up from the bottom edge
        static void Place(RectTransform rt, float minX, float maxX, float y, float height)
        {
            Undo.RecordObject(rt, "Set Up Controls Cards");
            rt.anchorMin        = new Vector2(minX, 0f);
            rt.anchorMax        = new Vector2(maxX, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.sizeDelta        = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.localScale       = Vector3.one;
            EditorUtility.SetDirty(rt);
        }

        // This UI is authored in ~1920x1080 pixels (a 1000 px progress bar, 1500 px cards), so switching
        // the scaler to Scale With Screen Size against the stock 800x600 reference blows every element up
        // by 2.4x. Worth saying out loud rather than leaving it to be discovered at 2.4x.
        static void WarnAboutScaler(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;

            Vector2 r = scaler.referenceResolution;
            if (Mathf.Approximately(r.x, 1920f) && Mathf.Approximately(r.y, 1080f)) return;

            Debug.LogWarning($"[ControlsCards] Canvas Scaler is Scale With Screen Size at {r.x}x{r.y}. " +
                             "This UI is authored around 1920x1080, so set Reference Resolution to that " +
                             "or every element is scaled by the difference.", scaler);
        }

        // A Default texture yields no Sprite at all, and Image.sprite silently stays null
        static Sprite AsSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ControlsCards] '{path}' is not in the project. Generate the cards first " +
                               "with Local Docs/MotoSquid/tools/controls-card/make_controls_cards.py.");
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType       = TextureImporterType.Sprite;
                importer.spriteImportMode  = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize    = 2048;
                importer.SaveAndReimport();
                Debug.Log($"[ControlsCards] Re-imported '{System.IO.Path.GetFileName(path)}' as a Sprite.");
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogError($"[ControlsCards] '{path}' still has no Sprite after reimport.");
            return sprite;
        }

    }
}
