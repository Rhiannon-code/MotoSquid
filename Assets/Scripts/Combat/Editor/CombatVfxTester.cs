using MotoSquid.Bike;
using UnityEngine;
using UnityEditor;

namespace MotoSquid.Combat
{
    public class CombatVfxTester : EditorWindow
    {
        GameObject m_Bike;
        float m_Ahead = 2f;
        int   m_Side  = 1;

        [MenuItem("MotoSquid/Combat/Test Combat VFX")]
        static void Open() => GetWindow<CombatVfxTester>("Combat VFX").minSize = new Vector2(320f, 200f);

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode, pick a bike from the scene, then fire. The effect plays through the " +
                "same VfxBurst the fight uses, so what you see is what a real hit looks like.",
                MessageType.Info);

            m_Bike = (GameObject)EditorGUILayout.ObjectField("Bike", m_Bike, typeof(GameObject), true);
            if (GUILayout.Button("Use Selection") && Selection.activeGameObject != null)
                m_Bike = Selection.activeGameObject;

            EditorGUILayout.Space();
            m_Ahead = EditorGUILayout.Slider("Metres Ahead", m_Ahead, 0f, 6f);
            m_Side  = EditorGUILayout.IntPopup("Side", m_Side, new[] { "Left", "Right" }, new[] { -1, 1 });

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Fire Hit Impact"))  EditorApplication.delayCall += () => Fire(true);
                if (GUILayout.Button("Fire Swing Whoosh")) EditorApplication.delayCall += () => Fire(false);
                EditorGUILayout.Space();
                if (GUILayout.Button("Diagnose Whoosh")) EditorApplication.delayCall += Diagnose;
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play mode to fire.", MessageType.Warning);
        }

        void Diagnose()
        {
            if (m_Bike == null) { Debug.LogWarning("Combat VFX: pick a bike first."); return; }

            var burst = m_Bike.GetComponentInChildren<VfxBurst>(true);
            if (burst == null) { Debug.LogWarning("Combat VFX: no VfxBurst on that bike."); return; }

            if (burst.swingWhoosh == null) { Debug.LogWarning("Combat VFX: Swing Whoosh slot is empty."); return; }

            var copies = burst.WhooshCopies;
            if (copies == null || copies.Count == 0)
            {
                Debug.LogWarning("Combat VFX: the whoosh pool is empty, so Start never built it. " +
                                 "Are you in Play mode?");
                return;
            }

            var log = new System.Text.StringBuilder($"Whoosh diagnosis for '{m_Bike.name}':");
            for (int i = 0; i < copies.Count; i++)
            {
                GameObject go = copies[i];
                if (go == null) { log.Append($"\n  [{i}] destroyed"); continue; }

                var rends = go.GetComponentsInChildren<Renderer>(true);
                log.Append($"\n  [{i}] '{go.name}' active={go.activeInHierarchy} " +
                           $"parent={(go.transform.parent != null ? go.transform.parent.name : "none")} " +
                           $"pos={go.transform.position} scale={go.transform.lossyScale}");

                if (rends.Length == 0) { log.Append("\n        NO RENDERER on it or any child"); continue; }

                foreach (var r in rends)
                {
                    Material m = r.sharedMaterial;
                    log.Append($"\n        renderer '{r.name}' enabled={r.enabled} " +
                               $"goActive={r.gameObject.activeInHierarchy} " +
                               $"material={(m != null ? m.name : "NONE")} " +
                               $"shader={(m != null && m.shader != null ? m.shader.name : "NONE")} " +
                               $"boundsSize={r.bounds.size}");
                }
            }

            var combat = m_Bike.GetComponentInChildren<CombatSystem>(true);
            if (combat == null) log.Append("\n  NO CombatSystem on this bike");
            else
            {
                int subs = combat.onSwungAt != null ? combat.onSwungAt.GetInvocationList().Length : 0;
                log.Append($"\n  onSwungAt subscribers = {subs}");
                var holder = combat.weaponHolder;
                log.Append(holder == null
                    ? "\n  weaponHolder = NONE on the CombatSystem"
                    : $"\n  weaponHolder ok, HasWeapon={holder.HasWeapon} " +
                      $"PropTransform={(holder.PropTransform != null ? holder.PropTransform.name : "NULL")}");
            }

            var slash = burst.swingWhoosh.GetComponentInChildren<SlashFlash>(true);
            log.Append(slash != null
                ? $"\n  SlashFlash found on the prefab, colorProperty='{slash.colorProperty}' " +
                  $"peakAlpha={slash.peakAlpha} duration={slash.duration}"
                : "\n  NO SlashFlash on the Swing Whoosh prefab - nothing will ever play it");

            Debug.Log(log.ToString(), m_Bike);
        }

        void Fire(bool impact)
        {
            try
            {
                if (m_Bike == null) { Debug.LogWarning("Combat VFX: pick a bike first."); return; }

                var combat = m_Bike.GetComponentInChildren<CombatSystem>(true);
                var burst  = m_Bike.GetComponentInChildren<VfxBurst>(true);
                if (combat == null || burst == null)
                {
                    Debug.LogWarning("Combat VFX: that bike has no CombatSystem and VfxBurst.");
                    return;
                }

                Transform facing = combat.Facing;
                if (impact)
                {
                    if (burst.hitImpacts == null || burst.hitImpacts.Length == 0)
                        Debug.LogWarning("Combat VFX: VfxBurst has no Hit Impacts assigned, so nothing will show.");

                    Vector3 point = combat.transform.position
                                  + facing.right * (m_Side * m_Ahead) + Vector3.up * 0.4f;
                    combat.onHitLandedAt?.Invoke(facing, point, facing.right * m_Side);
                }
                else
                {
                    if (burst.swingWhoosh == null)
                        Debug.LogWarning("Combat VFX: VfxBurst has no Swing Whoosh assigned, so nothing will show.");

                    Transform weapon = combat.weaponHolder != null ? combat.weaponHolder.PropTransform : null;
                    if (weapon == null)
                    {
                        Debug.LogWarning("Combat VFX: no melee weapon equipped, so there is no arc to " +
                                         "ride. A whoosh only fires for an armed punch.", m_Bike);
                        return;
                    }
                    combat.onSwungAt?.Invoke(weapon, m_Side);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
