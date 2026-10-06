using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Controls;
using MotoSquid.Core;
using MotoSquid.Race;
using UnityEngine;
using TMPro;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Michsky.UI.Heat;

namespace MotoSquid.UI
{
    public class EndGameManager : MonoBehaviour
    {
        [Header("Fade Transition Settings")]
        [SerializeField] private float DampingDuringCinematic = 10f;
        [SerializeField] private float targetDampingDuration = 2.0f;
        [SerializeField] private CanvasGroup fadePanel;
        [SerializeField] private float fadeDuration = 1.0f;
        [SerializeField] private float lookAtDelay = 2.0f;
    
        [Header("UI")]
        [SerializeField] private GameObject endScreenPanel;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text bestTimeText;
        [SerializeField] private TMP_Text titleText;

        [Header("Controller")]
        [SerializeField] private GameObject firstSelected;
        [SerializeField] private string mainMenuScene = "MainMenu";
        private bool menuShown;

        [Header("UI Animation Settings")]
        [SerializeField] private float slideDuration = 1.0f;
        [SerializeField] private float textRevealDelay = 0.5f;
        [SerializeField] private Vector2 offScreenPos = new Vector2(0, 1000);

        [Header("Stats to Reveal")]
        [SerializeField] private GameObject[] objectsToReveal;

        [Header("Stats to Hide")]
        [SerializeField] private GameObject[] objectsToHide;

        [Header("Camera Settings")]
        [SerializeField] private CinemachineCamera victoryCam;
        [SerializeField] private float rotationSpeed = 20f;
        private bool isSpinning = false;
        // Cinemachine 3 has no FreeLook, the orbit that used to be victoryCam.m_XAxis now lives on a
        // CinemachineOrbitalFollow component on the same camera. Resolved lazily so a camera assigned
        // at runtime still works
        private CinemachineOrbitalFollow victoryOrbit;

        [Header("Spawning")]
        [SerializeField] private Transform swapPoint;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip endGameSound;

        [Header("Background Music")]
        //public MusicManager musicManager;
        private GameObject playerInstance;
        private CinemachineCamera PlayerCam;
        private GameObject Player;
        private float savedBest;

        [Header("Race Hookup")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private TimerSystem timerSystem;
        private bool _ended;

        private void Awake()
        {
            endScreenPanel.SetActive(false);

            foreach (GameObject obj in objectsToReveal)
            {

                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }

            string currentLevel = SceneManager.GetActiveScene().name;

            if (SaveManager.Instance != null)
                savedBest = SaveManager.Instance.GetBestTime(currentLevel);

            Debug.Log("Save File Path: " + Application.persistentDataPath);
        }

        void Start()
        {
            if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();
            if (timerSystem == null) timerSystem = FindFirstObjectByType<TimerSystem>();

            // Single source of "race over", RaceManager fires this when the result is decided in any
            // track/mode combo (lap count reached, or finish-line crossed on a PointToPoint track)
            if (raceManager != null) raceManager.OnRaceComplete += HandleRaceComplete;
        }

        void OnDestroy()
        {
            if (raceManager != null) raceManager.OnRaceComplete -= HandleRaceComplete;
        }

        // Derive the player's time and win/lose from the finish order, then run the end sequence
        // Time Trial banks the countdown's elapsed time; Race uses wall clock race time
        void HandleRaceComplete(System.Collections.Generic.List<RaceManager.RacerInfo> finishOrder)
        {
            bool playerWon = finishOrder != null && finishOrder.Count > 0 && finishOrder[0].ai == null;

            float finalTime;
            bool humanFinished = raceManager != null && raceManager.GetBestHumanFinishTime() >= 0f;
            if (raceManager != null && raceManager.ActiveGameMode == GameMode.TimeTrial && timerSystem != null)
            {
                timerSystem.StopAndGetTime(out finalTime);     // banks elapsed and stops the countdown
            }
            else
            {
                // Race, the player's finish time (split-screen takes the faster of the two)
                finalTime = raceManager != null ? raceManager.GetBestHumanFinishTime() : 0f;
                if (finalTime < 0f) finalTime = raceManager != null ? raceManager.RaceElapsedTime : 0f;
            }

            // An AI crossing first also ends the race, and an unfinished run must not become a best time
            StartEndSequence(finalTime, playerWon, recordBest: humanFinished);
        }

        void Update()
        {
            if (isSpinning && victoryCam != null)
            {
                if (victoryOrbit == null) victoryOrbit = victoryCam.GetComponent<CinemachineOrbitalFollow>();
                if (victoryOrbit != null)
                    victoryOrbit.HorizontalAxis.Value += rotationSpeed * Time.deltaTime;
            }

            // ControllerManager only recovers a lost selection for Heat panels, and these are plain buttons
            if (menuShown && EventSystem.current != null && ControllerManager.instance != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy)
                    ControllerManager.instance.SelectUIObject(firstSelected);
            }
        }

