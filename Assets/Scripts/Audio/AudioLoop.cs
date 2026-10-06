using System.Collections.Generic;
using UnityEngine;

namespace MotoSquid.Audio
{
    public static class AudioLoop
    {
        const float MinRestartInterval = 1f;

        static readonly Dictionary<AudioSource, float> s_lastStart = new Dictionary<AudioSource, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear() => s_lastStart.Clear();

        public static void EnsurePlaying(AudioSource source, string what, Component owner)
        {
            if (source == null || source.clip == null || source.isPlaying || AudioListener.pause) return;
            if (!Application.isFocused && !Application.runInBackground) return;

            float now = Time.unscaledTime;
            if (s_lastStart.TryGetValue(source, out float last) && now - last < MinRestartInterval) return;
            s_lastStart[source] = now;

            source.Play();
            AudioBurstLog.Note(what, owner);
        }

        public static void RouteToGroup(AudioSource source, string groupName)
        {
            var current = source != null ? source.outputAudioMixerGroup : null;
            if (current == null || current.name == groupName) return;

            var mixer = current.audioMixer;
            var matches = mixer.FindMatchingGroups($"{current.name}/{groupName}");
            if (matches == null || matches.Length == 0) matches = mixer.FindMatchingGroups(groupName);
            foreach (var group in matches)
                if (group.name == groupName) { source.outputAudioMixerGroup = group; return; }
        }
    }
}
