using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace MotoSquid.Bike
{
    public static class WireAllBikeAudio
    {
        const string EngineModel = "1000cc_Sport";
        const string ResChildName = "RES2_Engine";
        const string MixerPath   = "Assets/Audio/MainMixer.mixer";
        const string EngineGroup = "Engine";
        const string WorldGroup  = "World SFX";

        [MenuItem("MotoSquid/Wire All Bike Audio")]
        static void WireAll()
        {
            var mixer     = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var engineGrp = FindGroup(mixer, EngineGroup);
            var worldGrp  = FindGroup(mixer, WorldGroup);

            if (mixer == null)
                Debug.LogWarning($"[BikeAudio] MainMixer not found at {MixerPath}, sources left unrouted (they'll play through the listener).");
            else
            {
                if (engineGrp == null) Debug.LogWarning($"[BikeAudio] No '{EngineGroup}' group in MainMixer, engines unrouted. Do the mixer step, then re-run.");
                if (worldGrp  == null) Debug.LogWarning($"[BikeAudio] No '{WorldGroup}' group in MainMixer, wind/skid unrouted. Do the mixer step, then re-run.");
            }

            int wired = 0, failed = 0;
            var report = new StringBuilder();

            foreach (var (rel, isPlayer) in BikeRoster.All)
            {
                string path = BikeRoster.PathOf(rel);
                string bikeName = BikeRoster.NameOf(rel);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) { Debug.LogError($"[BikeAudio] Could not load {path}"); failed++; report.AppendLine($"✗ {bikeName}: prefab not found"); continue; }

                try
                {
                    WireBike(root, isPlayer, engineGrp, worldGrp);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    wired++;
                    report.AppendLine($"• {bikeName}: {(isPlayer ? "player" : "AI")}, engine={EngineModel}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BikeAudio] {bikeName}: {e}");
                    failed++;
                    report.AppendLine($"✗ {bikeName}: {e.Message}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Wire All Bike Audio",
                $"Wired {wired} bike(s), {failed} failed.\n\n{report}", "OK");
        }

        static void WireBike(GameObject root, bool isPlayer, AudioMixerGroup engineGrp, AudioMixerGroup worldGrp)
        {
            var chassis = root.GetComponentInChildren<BikeAudioController>(true);
            if (chassis == null)
            {
                Transform host = root.transform.Find("Audios");
                if (host == null)
                {
                    var go = new GameObject("Audios");
                    go.transform.SetParent(root.transform, false);
                    host = go.transform;
                }
                chassis = host.gameObject.AddComponent<BikeAudioController>();
            }

            chassis.isLocalPlayer = isPlayer;
            if (engineGrp != null) chassis.engineOutput = engineGrp;
 
            WireEngine(root, chassis, engineGrp, isPlayer);
            EnsureChildSource(root, chassis.transform, "Gear Shift");
            EnsureChildSource(root, chassis.transform, "Skid Sound");
            EnsureChildSource(root, chassis.transform, "Wind");
            EnsureChildSource(root, chassis.transform, "Helmet");

            chassis.WireChassisAudio();

            RouteChildSource(root, "Gear Shift", engineGrp);
            RouteChildSource(root, "Skid Sound", worldGrp);
            RouteChildSource(root, "Wind",       worldGrp);

            EditorUtility.SetDirty(chassis);
        }

        static void WireEngine(GameObject root, BikeAudioController chassis, AudioMixerGroup engineGrp, bool isPlayer)
        {
            Transform host = chassis.transform;

            var resDriver = chassis.GetComponent<EngineAudioRES>();
            if (resDriver != null) Object.DestroyImmediate(resDriver);
            foreach (var t in host.GetComponentsInChildren<Transform>(true))
                if (t != host && t.name == ResChildName) { Object.DestroyImmediate(t.gameObject); break; }

            var engine = chassis.GetComponent<EngineAudio>();
            if (engine == null) engine = chassis.gameObject.AddComponent<EngineAudio>();
            engine.isLocalPlayer = isPlayer;
            if (engineGrp != null) engine.output = engineGrp;
            if (engine.onClip == null || engine.offClip == null)
                engine.WireEngineFromModel();

            EditorUtility.SetDirty(engine);
        }

        static void EnsureChildSource(GameObject root, Transform host, string childName)
        {
            Transform found = null;
            foreach (var t in host.GetComponentsInChildren<Transform>(true))
                if (t != host && t.name == childName) { found = t; break; }

            if (found == null)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == childName) { found = t; break; }

            if (found == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(host, false);
                found = go.transform;
            }

            if (found.GetComponent<AudioSource>() == null)
                found.gameObject.AddComponent<AudioSource>();
        }

        static void RouteChildSource(GameObject root, string childName, AudioMixerGroup grp)
        {
            if (grp == null) return;
            foreach (var s in root.GetComponentsInChildren<AudioSource>(true))
                if (s.gameObject.name == childName) s.outputAudioMixerGroup = grp;
        }

        static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null) return null;
            var groups = mixer.FindMatchingGroups(groupName);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }
    }
}