        public void StartEndSequence(float finalTime, bool isVictory, bool recordBest = true)
        {
            if (PlayerCam == null)
            {

                Debug.LogError("EndGameManager: No PlayerCam found, SetPlayer() was never called. " +
                    "If the skipStartLineSpawn debug flag is enabled, call SetPlayer() manually or disable it.");
                return;
            }

            // Idempotent, the timer-out (lose) path and the OnRaceComplete (finish) path could both fire,
            // only the first one runs the end sequence
            if (_ended) return;
            _ended = true;

            CinemachineFollow transposer = PlayerCam.GetComponent<CinemachineFollow>();

            // Tally this race (and the win, if any) into the expanded save file.
            if (SaveManager.Instance != null)
                SaveManager.Instance.RecordRaceResult(isVictory);

            //Handle the camera based on win or lose after crossing the finish line or timer hitting 0
            if (isVictory)
            {
                if (transposer != null)
                {
                    StartCoroutine(SmoothCameraDrift(transposer, DampingDuringCinematic, targetDampingDuration));
                }
            }
            else
            {
                // Add effects wanted for if the timer hit 0
                /*if (transposer != null)
                {
                    StartCoroutine(SmoothCameraDrift(transposer, DampingDuringCinematic, targetDampingDuration));
                }*/
            }

            /* Fades out the background music
            if (musicManager != null)
                {
                    // take backgroundMusicFadeout if wanting to fade out music
                    //musicManager.StartFadeOut();
                } */

            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.clip = endGameSound;
                audioSource.Play();
            }

            StartCoroutine(CinematicSequence());

            //Stat and UI Info
            if (titleText != null)
            {
                titleText.text = isVictory ? "COURSE CLEAR" : "TIME UP";
            }

            FormatMath(finalTime);
            BestTimeCheck(finalTime, recordBest);
        
            //Start the 360 spin: change to animation later
            isSpinning = true;
        
            //Add pos animation here later
            //Debug.Log("End Sequence Initiated.");
        }

        IEnumerator SlideHUDDown()
        {
            // Set up
            RectTransform rect = endScreenPanel.GetComponent<RectTransform>();
            Vector2 targetPos = Vector2.zero;
    
            rect.anchoredPosition = offScreenPos;
            endScreenPanel.SetActive(true);

            // Sliding the stat screen down
            float elapsed = 0;

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(offScreenPos, targetPos, elapsed / slideDuration);
                yield return null;
            }

            rect.anchoredPosition = targetPos;
            menuShown = firstSelected != null;

            //Reveal each object loop
            foreach (GameObject obj in objectsToReveal)
            {
                yield return new WaitForSeconds(textRevealDelay);
                if (obj != null)
                {
                    obj.SetActive(true);
                    // Optional sound per thing shown
                }
            }
        
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        IEnumerator CinematicSequence()
        {

            if (fadePanel == null)
            {
                Debug.LogError("EndGameManager: fadePanel is not assigned, cannot run the end cinematic.", this);
                yield break;
            }

            //Wait before fading
            yield return new WaitForSeconds(lookAtDelay);

            //Fade to Black
            float elapsed = 0;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadePanel.alpha = elapsed / fadeDuration;
                yield return null;
            }

            //Hide the HUD objects
            if (objectsToHide != null) 
            {
                for (int i = 0; i < objectsToHide.Length; i++)
                {
                    if (objectsToHide[i] != null)
                        objectsToHide[i].SetActive(false);
                }
            }

            //Switch to Victory Cam and fade in
            var splitScreen = FindFirstObjectByType<SplitScreenSetup>();
            if (splitScreen != null) splitScreen.CollapseToPlayer1();
            var hud = FindFirstObjectByType<HUDManager>();
            if (hud != null) hud.HideRaceHud();
            victoryCam.Priority = 20; 
            yield return new WaitForSeconds(0.5f);

