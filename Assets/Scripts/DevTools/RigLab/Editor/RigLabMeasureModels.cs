using MotoSquid.Rider;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MotoSquid.DevTools
{
    public static class RigLabMeasureModels
    {
        [MenuItem("Tools/Rig Lab/6b. Measure All Character Models", false, 41)]
        static void Measure()
        {
            const string MannequinPath = "Assets/Models/Dummy_Mannequin/Dummy_Mannequin.fbx";
            var reference = Read(AssetDatabase.LoadAssetAtPath<GameObject>(MannequinPath));

            var models = RigLabRiders.Characters();
            if (models.Count == 0)
            {
                Debug.LogWarning("Rig Lab: no Humanoid character models found. Use '2. Swap Riders' to see what was found and why.");
                return;
            }

            var rows = new List<Row>();
            foreach (var m in models)
            {
                var row = Read(m);
                if (row != null) rows.Add(row);
            }

            var sb = new StringBuilder("Rig Lab character measurements   (reference = Dummy_Mannequin bind pose)\n");
            sb.Append("Measured from the model asset, so no bike or rider swap is needed.\n\n");
            sb.Append(string.Format("{0,-26}{1,9}{2,11}{3,10}{4,8}{5,8}{6,8}\n",
                                    "model", "hips", "shoulder", "halfspan", "foot", "arm", "leg"));
            if (reference != null)
                sb.Append(string.Format("{0,-26}{1,9:0.000}{2,11:0.000}{3,10:0.000}{4,8:0.000}{5,8:0.000}{6,8:0.000}\n",
                                        "Dummy_Mannequin (ref)", reference.hipY, reference.handY,
                                        reference.handX, reference.footY, reference.arm, reference.leg));
            else
                sb.Append(string.Format("{0,-26}{1,9:0.000}{2,11:0.000}{3,10:0.000}{4,8:0.000}{5,8}{6,8}\n",
                                        "(reference)", RigLabMeasure.RefHipY, RigLabMeasure.RefHandY,
                                        RigLabMeasure.RefHandX, RigLabMeasure.RefFootY, "-", "-"));
            foreach (var r in rows)
                sb.Append(string.Format("{0,-26}{1,9:0.000}{2,11:0.000}{3,10:0.000}{4,8:0.000}{5,8:0.000}{6,8:0.000}\n",
                                        r.name, r.hipY, r.handY, r.handX, r.footY, r.arm, r.leg));

            float refArm = reference != null ? reference.arm : 0f;
            float refSho = reference != null ? reference.handY : RigLabMeasure.RefHandY;

            sb.Append("\nskinning: how many vertices each driven bone actually owns.\n");
            sb.Append("a bone with none moves without taking any mesh with it.\n\n");
            foreach (var m in models) Skinning(sb, m);

            sb.Append("\nreach margin vs the mannequin the bar targets were placed for.\n");
            sb.Append("negative = has to stretch further than the rig was built for.\n\n");
            foreach (var r in rows)
            {
                float dSho = r.handY - refSho;
                float dArm = refArm > 0f ? r.arm - refArm : 0f;
                float margin = dArm - dSho;

                sb.Append(string.Format("   {0,-26} arm {1,8:+0.000;-0.000; 0.000}   shoulders {2,8:+0.000;-0.000; 0.000}   margin {3,8:+0.000;-0.000; 0.000}\n",
                                        r.name, dArm, dSho, margin));

                float shortfall = Mathf.Max(0f, -margin);
                sb.Append(string.Format("   {0,-26} try: hip forward {1,5:0.00}   hip rise {2,6:0.00}   spine tuck {3,5:0.0} deg\n",
                                        "", 0.03f + shortfall * 0.5f, -Mathf.Max(0f, dSho),
                                        6f + Mathf.Clamp(shortfall * 80f, 0f, 14f)));
            }

            sb.Append("\nShoulders sitting well above the reference is what pulls the arms toward full\n" +
                      "extension on the bars, where an IK hint has almost no leverage, fix the fit\n" +
                      "before judging elbow splay.\n");
            Debug.Log(sb.ToString());
        }

        static void Skinning(StringBuilder sb, GameObject model)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var anim = go.GetComponent<Animator>();
                if (anim == null || !anim.isHuman) return;

                var owned = new Dictionary<Transform, int>();
                int verts = 0;

                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null) continue;
                    var bones = smr.bones;
                    var bw = smr.sharedMesh.boneWeights;
                    verts += bw.Length;

                    foreach (var w in bw)
                    {
                        if (w.boneIndex0 < 0 || w.boneIndex0 >= bones.Length) continue;
                        var b = bones[w.boneIndex0];
                        if (b == null) continue;
                        int n;
                        owned.TryGetValue(b, out n);
                        owned[b] = n + 1;
                    }
                }

                sb.Append("   ").Append(model.name).Append("   ").Append(verts).Append(" vertices\n");

                var watch = new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                                    HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                                    HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
                foreach (var hb in watch)
                {
                    var t = anim.GetBoneTransform(hb);
                    if (t == null) { sb.Append("        ").Append(hb).Append("  NOT MAPPED\n"); continue; }
                    int n;
                    owned.TryGetValue(t, out n);
                    sb.Append(string.Format("        {0,-14} {1,-26} owns {2,6} vertices{3}\n",
                                            hb, t.name, n,
                                            n == 0 ? "   <-- NOTHING WEIGHTED TO THIS BONE" : ""));
                }

                sb.Append("        lower leg mesh actually sits on:\n");
                foreach (var kv in owned.OrderByDescending(k => k.Value))
                {
                    string n2 = kv.Key.name;
                    if (n2.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        n2.IndexOf("Toe", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        n2.IndexOf("Calf", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        n2.IndexOf("Ankle", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    sb.Append(string.Format("            {0,-32} {1,6} vertices\n", n2, kv.Value));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        class Row
        {
            public string name;
            public float hipY, handY, handX, footY, arm, leg;
        }

        static Row Read(GameObject model)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var anim = go.GetComponent<Animator>();
                if (anim == null || !anim.isHuman)
                {
                    Debug.LogWarning("Rig Lab: " + model.name + " has no Humanoid avatar, skipped.", model);
                    return null;
                }

                var root = go.transform;
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                var lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                var lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (hips == null || lHand == null || lFoot == null) return null;

                return new Row
                {
                    name = model.name,
                    hipY = root.InverseTransformPoint(hips.position).y,
                    handY = root.InverseTransformPoint(lHand.position).y,
                    handX = Mathf.Abs(root.InverseTransformPoint(lHand.position).x),
                    footY = root.InverseTransformPoint(lFoot.position).y,
                    arm = RigLabMeasure.Chain(root, anim.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                                                       anim.GetBoneTransform(HumanBodyBones.LeftLowerArm), lHand),
                    leg = RigLabMeasure.Chain(root, anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                                                       anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), lFoot),
                };
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
