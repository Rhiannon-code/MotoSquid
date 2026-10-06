using MotoSquid.Audio;
using MotoSquid.Traffic;
using MotoSquid.UI;
using System.Collections;
using Michsky.UI.Heat;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace MotoSquid.Core
{
    public class SceneLoader : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private ProgressBar progressBar;
        [SerializeField] private TMP_Text percentageText;
        [SerializeField] private TMP_Text loadingTipText;
        [SerializeField] private CanvasGroup fadePanel;
        [SerializeField] private ControlsCardShow controlsCards;
        [SerializeField] private LoadingPrewarmer prewarmer2;

        [Header("Settings")]
        [SerializeField] private float minimumDisplayTime = 2f;
        [SerializeField] private float sceneWarmupTime = 3f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float prewarmTimeout = 30f;
        // Held after the bar reaches 100%, not folded into minimumDisplayTime: a slow load must not eat
        // the time the controls card is readable
        [SerializeField] private float controlsHoldTime = 0f;
        [SerializeField] private string fallbackScene = "Main 2";
        [SerializeField] private bool   showPercentageText = false;

        [Header("Loading Tips (optional)")]
        [SerializeField] private string[] tips;

        float _shownProgress;

        void OnDestroy()
        {
            StopAllCoroutines();
        }

        void Start()
        {
            if (!showPercentageText && percentageText != null)
                percentageText.gameObject.SetActive(false);

            ShowRandomTip();

            if (fadePanel != null) fadePanel.alpha = 0f;

            string targetScene = fallbackScene;
            if (GameSession.Instance != null)
                targetScene = GameSession.Instance.SelectedTrackScene;

            StartCoroutine(LoadAsync(targetScene));
        }

        IEnumerator LoadAsync(string sceneName)
        {
            float startTime = Time.unscaledTime;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' not found. Check Build Profiles and scene name spelling.");
                yield break;
            }
            operation.allowSceneActivation = false;

            // Spread over the load instead of blocking the first frame, so the bar moves while pools fill
            var prewarmer = TrafficPoolPrewarmer.Instance;
            // On the prewarmer, not on this: this object's scene unloads and takes its coroutines with it
            if (prewarmer != null) prewarmer.StartCoroutine(prewarmer.PrewarmAsync());

            // Shaders and audio, the costs otherwise paid the first time something is seen or heard
            if (prewarmer2 != null) StartCoroutine(prewarmer2.PrewarmAsync());

            // The cards are the reason this screen is up at all now, so a fast load must not cut them off
            float sceneAt = -1f, poolAt = -1f, warmAt = -1f, readyAt = -1f;

            float minimumHold = controlsCards != null
                ? Mathf.Max(minimumDisplayTime, controlsCards.TotalDuration)
                : minimumDisplayTime;

            bool menuMusicFading = false;
            while (true)
            {
                bool ready = operation.progress >= 0.9f
                    && (Time.unscaledTime - startTime) >= minimumHold
                    && (prewarmer2 == null || prewarmer2.Finished || (Time.unscaledTime - startTime) >= prewarmTimeout)
                    && (prewarmer  == null || prewarmer.Finished  || (Time.unscaledTime - startTime) >= prewarmTimeout);

                // The cards finish the round they are on, so each has been up for the same time
                if (ready)
                {
                    if (controlsCards == null || controlsCards.Finished) break;
                    controlsCards.StopAtRoundEnd();
                }

                // Everything the screen waits on, not just the scene: loading finished early and the bar sat
                // full while both prewarmers and the card round were still running
                float scene = Mathf.Clamp01(operation.progress / 0.9f);
                float pool  = prewarmer  != null ? prewarmer.Progress  : 1f;
                float warm  = prewarmer2 != null ? prewarmer2.Progress : 1f;
                float timed = minimumHold > 0f ? Mathf.Clamp01((Time.unscaledTime - startTime) / minimumHold) : 1f;
                float work  = scene * 0.4f + pool * 0.3f + warm * 0.3f;
                if (sceneAt  < 0f && scene >= 1f) sceneAt = Time.unscaledTime - startTime;
                if (poolAt   < 0f && pool  >= 1f) poolAt  = Time.unscaledTime - startTime;
                if (warmAt   < 0f && warm  >= 1f) warmAt  = Time.unscaledTime - startTime;
                if (readyAt  < 0f && ready)       readyAt = Time.unscaledTime - startTime;

                // The last fifth is the card round finishing once everything else is ready, the one wait
                // the bar could not see, which is why it used to sit at 99
                float finish = ready && controlsCards != null ? controlsCards.RoundProgress : (ready ? 1f : 0f);
                float bar    = 0.8f * Mathf.Min(work, timed) + 0.2f * finish;

                // Never runs backwards, and only reads full once the screen is really about to fade
                _shownProgress = Mathf.Max(_shownProgress, Mathf.Min(0.99f, bar));
                UpdateProgressUI(_shownProgress);

                // The bar holds at 99 for the card round, long enough for the menu music to fade out in
                if (!menuMusicFading && _shownProgress >= 0.99f && MenuMusicManager.Instance != null)
                {
                    MenuMusicManager.Instance.FadeOut();
                    menuMusicFading = true;
                }
                yield return null;
            }
            UpdateProgressUI(1f);
            Debug.Log($"[SceneLoader] Scene loaded at {sceneAt:F1}s, traffic pool {poolAt:F1}s, shaders+audio " +
                      $"{warmAt:F1}s, ready {readyAt:F1}s, cards done {Time.unscaledTime - startTime:F1}s " +
                      "(-1 = never reached, timed out).", this);

            if (controlsHoldTime > 0f)
                yield return new WaitForSecondsRealtime(controlsHoldTime);

            // Cover the loading UI before the target scene appears, and stay covered until this scene
            // unloads: the target starts black behind us, so the handover is one continuous black
            // Normally already fading from 99%; this returns that fade, or starts one if the bar never got there
            var menuMusicFade = MenuMusicManager.Instance != null ? MenuMusicManager.Instance.FadeOut() : null;
            yield return StartCoroutine(FadeToBlack());
            // The target scene starts its own music on activation, so hold on black until the menu music is silent
            if (menuMusicFade != null) yield return menuMusicFade;

            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            Scene target = SceneManager.GetSceneByName(sceneName);
            if (target.IsValid()) SceneManager.SetActiveScene(target);

            // Only possible now the held scene load has completed; runs under the black warm-up
            Resources.UnloadUnusedAssets();

            yield return new WaitForSecondsRealtime(sceneWarmupTime);

            SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        void UpdateProgressUI(float progress)
        {
            if (progressBar    != null) progressBar.SetValue(progress * 100f);
            if (percentageText != null) percentageText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }

        void ShowRandomTip()
        {
            if (loadingTipText == null || tips == null || tips.Length == 0) return;

            loadingTipText.overflowMode = TMPro.TextOverflowModes.Overflow;
            loadingTipText.alignment    = TMPro.TextAlignmentOptions.Center;

            RaiseTo(loadingTipText, fadePanel);

            loadingTipText.text = tips[Random.Range(0, tips.Length)];
        }

        static void RaiseTo(Component element, Component top)
        {
            if (element == null || top == null) return;
            if (element.transform.parent != top.transform.parent) return;

            element.transform.SetSiblingIndex(top.transform.GetSiblingIndex());
        }

        IEnumerator FadeToBlack()
        {
            if (fadePanel == null) yield break;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadePanel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            fadePanel.alpha = 1f;
        }
    }
}