            // swaps the player bike to new spot
            PlayerSpawningMove();

            elapsed = 0;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadePanel.alpha = 1 - (elapsed / fadeDuration);
                yield return null;
            }

            //Slide the Stats Panel
            yield return StartCoroutine(SlideHUDDown());
        }

        IEnumerator SmoothCameraDrift(CinemachineFollow transposer, float targetDamping, float duration)
        {
            //Saves the original damping values
            float startX = transposer.TrackerSettings.PositionDamping.x;
            float startY = transposer.TrackerSettings.PositionDamping.y;
            float startZ = transposer.TrackerSettings.PositionDamping.z;
        
            float elapsed = 0;

            //Slowly drags the camera
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
            
                transposer.TrackerSettings.PositionDamping.x = Mathf.Lerp(startX, targetDamping, t);
                transposer.TrackerSettings.PositionDamping.y = Mathf.Lerp(startY, targetDamping, t);
                transposer.TrackerSettings.PositionDamping.z = Mathf.Lerp(startZ, targetDamping, t);
            
                yield return null;
            }
        }

        void PlayerSpawningMove()
        {
            if (playerInstance == null || swapPoint == null) return;

            // Stop player input first so no commands arrive while we're repositioning
            var bikeInput = playerInstance.GetComponentInChildren<BikeInput>();
            if (bikeInput != null) bikeInput.enabled = false;

            // Disable the bike controller so the player can't drive
            var bikeController = playerInstance.GetComponentInChildren<BikeController>();
            if (bikeController != null) bikeController.enabled = false;


            Rigidbody rb = playerInstance.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic     = true;
                rb.position        = swapPoint.position;
                rb.rotation        = swapPoint.rotation;
            }

            // Keep the transform in sync for everything that reads it (camera, UI, etc.)
            playerInstance.transform.SetPositionAndRotation(swapPoint.position, swapPoint.rotation);

            Transform audioContainer = playerInstance.transform.Find("Audios");
            if (audioContainer != null)
                audioContainer.gameObject.SetActive(false);
            else
                Debug.LogWarning("EndGameManager: Couldn't find a child named 'Audios' to disable");
        }

        void BestTimeCheck(float finalTime, bool recordBest)
        {

            if (bestTimeText == null)
            {
                Debug.LogError("EndGameManager: bestTimeText is not assigned.", this);
                return;
            }

            // Get the name of the current Unity Scene
            string currentLevel = SceneManager.GetActiveScene().name;

            if (SaveManager.Instance == null)
            {
                bestTimeText.text = "";
                return;
            }

            // SaveManager what our current best time is for this track/level
            float savedBest = SaveManager.Instance.GetBestTime(currentLevel);

            // Record when the player completed the course (recordBest) and beat the stored time. This is
            // independent of winning, finishing 2nd vs AI still logs your time. The timer out lose path
            // passes recordBest:false so a DNF never writes a record
            if (recordBest && finalTime > 0f && (finalTime < savedBest || savedBest == 0f))
            {
                // Tell the SaveManager to write the new record to the file
                SaveManager.Instance.SaveLevelTime(currentLevel, finalTime);
            

                bestTimeText.text = "NEW BEST TIME: " + FormatTime(finalTime);

                // Play effect here for new record
            }
            else
            {
                // If we didn't beat the record, just show the old record on the screen after formating the saved best time
                bestTimeText.text = "BEST TIME: " + FormatTime(savedBest);
            }
        }

        void FormatMath(float finalTime)
        {
            //Update the UI
            scoreText.text = "SURVIVED: " + FormatTime(finalTime);
        }

        // Shared mm:ss.ff formatter
        static string FormatTime(float t)
        {
            int minutes   = Mathf.FloorToInt(t / 60F);
            int seconds   = Mathf.FloorToInt(t % 60F);
            int fractions = Mathf.FloorToInt((t % 1F) * 100F);
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fractions);
        }

        public void RestartGame()
        {
            // Reloads the current scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void MenuGame()
        {
            // Loads the main menu scene
            SceneManager.LoadScene(mainMenuScene);
        }

        public void QuitGame()
        {
            // Quits the application
            Application.Quit();
        }

        public void SetPlayer(GameObject player, CinemachineCamera playersCam)
        {
            //Replaces the "PlayerCam" and "Player" with what was spawned in by the starting manager
            playerInstance = player;
            PlayerCam = playersCam;
        }
    }
}