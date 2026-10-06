using MotoSquid.Audio;
using MotoSquid.DevTools;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.UI
{
    public static class AnimatedLogoBuilder
    {
        const string LogoDir = "Assets/Textures/Logo/";
        const string Mine = "Assets/Textures/Logo/";

        static readonly Vector2 DesignSize = new Vector2(5120f, 2160f);

        struct Layer
        {
            public string path;
            public string node;
            public float depth;
            public Vector2 size;
            public Vector2 pivot;
            public Vector2 position;
            public float spin;
            public bool glitch;
            public LogoIntroReveal.Style reveal;
            public float revealDelay;
            public float revealDuration;
            public int maxTextureSize;
            public Color solid;
        }

        const LogoIntroReveal.Style NoReveal = (LogoIntroReveal.Style)(-1);

        static Layer Plate(string path, string node, float depth, int maxSize = 4096, bool glitch = false)
        {
            return new Layer
            {
                path = path, node = node, depth = depth,
                size = DesignSize, pivot = new Vector2(0.5f, 0.5f), position = Vector2.zero,
                glitch = glitch, reveal = NoReveal, maxTextureSize = maxSize, solid = Color.white
            };
        }

        static Layer Reveal(Layer layer, LogoIntroReveal.Style style, float delay, float duration)
        {
            layer.reveal = style;
            layer.revealDelay = delay;
            layer.revealDuration = duration;
            return layer;
        }

        static Layer Placed(Layer layer, Vector2 size, Vector2 pivot, Vector2 position)
        {
            layer.size = size;
            layer.pivot = pivot;
            layer.position = position;
            return layer;
        }

        // Gear hubs are the circle fit through their tooth tips, not the texture centre, so they turn
        // about the sprocket rather than wobbling. Speeds are in the hubs' radius ratio, 210.1 / 136.2.
        static Layer BigGear()
        {
            Layer l = Placed(Plate(LogoDir + "Loading_Screen_Layer3BIGGear.png", "L04_BigGear", 0.45f, 512),
                             new Vector2(476f, 478f), new Vector2(0.49996f, 0.48751f), new Vector2(-318.02f, 240.03f));
            l.spin = -20.00f;
            return l;
        }

        static Layer SmallGear()
        {
            Layer l = Placed(Plate(LogoDir + "Loading_Screen_Layer4SmallGear.png", "L05_SmallGear", 0.50f, 512),
                             new Vector2(325f, 325f), new Vector2(0.50212f, 0.51578f), new Vector2(285.19f, -275.37f));
            l.spin = -30.86f;
            return l;
        }

        static List<Layer> LoadingScreenLayers()
        {
            return new List<Layer>
            {
                Plate(Mine + "BaseNoSkid.png", "L01_Base", 0.00f),
                Reveal(Plate(Mine + "Skid.png", "L02_Skid", 0.05f, 2048),
                       LogoIntroReveal.Style.WipeRight, 0.15f, 1.0f),
                Plate(LogoDir + "Loading_Screen_Layer2SquidTop.png",        "L03_SquidTop",    0.65f, 4096, true),
                BigGear(),
                SmallGear(),
                Plate(LogoDir + "Loading_Screen_Layer5Doodles.png",         "L06_Doodles",     0.85f, 4096, true),
                Plate(LogoDir + "Loading_Screen_Layer6MSJapanese.png",      "L07_Japanese",    0.12f),
                Plate(LogoDir + "Loading_Screen_Layer7SquidBottom.png",     "L08_SquidBottom", 0.70f, 4096, true),
                Plate(LogoDir + "Loading_Screen_Layer8MSEnglish.png",       "L09_Wordmark",    0.20f, 4096, true),
                Reveal(Plate(LogoDir + "Loading_Screen_Layer9ForegroundBlood.png", "L10_Blood", 1.00f),
                       LogoIntroReveal.Style.Splatter, 0.95f, 0.5f),
            };
        }

        // LogoAlpha is 15000x8438 with its artwork inside (2821,2225)-(11340,6642). Scaled so that
        // artwork spans 55% of the design width, and nudged so its centre lands on the design centre
        // rather than the image centre, which sits 419.5 x 214.5 native pixels away from it.
        static List<Layer> SplashLayers()
        {
            Layer backdrop = Plate(null, "S1_Backdrop", 0f);
            backdrop.solid = Color.black;

            return new List<Layer>
            {
                backdrop,
                Reveal(Plate(Mine + "SkidFilled.png", "S2_Skid", 0.05f, 2048),
                       LogoIntroReveal.Style.WipeRight, 0.25f, 1.1f),
                Reveal(Placed(Plate(LogoDir + "LogoAlphaReversed_.png", "S3_Logo", 0.20f, 4096, true),
                              new Vector2(4958.3f, 2789.4f), new Vector2(0.5f, 0.5f), new Vector2(138.67f, 70.90f)),
                       LogoIntroReveal.Style.Fade, 0.10f, 0.7f),
                Reveal(Plate(LogoDir + "Loading_Screen_Layer9ForegroundBlood.png", "S4_Blood", 1.00f),
                       LogoIntroReveal.Style.Splatter, 1.10f, 0.5f),
            };
        }

        [MenuItem("MotoSquid/UI/Fix Logo Layer Imports")]
        static void FixImports()
        {
            List<Layer> all = LoadingScreenLayers();
            all.AddRange(SplashLayers());

            if (!EditorUtility.DisplayDialog("Fix Logo Layer Imports",
                    "This rewrites the .meta import settings of the logo PNGs, including 10 in " +
                    "Assets/Textures/Logo/, and reimports them.\n\n" +
                    "Commit first.\n\nContinue?", "Reimport", "Cancel"))
                return;

            HashSet<string> done = new HashSet<string>();
            int changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (Layer layer in all)
                    if (layer.path != null && done.Add(layer.path) && ApplyImportSettings(layer)) changed++;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
            Debug.Log($"[AnimatedLogoBuilder] Reimported {changed} of {done.Count} logo textures as sprites.");
        }

        static bool ApplyImportSettings(Layer layer)
        {
            TextureImporter importer = AssetImporter.GetAtPath(layer.path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[AnimatedLogoBuilder] No texture importer at '{layer.path}'. " +
                               "If it is one of the derived files, run tools/extract-skid.py first.");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = layer.maxTextureSize;
            importer.SaveAndReimport();
            return true;
        }

        [MenuItem("MotoSquid/UI/Build Loading Screen Logo (Selected Parent)")]
        static void BuildLoadingScreen()
        {
            Build("MotoSquidLogo", LoadingScreenLayers());
        }

        [MenuItem("MotoSquid/UI/Build Splash Logo (Selected Parent)")]
        static void BuildSplash()
        {
            Build("MotoSquidSplashLogo", SplashLayers());
        }

        static void Build(string rootName, List<Layer> layers)
        {
            RectTransform parent = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<RectTransform>()
                : null;

            if (parent == null)
            {
                EditorUtility.DisplayDialog("Build Animated Logo",
                    "Select the RectTransform the logo should live under first, such as the Canvas.", "OK");
                return;
            }

            Transform existing = parent.Find(rootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            RectTransform root = NewRect(rootName, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = DesignSize;
            root.SetAsFirstSibling();

            LogoCoverFit fit = Undo.AddComponent<LogoCoverFit>(root.gameObject);
            fit.designSize = DesignSize;
            Undo.AddComponent<LogoParallaxRig>(root.gameObject);

            int missing = 0;
            foreach (Layer layer in layers)
            {
                RectTransform rect = NewRect(layer.node, root);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = layer.pivot;
                rect.sizeDelta = layer.size;
                rect.anchoredPosition = layer.position;

                Image image = Undo.AddComponent<Image>(rect.gameObject);
                image.raycastTarget = false;
                image.preserveAspect = false;
                image.color = layer.solid;

                if (layer.path != null)
                {
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(layer.path);
                    if (image.sprite == null)
                    {
                        Debug.LogError($"[AnimatedLogoBuilder] '{layer.path}' has no Sprite. " +
                                       "Run MotoSquid/UI/Fix Logo Layer Imports first.");
                        missing++;
                    }
                }

                Undo.AddComponent<LogoParallaxLayer>(rect.gameObject).depth = layer.depth;

                if (layer.spin != 0f)
                    Undo.AddComponent<LogoGearSpin>(rect.gameObject).degreesPerSecond = layer.spin;

                if (layer.glitch)
                    Undo.AddComponent<LogoGlitch>(rect.gameObject);

                if (layer.reveal != NoReveal)
                {
                    LogoIntroReveal intro = Undo.AddComponent<LogoIntroReveal>(rect.gameObject);
                    intro.style = layer.reveal;
                    intro.delay = layer.revealDelay;
                    intro.duration = layer.revealDuration;
                }
            }

            Undo.SetCurrentGroupName("Build Animated Logo");
            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

            if (missing > 0)
                Debug.LogWarning($"[AnimatedLogoBuilder] Built '{rootName}' but {missing} layer(s) had no sprite.");
            else
                Debug.Log($"[AnimatedLogoBuilder] Built '{rootName}' with {layers.Count} layers under '{parent.name}'.");
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build Animated Logo");
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
