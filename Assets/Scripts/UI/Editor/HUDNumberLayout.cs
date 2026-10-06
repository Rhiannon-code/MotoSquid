using MotoSquid.Rider;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MotoSquid.UI
{
    public static class HUDNumberLayout
    {
        const float SpriteW = 933f;
        const float SpriteH = 365f;

        readonly struct Slot
        {
            public readonly float X, Y, Width, Em;
            public readonly bool  Right; 

            public Slot(float x, float y, float width, float em, bool right)
            {
                X = x; Y = y; Width = width; Em = em; Right = right;
            }
        }

        static readonly Slot Speed = new Slot(578f, 175f, 400f, 88f, true);
        static readonly Slot Gear = new Slot(757f, 236f, 200f, 62f, true);
        static readonly Slot[] Standings =
        {
            new Slot(330f,  64.5f, 330f, 37f, false),
            new Slot(410f, 145f,   330f, 37f, false),
            new Slot(490f, 224f,   330f, 37f, false),
        };

        [MenuItem("MotoSquid/HUD/Apply HUD Number Layout")]
        static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("HUD number layout", "Exit Play mode first, changes made in Play mode are discarded.", "OK");
                return;
            }

            var hud = Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
            if (hud == null)
            {
                EditorUtility.DisplayDialog("HUD number layout", "No HUDManager in the open scene.", "OK");
                return;
            }

            var so      = new SerializedObject(hud);
            int applied = 0;

            foreach (string player in new[] { "p1", "p2" })
            {
                applied += Place(so, player + "SpeedNumber", Speed, false);
                applied += Place(so, player + "GearNumber",  Gear,  false);
                for (int i = 0; i < Standings.Length; i++)
                    applied += Place(so, $"{player}Pos{i + 1}Text", Standings[i], true);
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"HUD number layout: placed {applied} of 10 readouts. Save the scene to keep it.", hud);
        }

        static int Place(SerializedObject hud, string field, Slot slot, bool autoSize)
        {
            var text = hud.FindProperty(field)?.objectReferenceValue as TMP_Text;
            if (text == null)
            {
                Debug.LogWarning($"HUD number layout: {field} is not assigned on the HUDManager, skipped.");
                return 0;
            }

            RectTransform rt     = text.rectTransform;
            RectTransform parent = rt.parent as RectTransform;
            if (parent == null)
            {
                Debug.LogWarning($"HUD number layout: {field} has no RectTransform parent to place it against, skipped.");
                return 0;
            }

            Undo.RecordObject(rt,   "Apply HUD number layout");
            Undo.RecordObject(text, "Apply HUD number layout");

            float w = parent.rect.width, h = parent.rect.height;
            float toLocalX = w / SpriteW, toLocalY = h / SpriteH;
            float parentX = parent.localScale.x;
            float unsquash = Mathf.Approximately(parentX, 0f) ? 1f : parent.localScale.y / parentX;
            rt.localScale = new Vector3(unsquash, 1f, 1f);

            Vector2 anchor = new Vector2(slot.Right ? 1f : 0f, 0f);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = new Vector2(anchor.x, 0.5f);

            Vector2 target = new Vector2(slot.X * toLocalX, (1f - slot.Y / SpriteH) * h)
                           - new Vector2(parent.pivot.x * w, parent.pivot.y * h);
            Vector2 anchorPoint = new Vector2((anchor.x - parent.pivot.x) * w, -parent.pivot.y * h);
            rt.anchoredPosition = target - anchorPoint;

            float fontSize = slot.Em * toLocalY;
            rt.sizeDelta = new Vector2(slot.Width * toLocalX / unsquash, fontSize * 1.6f);

            text.enableAutoSizing   = false;
            text.fontSize           = fontSize;
            text.alignment          = slot.Right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            text.textWrappingMode   = TextWrappingModes.NoWrap;
            text.overflowMode       = autoSize ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            if (autoSize)
            {
                text.fontSizeMin      = fontSize * 0.55f;
                text.fontSizeMax      = fontSize;
                text.enableAutoSizing = true;
            }

            EditorUtility.SetDirty(rt);
            EditorUtility.SetDirty(text);
            return 1;
        }
    }
}
