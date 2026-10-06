using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MotoSquid.Rider
{
    public static class ProceduralRigDiagnostics
    {
        [MenuItem("MotoSquid/Characters/Diagnose Procedural Rig (Selected)")]
        public static void Diagnose()
        {
            var root = Selection.activeGameObject;
            if (root == null) { Debug.LogError("[Diag] Select the character in the scene."); return; }

            var s = new StringBuilder();
            s.AppendLine($"[Diag] {root.name}, {(Application.isPlaying ? "PLAY MODE" : "EDIT MODE (rigs do not evaluate)")}");

            var animator = root.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);

            if (animator == null) { s.AppendLine("  NO humanoid Animator."); Debug.Log(s); return; }

            s.AppendLine($"  Rider           : '{animator.name}'  parent='{animator.transform.parent?.name}'");
            s.AppendLine($"  Animator        : enabled={animator.enabled} active={animator.gameObject.activeInHierarchy} " +
                         $"controller={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NONE")} " +
                         $"avatar={(animator.avatar != null ? (animator.avatar.isHuman ? "Humanoid" : "Generic") : "NONE")} " +
                         $"cull={animator.cullingMode}");

            if (Application.isPlaying && animator.runtimeAnimatorController != null && animator.layerCount > 0)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                s.AppendLine($"  Animator state  : hash={st.fullPathHash} normTime={st.normalizedTime:0.00} " +
                             $"clips={clips.Length}{(clips.Length > 0 ? " ('" + clips[0].clip.name + "')" : " <-- NOTHING PLAYING")}");
            }

            var rb = animator.GetComponent<RigBuilder>();
            s.AppendLine($"  RigBuilder      : {(rb == null ? "MISSING on the rider" : $"enabled={rb.enabled} layers={rb.layers.Count}")}");
            if (rb != null)
                foreach (var l in rb.layers)
                    s.AppendLine($"      layer rig='{(l.rig != null ? l.rig.name : "NULL")}' active={l.active} " +
                                 $"weight={(l.rig != null ? l.rig.weight.ToString("0.00") : ",")} " +
                                 $"go={(l.rig != null ? l.rig.gameObject.activeInHierarchy.ToString() : ",")}");

            foreach (var rig in root.GetComponentsInChildren<Rig>(true))
                s.AppendLine($"  Rig '{rig.name}' weight={rig.weight:0.00} active={rig.gameObject.activeInHierarchy}");

            var ik = root.GetComponentsInChildren<TwoBoneIKConstraint>(true);
            var dupes = ik.Where(c => c.data.tip != null)
                .GroupBy(c => c.data.tip)
                .Where(g => g.Count(c => c.weight > 0.001f && c.gameObject.activeInHierarchy) > 1)
                .ToArray();

            foreach (var g in dupes)
                s.AppendLine($"  DUPLICATE: {g.Count()} active constraints drive '{g.Key.name}' " +
                             $"({string.Join(", ", g.Select(c => "'" + c.name + "'"))}) , they fight.");

            foreach (var c in ik)
                s.AppendLine($"  IK  '{c.name}' w={c.weight:0.00} root={Nm(c.data.root)} mid={Nm(c.data.mid)} " +
                             $"tip={Nm(c.data.tip)} target={Nm(c.data.target)} hint={Nm(c.data.hint)}");

            foreach (var c in root.GetComponentsInChildren<MultiParentConstraint>(true))
                s.AppendLine($"  Parent '{c.name}' w={c.weight:0.00} obj={Nm(c.data.constrainedObject)} " +
                             $"sources={c.data.sourceObjects.Count}");

            foreach (var c in root.GetComponentsInChildren<MultiRotationConstraint>(true))
                s.AppendLine($"  Rot '{c.name}' w={c.weight:0.00} obj={Nm(c.data.constrainedObject)} " +
                             $"sources={c.data.sourceObjects.Count}");

            var ctrl = root.GetComponentInChildren<BikeAnimationController>(true);
            s.AppendLine($"  BikeAnimationController : {(ctrl == null ? "ABSENT" : $"on '{ctrl.name}' enabled={ctrl.enabled}")}");
            if (ctrl != null && !ctrl.enabled)
                s.AppendLine("      DISABLED: Start() bails when any required reference is null, so nothing blends.");

            var refs = root.GetComponentInChildren<BikerRigReferences>(true);
            s.AppendLine($"  BikerRigReferences      : {(refs == null ? "ABSENT" : $"on '{refs.name}'")}");

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null && refs != null && refs.hipTarget != null)
                s.AppendLine($"  Hips vs hipTarget       : {Vector3.Distance(hips.position, refs.hipTarget.position) * 100f:0.0} cm apart " +
                             "(should be ~0 while the rig is driving)");

            Debug.Log(s.ToString());
        }

        static string Nm(Transform t) => t == null ? "NULL" : t.name;
    }
}
