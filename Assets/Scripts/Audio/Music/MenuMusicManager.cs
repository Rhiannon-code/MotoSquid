using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace MotoSquid.Audio
{
    // RequireComponent ensures Unity automatically adds an AudioSource to this object with this script
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusicManager : MonoBehaviour
    {
        [Header("Scenes where music should PLAY")]
        public List<string> menuScenes = new List<string>();

        [Tooltip("Seconds the menu music takes to fade out under the loading screen's fade to black")]
        public float fadeOutTime = 1.5f;

        public static MenuMusicManager Instance; // The Singleton
        private AudioSource audioSource;
        private float baseVolume;
        private Coroutine fade;

        private void Awake()
        {

            audioSource = GetComponent<AudioSource>();
            baseVolume = audioSource.volume;

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            // Subscribe to the Scene Loaded event
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            // Clean up the subscription if this object is ever destroyed
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Runs automatically every time any scene finishes loading
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // We check if the name of the scene we just entered is inside our list
            if (menuScenes.Contains(scene.name))
            {
                StopFade();
                audioSource.volume = baseVolume;
                if (!audioSource.isPlaying) audioSource.Play();
            }
            else
            {
                // Anything still playing here would overlap the scene's own music, so it goes now
                StopFade();
                audioSource.Stop();
                audioSource.volume = baseVolume;
            }
        }

        // SceneLoader starts this with its fade to black and waits on it, so the menu music is silent
        // before the race scene activates and starts its own song
        public Coroutine FadeOut()
        {
            if (!audioSource.isPlaying) return null;
            if (fade == null) fade = StartCoroutine(FadeOutRoutine());
            return fade;
        }

        private void StopFade()
        {
            if (fade != null) StopCoroutine(fade);
            fade = null;
        }

        // Unscaled, so a paused or slowed timescale can't hold the menu music on
        private System.Collections.IEnumerator FadeOutRoutine()
        {
            for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
            {
                audioSource.volume = Mathf.Lerp(baseVolume, 0f, t / fadeOutTime);
                yield return null;
            }
            audioSource.Stop();
            audioSource.volume = baseVolume;
            fade = null;
        }
    }
}
