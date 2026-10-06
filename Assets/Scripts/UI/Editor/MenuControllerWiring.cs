using System.Collections.Generic;
using System.Linq;
using Michsky.UI.Heat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.UI
{
    public static class MenuControllerWiring
    {
        static readonly string[] RowOrder = { "charactername", "bikename", "gamemode" };

        [MenuItem("MotoSquid/Menu/Wire Controller Navigation")]
        static void Wire()
        {
            var selectors = Object.FindObjectsByType<HorizontalSelector>(
                                      FindObjectsInactive.Include, FindObjectsSortMode.None)
                                  .Where(s => s != null)
                                  .ToList();

            var start = Object.FindObjectsByType<ButtonManager>(
                                  FindObjectsInactive.Include, FindObjectsSortMode.None)
                              .FirstOrDefault(b => b != null && b.name.Contains("Start"));

            if (selectors.Count == 0 && start == null)
            {
                Debug.LogError("[MenuWiring] No HorizontalSelector and no Start ButtonManager in the open " +
                               "scene. Open MainMenu first.");
                return;
            }

            var ordered = new List<HorizontalSelector>();
            foreach (string key in RowOrder)
            {
                var match = selectors.FirstOrDefault(s => RowName(s).Contains(key));
                if (match == null)
                {
                    Debug.LogWarning($"[MenuWiring] No selector matching '{key}'. The chain will skip it.");
                    continue;
                }
                ordered.Add(match);
                selectors.Remove(match);
            }
            // Anything the scene has gained since goes on the end, top to bottom, rather than vanishing
            ordered.AddRange(selectors.OrderByDescending(s => s.transform.position.y));

            var chain = new List<GameObject>();

            foreach (var selector in ordered)
            {
                EnsureSelectable(selector.gameObject, tint: true);
                EnsurePadInput(selector);
                chain.Add(selector.gameObject);
            }

            if (start != null)
            {
                // Heat's ButtonManager already draws its own selected state, so this one is left untinted
                EnsureSelectable(start.gameObject, tint: false);
                chain.Add(start.gameObject);
            }

            LinkChain(chain);

            if (start != null)
            {
                // Heat drives its own navigation through these, so it is pointed the same way as the
                // Selectable chain rather than fighting it
                Undo.RecordObject(start, "Wire Controller Navigation");
                start.useUINavigation = true;
                int i = chain.IndexOf(start.gameObject);
                start.selectOnUp   = chain[(i - 1 + chain.Count) % chain.Count];
                start.selectOnDown = chain[(i + 1) % chain.Count];
                EditorUtility.SetDirty(start);
            }

            SetFirstSelected(chain.FirstOrDefault());

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[MenuWiring] Wired {chain.Count} row(s), up/down in this order:\n   " +
                      string.Join("\n   ", chain.Select(g => RowName(g.transform))) +
                      $"\nStarts on '{(chain.Count > 0 ? RowName(chain[0].transform) : "nothing")}'. Left/right changes the value. " +
                      "Save the scene to persist.");
        }

        static string Normalise(string name) => name.ToLowerInvariant().Replace(" ", "");

        // Every selector object is literally named "Horizontal Selector", so the row's identity lives on
        // the nearest ancestor that names one ("Bike Name Selector", "GameMode Selector", ...).
        static string RowName(Component c)
        {
            for (Transform t = c.transform; t != null; t = t.parent)
            {
                string n = Normalise(t.name);
                if (n.Contains("selector") && n != "horizontalselector") return n;
            }
            return Normalise(c.name);
        }

        static void EnsureSelectable(GameObject go, bool tint)
        {
            var button = go.GetComponent<Button>();
            if (button == null) button = Undo.AddComponent<Button>(go);

            Undo.RecordObject(button, "Wire Controller Navigation");

            if (!tint)
            {
                button.transition = Selectable.Transition.None;
                EnsureRaycastTarget(go);
                EditorUtility.SetDirty(button);
                EditorUtility.SetDirty(go);
                return;
            }

            // Its own child, never the row's existing Image: tinting that one drove an authored graphic
            // to alpha 0, and a white band across the whole row washed out the prev/next arrows on
            // whichever row was selected. Behind everything, and faint.
            NormaliseArrowHighlights(go);

            var highlight = EnsureHighlight(go);

            var rowImage = go.GetComponent<Image>();
            if (rowImage != null && rowImage != highlight && rowImage.sprite == null)
            {
                rowImage.color         = new Color(0f, 0f, 0f, 0f);
                rowImage.raycastTarget = true;
                EditorUtility.SetDirty(rowImage);
            }

            button.transition    = Selectable.Transition.ColorTint;
            button.targetGraphic = highlight;

            var colors = button.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.15f);
            colors.selectedColor    = new Color(1f, 1f, 1f, 0.25f);
            colors.pressedColor     = new Color(1f, 1f, 1f, 0.35f);
            colors.disabledColor    = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration     = 0.1f;
            button.colors = colors;

            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(go);
        }

        static void NormaliseArrowHighlights(GameObject row)
        {
            foreach (string arrow in new[] { "Prev Button", "Next Button" })
            {
                var filler = row.transform.Find($"{arrow}/Highlight/Filler");
                var image  = filler != null ? filler.GetComponent<Image>() : null;
                if (image == null || image.color == Color.white) continue;

                Debug.LogWarning($"[MenuWiring] '{row.transform.parent?.name}' {arrow} highlight filler " +
                                 $"was {image.color}, setting it to white to match the other rows.", image);

                Undo.RecordObject(image, "Wire Controller Navigation");
                image.color = Color.white;
                EditorUtility.SetDirty(image);
            }
        }

        const string HighlightName = "[Nav Highlight]";

        static Image EnsureHighlight(GameObject row)
        {
            var existing = row.transform.Find(HighlightName);
            if (existing != null) return existing.GetComponent<Image>();

            var child = new GameObject(HighlightName, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(child, "Wire Controller Navigation");

            var rt = (RectTransform)child.transform;
            rt.SetParent(row.transform, worldPositionStays: false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();

            var image = child.GetComponent<Image>();
            image.color         = Color.white;
            image.raycastTarget = false;   // the row's own graphic takes the clicks
            return image;
        }

        // Selectable needs something raycastable or it can be selected but never clicked
        static void EnsureRaycastTarget(GameObject go)
        {
            if (go.GetComponent<Graphic>() != null) return;

            var image = Undo.AddComponent<Image>(go);
            image.color         = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
        }

        // Heat's SelectorInputHandler reads ControllerManager.hAxis, which is the right stick. The menu
        // is driven on the D-Pad, so the vendor handler is removed in favour of one that reads it.
        static void EnsurePadInput(HorizontalSelector selector)
        {
            var vendor = selector.GetComponent<SelectorInputHandler>();
            if (vendor != null) Undo.DestroyObjectImmediate(vendor);

            if (selector.GetComponent<SelectorPadInput>() == null)
                Undo.AddComponent<SelectorPadInput>(selector.gameObject);

            EditorUtility.SetDirty(selector.gameObject);
        }

        static void LinkChain(List<GameObject> chain)
        {
            for (int i = 0; i < chain.Count; i++)
            {
                var button = chain[i].GetComponent<Button>();
                if (button == null) continue;

                Undo.RecordObject(button, "Wire Controller Navigation");
                var nav = button.navigation;
                nav.mode         = Navigation.Mode.Explicit;
                nav.selectOnUp   = chain[(i - 1 + chain.Count) % chain.Count].GetComponent<Selectable>();
                nav.selectOnDown = chain[(i + 1) % chain.Count].GetComponent<Selectable>();
                // Left and right belong to the row's own value. Anything here would eat the D-Pad press
                // before SelectorPadInput ever sees it.
                nav.selectOnLeft  = null;
                nav.selectOnRight = null;
                button.navigation = nav;
                EditorUtility.SetDirty(button);
            }
        }

        static void SetFirstSelected(GameObject first)
        {
            if (first == null) return;

            // The splash screen hides Main Content after the binder has selected; Heat reselects this on return
            var controllerManager = Object.FindFirstObjectByType<ControllerManager>();
            if (controllerManager != null)
            {
                Undo.RecordObject(controllerManager, "Wire Controller Navigation");
                controllerManager.firstSelected = first;
                EditorUtility.SetDirty(controllerManager);
            }

            var binder = Object.FindFirstObjectByType<MenuControllerBinder>();
            if (binder == null)
            {
                Debug.LogWarning("[MenuWiring] No MenuControllerBinder in the scene, so nothing " +
                                 "will be selected on start. Add it to the EventSystem.");
                return;
            }

            var so = new SerializedObject(binder);
            var prop = so.FindProperty("firstSelected");
            if (prop != null)
            {
                prop.objectReferenceValue = first;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(binder);
            }
        }
    }

    public static class SelectorArrowRestore
    {
        [MenuItem("MotoSquid/Menu/Copy Selector Arrow Layout From Selection")]
        static void Copy()
        {
            var reference = SelectedSelector();
            if (reference == null)
            {
                Debug.LogError("[ArrowRestore] Select a good selector row in the Hierarchy first, the " +
                               "Horizontal Selector object, or anything under it. Bike Name Selector or " +
                               "GameMode Selector are the intact ones.");
                return;
            }

            var all = Object.FindObjectsByType<HorizontalSelector>(
                                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int changed = 0;
            foreach (var target in all)
            {
                if (target == null || target == reference) continue;
                foreach (string arrow in new[] { "Prev Button", "Next Button" })
                    changed += CopyArrow(reference.transform, target.transform, arrow);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[ArrowRestore] Copied arrow layout from '{RowLabel(reference.transform)}' to " +
                      $"{all.Length - 1} other row(s), {changed} value(s) changed. Save the scene to persist.");
        }

        [MenuItem("MotoSquid/Menu/Copy Selector Arrow Layout From Selection", true)]
        static bool CopyValidate() => SelectedSelector() != null;

        static HorizontalSelector SelectedSelector()
        {
            var go = Selection.activeGameObject;
            if (go == null) return null;
            return go.GetComponent<HorizontalSelector>()
                ?? go.GetComponentInParent<HorizontalSelector>();
        }

        static string RowLabel(Transform selector) =>
            selector.parent != null ? selector.parent.name : selector.name;

        static int CopyArrow(Transform from, Transform to, string arrow)
        {
            var src = from.Find(arrow) as RectTransform;
            var dst = to.Find(arrow) as RectTransform;
            if (src == null || dst == null) return 0;

            int changed = 0;

            if (dst.anchorMin != src.anchorMin || dst.anchorMax != src.anchorMax ||
                dst.anchoredPosition != src.anchoredPosition || dst.sizeDelta != src.sizeDelta ||
                dst.pivot != src.pivot)
            {
                Undo.RecordObject(dst, "Copy Selector Arrow Layout");
                Debug.LogWarning($"[ArrowRestore] {RowLabel(to)} {arrow}: anchorMax {dst.anchorMax} -> " +
                                 $"{src.anchorMax}, pos {dst.anchoredPosition} -> {src.anchoredPosition}.", dst);

                dst.anchorMin        = src.anchorMin;
                dst.anchorMax        = src.anchorMax;
                dst.pivot            = src.pivot;
                dst.anchoredPosition = src.anchoredPosition;
                dst.sizeDelta        = src.sizeDelta;
                EditorUtility.SetDirty(dst);
                changed++;
            }

            // Disabled, Normal and Highlight all carry their own frame and icon tints
            foreach (string state in new[] { "Disabled", "Normal", "Highlight" })
                foreach (string part in new[] { "Frame", "Icon", "Filler" })
                    changed += CopyColour(src, dst, $"{state}/{part}", to, arrow);

            return changed;
        }

        static int CopyColour(Transform src, Transform dst, string path, Transform row, string arrow)
        {
            var a = src.Find(path)?.GetComponent<Image>();
            var b = dst.Find(path)?.GetComponent<Image>();
            if (a == null || b == null || a.color == b.color) return 0;

            Undo.RecordObject(b, "Copy Selector Arrow Layout");
            Debug.LogWarning($"[ArrowRestore] {RowLabel(row)} {arrow}/{path}: {b.color} -> {a.color}.", b);

            b.color = a.color;
            EditorUtility.SetDirty(b);
            return 1;
        }
    }
}
