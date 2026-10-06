using UnityEngine;

namespace MotoSquid.Audio
{
    public enum BarkCategory
    {
        StartTaunt,     // Start of race taunts
        Attacking,      // Spoken line when your hit lands
        BeingAttacked,  // Getting hit by another rider
        Overtaking,     // Passing an opponent
        BeingOvertaken, // Getting passed by an opponent
        Crash,          // Coming off the bike
        LoseRace,       // Finished, but not first
        WinRace,        // Finished first
        SwingGrunt      // Short effort on every swing, hit or miss. Last so the saved values above keep their numbers
    }

    [CreateAssetMenu(fileName = "CharacterVoice", menuName = "MotoSquid/Character Voice")]
    public class CharacterVoice : ScriptableObject
    {
        public string characterName = "Voodoo";

        [Header("Start of race taunts")]
        public AudioClip[] startTaunt;

        [Header("Hit landed  (spoken line when your hit connects)")]
        public AudioClip[] attacking;

        [Header("Swing grunts  (short effort on every swing, hit or miss)")]
        public AudioClip[] swingGrunt;

        [Header("Being attacked  (taking a hit, oofs, retaliation)")]
        public AudioClip[] beingAttacked;

        [Header("Overtaking  (passing an opponent)")]
        public AudioClip[] overtaking;

        [Header("Being overtaken  (getting passed)")]
        public AudioClip[] beingOvertaken;

        [Header("Crash  (coming off the bike, impact foley)")]
        public AudioClip[] crash;

        [Header("Loses race")]
        public AudioClip[] loseRace;

        [Header("Wins race")]
        public AudioClip[] winRace;

        // Silence at the head of each clip, measured from the audio itself the first time it is asked for
        // It used to be measured by the voice builder and stored, so a take dropped over an old file kept
        // the old figure until someone rebuilt, and lost its first word
        static readonly System.Collections.Generic.Dictionary<AudioClip, float> s_leadIn =
            new System.Collections.Generic.Dictionary<AudioClip, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearLeadIns() => s_leadIn.Clear();

        public float LeadIn(AudioClip clip)
        {
            if (clip == null) return 0f;
            if (!s_leadIn.TryGetValue(clip, out float lead))
            {
                lead = MeasureLeadIn(clip);
                s_leadIn[clip] = lead;
            }
            return lead;
        }

        // Every line up front, so the reads happen while the race loads instead of on a bark
        public void MeasureAll()
        {
            foreach (BarkCategory category in System.Enum.GetValues(typeof(BarkCategory)))
                foreach (var clip in GetClips(category))
                    LeadIn(clip);
        }

        // Seconds of silence before the first audible sample. These are hand recorded VO with a
        // mean 0.3 s of dead air at the head, which on a reactive bark reads as engine lag
        public static float MeasureLeadIn(AudioClip clip, System.Collections.Generic.List<string> unreadable = null,
                                          float thresholdDb = -45f)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0) return 0f;

            // GetData logs a red error on anything but Decompress On Load, so ask first rather than
            // letting it fail, the fallback is the same 0 either way, without the console noise
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                unreadable?.Add(clip.name);
                return 0f;
            }

            if (!clip.LoadAudioData()) return 0f;

            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) return 0f;

            float threshold = Mathf.Pow(10f, thresholdDb / 20f);
            int   win       = Mathf.Max(1, clip.frequency / 100) * clip.channels;   // 10 ms

            for (int i = 0; i + win <= data.Length; i += win)
            {
                double sum = 0;
                for (int j = i; j < i + win; j++) sum += (double)data[j] * data[j];
                if (System.Math.Sqrt(sum / win) > threshold)
                    return (float)i / clip.channels / clip.frequency;
            }
            return 0f;   // Never rises above the floor, play it whole rather than skip it
        }

        // Returns the clip array backing a category (never null, may be empty)
        public AudioClip[] GetClips(BarkCategory category)
        {
            switch (category)
            {
                case BarkCategory.StartTaunt:     return startTaunt     ?? System.Array.Empty<AudioClip>();
                case BarkCategory.Attacking:      return attacking      ?? System.Array.Empty<AudioClip>();
                case BarkCategory.BeingAttacked:  return beingAttacked  ?? System.Array.Empty<AudioClip>();
                case BarkCategory.Overtaking:     return overtaking     ?? System.Array.Empty<AudioClip>();
                case BarkCategory.BeingOvertaken: return beingOvertaken ?? System.Array.Empty<AudioClip>();
                case BarkCategory.Crash:          return crash          ?? System.Array.Empty<AudioClip>();
                case BarkCategory.LoseRace:       return loseRace       ?? System.Array.Empty<AudioClip>();
                case BarkCategory.WinRace:        return winRace        ?? System.Array.Empty<AudioClip>();
                case BarkCategory.SwingGrunt:     return swingGrunt     ?? System.Array.Empty<AudioClip>();
                default:                          return System.Array.Empty<AudioClip>();
            }
        }

        public AudioClip GetRandomClip(BarkCategory category, AudioClip avoid = null)
        {
            AudioClip[] clips = GetClips(category);
            if (clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];

            AudioClip pick = clips[Random.Range(0, clips.Length)];
            if (pick == avoid)   // One re roll is enough to dodge a back to back repeat
                pick = clips[(System.Array.IndexOf(clips, pick) + 1) % clips.Length];
            return pick;
        }
    }
}
