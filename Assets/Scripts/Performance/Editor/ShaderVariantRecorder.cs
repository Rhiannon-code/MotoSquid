using MotoSquid.Bike;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoSquid.Performance
{
    [InitializeOnLoad]
    public static class ShaderVariantRecorder
    {
        const string CollectionPath = "Assets/ShaderVariant.shadervariants";
        const string AutoKey        = "MotoSquid.ShaderAutoRecord";

        static ShaderVariantRecorder()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static bool AutoRecord
        {
            get => EditorPrefs.GetBool(AutoKey, false);
            set => EditorPrefs.SetBool(AutoKey, value);
        }

        static MethodInfo Find(string name, params Type[] args) =>
            typeof(ShaderUtil).GetMethod(
                name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null, args ?? Type.EmptyTypes, null);

        static bool TryCounts(out int shaders, out int variants)
        {
            shaders = variants = 0;

            MethodInfo s = Find("GetCurrentShaderVariantCollectionShaderCount");
            MethodInfo v = Find("GetCurrentShaderVariantCollectionVariantCount");
            if (s == null || v == null)
            {
                Debug.LogError("[ShaderVariantRecorder] UnityEditor.ShaderUtil no longer exposes the " +
                               "shader-variant tracking methods. This editor version has moved them.");
                return false;
            }

            shaders  = (int)s.Invoke(null, null);
            variants = (int)v.Invoke(null, null);
            return true;
        }

        const string SessionPath = "Assets/MotoSquid/Data/Performance/_ShaderVariant_Session.shadervariants";

        // Unity can only write the CURRENT session's variants, and it overwrites whatever is at the path.
        // Comparing counts and skipping the save (what this used to do) meant a lap that exercised 30 new
        // variants but 152 in total never contributed anything to a collection holding 166. The session is
        // written to a scratch asset and merged in instead, so the collection is a genuine union.
        static bool SaveCollection(int shaders, int variants)
        {
            MethodInfo save = Find("SaveCurrentShaderVariantCollection", typeof(string));
            if (save == null)
            {
                Debug.LogError("[ShaderVariantRecorder] SaveCurrentShaderVariantCollection(string) not found.");
                return false;
            }

            var target = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(CollectionPath);
            if (target == null)
            {
                save.Invoke(null, new object[] { CollectionPath });
                AssetDatabase.Refresh();
                Debug.Log($"[ShaderVariantRecorder] Created {CollectionPath} with {shaders} shaders / " +
                          $"{variants} variants.");
                return true;
            }

            save.Invoke(null, new object[] { SessionPath });
            AssetDatabase.Refresh();

            var session = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(SessionPath);
            if (session == null)
            {
                Debug.LogError($"[ShaderVariantRecorder] Could not read the session collection at {SessionPath}.");
                return false;
            }

            int before = target.variantCount;
            int added  = MergeInto(target, session);

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(SessionPath);
            AssetDatabase.Refresh();

            Debug.Log($"[ShaderVariantRecorder] Merged this session ({variants} variants) into " +
                      $"{CollectionPath}: {added} new, {before} -> {target.variantCount} total.");
            return true;
        }

        // ShaderVariantCollection exposes Add and Contains but no way to enumerate what it holds, so the
        // session's entries are read off the serialized asset and re-added through the public API.
        static int MergeInto(ShaderVariantCollection target, ShaderVariantCollection source)
        {
            var so      = new SerializedObject(source);
            var shaders = so.FindProperty("m_Shaders");
            if (shaders == null) return 0;

            int added = 0;
            for (int i = 0; i < shaders.arraySize; i++)
            {
                var entry  = shaders.GetArrayElementAtIndex(i);
                var shader = entry.FindPropertyRelative("first")?.objectReferenceValue as Shader;
                var list   = entry.FindPropertyRelative("second.variants");
                if (shader == null || list == null) continue;

                for (int j = 0; j < list.arraySize; j++)
                {
                    var v        = list.GetArrayElementAtIndex(j);
                    string keys  = v.FindPropertyRelative("keywords")?.stringValue ?? "";
                    var passType = (PassType)(v.FindPropertyRelative("passType")?.intValue ?? 0);

                    string[] keywords = string.IsNullOrWhiteSpace(keys)
                        ? new string[0]
                        : keys.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

                    try
                    {
                        var variant = new ShaderVariantCollection.ShaderVariant(shader, passType, keywords);
                        if (target.Add(variant)) added++;
                    }
                    catch (System.ArgumentException)
                    {
                        // The shader no longer has that variant (a keyword or pass was renamed). Dropping
                        // it is correct: warming a variant that does not exist would throw at runtime.
                    }
                }
            }

            return added;
        }

        static int CurrentCollectionVariants()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(CollectionPath);
            return existing != null ? existing.variantCount : 0;
        }

        // Saving on every play-mode exit would let a five-second session overwrite a full lap's capture,
        // so the collection only ever grows. Tracking is NOT cleared automatically for the same reason:
        // variants accumulate across sessions, which is exactly what a preload list wants.
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!AutoRecord || state != PlayModeStateChange.EnteredEditMode) return;
            if (!TryCounts(out int shaders, out int variants)) return;

            if (variants == 0) return;

            SaveCollection(shaders, variants);
        }

        [MenuItem("MotoSquid/Shaders/Auto-Record On Play Exit", priority = 0)]
        static void ToggleAuto() => AutoRecord = !AutoRecord;

        [MenuItem("MotoSquid/Shaders/Auto-Record On Play Exit", true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked("MotoSquid/Shaders/Auto-Record On Play Exit", AutoRecord);
            return true;
        }

        [MenuItem("MotoSquid/Shaders/Report Tracked Variants", priority = 20)]
        static void Report()
        {
            if (!TryCounts(out int shaders, out int variants)) return;
            Debug.Log($"[ShaderVariantRecorder] Currently tracked: {shaders} shaders, {variants} variants. " +
                      $"Collection on disk holds {CurrentCollectionVariants()}.");
        }

        [MenuItem("MotoSquid/Shaders/Clear Tracked Variants", priority = 21)]
        static void Clear()
        {
            MethodInfo clear = Find("ClearCurrentShaderVariantCollection");
            if (clear == null)
            {
                Debug.LogError("[ShaderVariantRecorder] ClearCurrentShaderVariantCollection not found.");
                return;
            }

            clear.Invoke(null, null);
            Debug.Log("[ShaderVariantRecorder] Cleared tracked variants.");
        }

        [MenuItem("MotoSquid/Shaders/Save Tracked Variants To Collection", priority = 22)]
        static void Save()
        {
            if (!TryCounts(out int shaders, out int variants)) return;

            if (variants == 0)
            {
                Debug.LogWarning("[ShaderVariantRecorder] Nothing tracked, so there is nothing to save.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Save tracked shader variants",
                    $"Overwrite\n{CollectionPath}\nwith {shaders} shaders / {variants} variants?\n" +
                    $"(the collection currently holds {CurrentCollectionVariants()} variants)",
                    "Overwrite", "Cancel"))
                return;

            SaveCollection(shaders, variants);
        }
    }
}
