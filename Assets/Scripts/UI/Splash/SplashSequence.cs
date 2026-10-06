using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MotoSquid.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class SplashSequence : MonoBehaviour
    {
        [Header("Scene loaded once the logo has had its time")]
        [SerializeField] private string nextScene = "MainMenu";

        [Header("Seconds the animated logo holds before moving on")]
        [SerializeField] private float holdTime = 3.5f;

        [Header("Seconds spent fading this canvas group out")]
        [SerializeField] private float fadeDuration = 0.6f;

        [Header("Any key or button cuts the hold short")]
        [SerializeField] private bool allowSkip = true;

        [Header("Ends Unity's own splash as soon as this scene is live. Whether this scene " +
                "runs while that splash is still up is platform dependent, so treat it as a bonus")]
        [SerializeField] private bool endUnitySplashEarly = true;

        CanvasGroup group;
        IDisposable skipListener;
        bool skipped;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 1f;
        }

        void Start()
        {
            if (endUnitySplashEarly) SplashScreen.Stop(SplashScreen.StopBehavior.FadeOut);
            if (allowSkip) skipListener = InputSystem.onAnyButtonPress.CallOnce(_ => skipped = true);
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            skipListener?.Dispose();
            StopAllCoroutines();
        }

        IEnumerator Run()
        {
            float elapsed = 0f;
            while (elapsed < holdTime && !skipped)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float faded = 0f;
            while (faded < fadeDuration)
            {
                faded += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(faded / fadeDuration);
                yield return null;
            }
            group.alpha = 0f;

            // LoadScene blocks and holds the last RENDERED frame, so without letting one frame through
            // at alpha 0 the screen freezes on a faintly visible logo for the whole load
            yield return null;

            if (Application.CanStreamedLevelBeLoaded(nextScene))
                SceneManager.LoadScene(nextScene);
            else
                Debug.LogError($"[SplashSequence] '{nextScene}' is not in the build scene list.");
        }
    }
}
