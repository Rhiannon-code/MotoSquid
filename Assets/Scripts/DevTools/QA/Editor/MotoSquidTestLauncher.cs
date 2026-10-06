using MotoSquid.Tests;
using UnityEditor;
using UnityEngine;
namespace MotoSquid.DevTools
{
    public static class MotoSquidTestLauncher
    {
        // Gameplay tests

        [MenuItem("MotoSquid/Run Gameplay Tests")]
        public static void RunGameplayTests()
        {
            PlayerPrefs.SetInt(MotoSquidPlayModeTests.PREF_KEY, 1);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
            Debug.Log("[MotoSquid QA] Running gameplay tests...");
        }

        [MenuItem("MotoSquid/Run Gameplay Tests", true)]
        public static bool ValidateGameplayTests() => !EditorApplication.isPlaying;

        // Known bug tests

        [MenuItem("MotoSquid/Run Known Bug Tests")]
        public static void RunKnownBugTests()
        {
            PlayerPrefs.SetInt(MotoSquidKnownBugTests.PREF_KEY, 1);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
            Debug.Log("[MotoSquid QA] Running known bug tests...");
        }

        [MenuItem("MotoSquid/Run Known Bug Tests", true)]
        public static bool ValidateKnownBugTests() => !EditorApplication.isPlaying;

        // Feature QA tests (new QoL / play-feel features)

        [MenuItem("MotoSquid/Run Feature QA Tests")]
        public static void RunFeatureTests()
        {
            PlayerPrefs.SetInt(MotoSquidFeatureTests.PREF_KEY, 1);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
            Debug.Log("[MotoSquid QA] Running feature QA tests...");
        }

        [MenuItem("MotoSquid/Run Feature QA Tests", true)]
        public static bool ValidateFeatureTests() => !EditorApplication.isPlaying;

        // Soak test (3 races × 60 s each)

        [MenuItem("MotoSquid/Run Soak Test (3 races)")]
        public static void RunSoakTest3()  => LaunchSoak(3, 60f);

        [MenuItem("MotoSquid/Run Soak Test (3 races)", true)]
        public static bool ValidateSoak3() => !EditorApplication.isPlaying;

        [MenuItem("MotoSquid/Run Soak Test (10 races)")]
        public static void RunSoakTest10()  => LaunchSoak(10, 60f);

        [MenuItem("MotoSquid/Run Soak Test (10 races)", true)]
        public static bool ValidateSoak10() => !EditorApplication.isPlaying;

        [MenuItem("MotoSquid/Run Soak Test (30 races)")]
        public static void RunSoakTest30()  => LaunchSoak(30, 90f);

        [MenuItem("MotoSquid/Run Soak Test (30 races)", true)]
        public static bool ValidateSoak30() => !EditorApplication.isPlaying;

        private static void LaunchSoak(int races, float durationPerRace)
        {
            PlayerPrefs.SetInt(MotoSquidSoakTest.PREF_KEY,           1);
            PlayerPrefs.SetInt(MotoSquidSoakTest.PREF_RACE_COUNT,    races);
            PlayerPrefs.SetFloat(MotoSquidSoakTest.PREF_RACE_DURATION, durationPerRace);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
            Debug.Log($"[MotoSquid QA] Running soak test: {races} race(s) × {durationPerRace}s each...");
        }
    }
}
