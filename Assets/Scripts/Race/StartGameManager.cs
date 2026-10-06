using MotoSquid.Audio;
using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Controls;
using MotoSquid.Core;
using MotoSquid.UI;
using UnityEngine;
using TMPro;
using Unity.Cinemachine;
using System.Collections;

namespace MotoSquid.Race
{
    public class StartGameManager : MonoBehaviour
    {
        [Header("Spawning")]
        private GameObject playerPrefab;
        [SerializeField] private Transform spawnPoint;
        private GameObject spawnedPlayer;

        [SerializeField] private GameObject playerObject;

        [Header("Camera Into Transition")]
        [SerializeField] private CinemachineCamera introCam;

        // Off: the race fades in already on the player's third person camera. The intro fly-in started
        // above the baked Occlusion Area, and a camera outside every visibility cell mis-culls distant
        // geometry as it crosses the boundary, which is what made the tree line flicker.
        [SerializeField] private bool useIntroCamera = false;
        [SerializeField] private CinemachineCamera playerObjectCam;
        private CinemachineCamera playerCam;
        [SerializeField] private float fadeInStartDelay = 1;
        [SerializeField] private float delayCameraSwitchingStart = 1;
        [SerializeField] private float delayStartTimer = 4;
        [SerializeField] private float wakeGridBeforeBlendEnds = 1;

        [Header("UI")]
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private Color baseColor = Color.yellow;
        [SerializeField] private Color goColor = Color.red;
        [SerializeField] private float popDuration = 0.3f; 

        [Header("Fade Transition Settings")]
        [SerializeField] private CanvasGroup fadePanel;
        [SerializeField] private float fadeDuration = 1.0f;
        // Cap on how long the fade will wait for the grid to settle before going up regardless
        [SerializeField] private float gridSettleTimeout = 2.5f;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip countingBeeps;
        [SerializeField] private AudioClip goBeep;

        [Header("Debug")]
        [SerializeField] private bool skipStartLineSpawn = false;

        [Header("RaceManager Script Ref")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private MusicManager musicManager;

        [Header("Player 1 Camera")]
        [SerializeField] private CameraController p1CameraController;

        [Header("Split Screen: Player 2")]
        [SerializeField] private BikeInput        p2BikeInput;
        [SerializeField] private CameraController p2CameraController;
        [SerializeField] private BoostSystem               p2BoostSystem;

        BikeInput        m_BikeInput;
        CameraController m_CameraController;
        BoostSystem               m_BoostSystem;
        TimerSystem        m_TimerSystem;

        WaitForSeconds m_WaitCameraSwitch;
        WaitForSeconds m_WaitHalfSecond;
        WaitForSeconds m_WaitFadeDelay;

        private void Awake() 
        {
            // Gets the Race Managaer script on the same parent
            //raceManager = GetComponent<RaceManager>();

            if (raceManager == null)
            {
                Debug.LogError("RaceManager is missing on this object");
            }
        }

        private void Start()
        {
            m_WaitCameraSwitch = new WaitForSeconds(delayCameraSwitchingStart);
            m_WaitHalfSecond   = new WaitForSeconds(0.5f);
            m_WaitFadeDelay    = new WaitForSeconds(fadeInStartDelay);
            m_TimerSystem      = FindFirstObjectByType<TimerSystem>();

            Cursor.visible = false;

            Cursor.lockState = CursorLockMode.Locked;

            fadePanel.alpha = 1;
            countdownText.color = baseColor;
            countdownText.gameObject.SetActive(false);

            //Spawn player
            //PlayerSpawningPerfeb();
            if (!skipStartLineSpawn)
                PlayerSpawningMove();

            if (musicManager != null)
            {
                string characterName = GameSession.Instance != null
                    ? GameSession.Instance.SelectedCharacterName
                    : "Mace";
                musicManager.PreparePlaylist(characterName);
            }

            if (playerObject != null)
            {
                m_BikeInput   = playerObject.GetComponentInChildren<BikeInput>();
                m_BoostSystem = playerObject.GetComponentInChildren<BoostSystem>();
                m_CameraController = p1CameraController != null
                    ? p1CameraController
                    : FindFirstObjectByType<CameraController>();
            }

            //Set cameras after finding the player's cam
            if (introCam != null) introCam.Priority = useIntroCamera ? 20 : 0;
            //playerCam.Priority = 10;
            if (playerObjectCam != null) playerObjectCam.Priority = 10;
            //Start the intro
            StartCoroutine(GameIntroSequence());
        }

        // The player bike is instantiated at runtime now, so these two scene references are empty and
        // the intro would never blend off introCam onto a player camera it cannot see
        public void BindPlayer(GameObject bike)
        {
            if (bike == null) return;
            playerObject = bike;

            var controller = bike.GetComponentInChildren<BikeController>(true);
            var rig = controller != null ? SplitScreenSetup.CameraRigOf(controller) : null;

            if (rig != null && rig.cameras != null && rig.cameras.Length > 0)
            {
                playerObjectCam = rig.cameras[0];

                // Also a scene reference, and lookEnabled is switched on through it at GO
                p1CameraController = rig;

                Debug.Log($"[StartGameManager] Intro bound to '{bike.name}', handover camera " +
                          $"'{playerObjectCam.name}', rig '{rig.name}'.", this);
            }
            else
                Debug.LogError($"[StartGameManager] '{bike.name}' has no camera rig, the intro cannot " +
                               "hand over to the player view.", bike);
        }

        // P2 is instantiated at runtime exactly like P1, so the serialized p2 fields are never filled and
        // P2 reached GO with boost still disabled and still in accelerate-only mode.
        public void BindPlayer2(GameObject bike)
        {
            if (bike == null) return;

            p2BikeInput   = bike.GetComponentInChildren<BikeInput>(true);
            p2BoostSystem = bike.GetComponentInChildren<BoostSystem>(true);

            var controller = bike.GetComponentInChildren<BikeController>(true);
            if (controller != null) p2CameraController = SplitScreenSetup.CameraRigOf(controller);

            Debug.Log($"[StartGameManager] P2 bound to '{bike.name}': input " +
                      $"{(p2BikeInput != null ? "ok" : "MISSING")}, boost " +
                      $"{(p2BoostSystem != null ? "ok" : "MISSING")}, rig " +
                      $"{(p2CameraController != null ? p2CameraController.name : "MISSING")}.", this);
        }

        IEnumerator GameIntroSequence()
        {
            //Camera animation and Sweep
            Coroutine fade = StartCoroutine(FadeIn());

            float blendTime = 0f;

            // Without the fly-in the fade is the whole transition, so the grid wakes once the picture is up
            // rather than on frame one, where the engines and the start taunt played over a black screen
            if (!useIntroCamera)
            {
                float fadeStart = Time.time;
                yield return fade;
                blendTime = Time.time - fadeStart;
            }
            else
            {
                yield return m_WaitCameraSwitch;

                if (introCam != null) introCam.Priority = 0;

                // The brain only starts the blend on its next update
                yield return null;
                blendTime = Time.deltaTime;
                CinemachineBrain brain = playerObjectCam != null ? CinemachineCore.FindPotentialTargetBrain(playerObjectCam) : null;
                while (brain != null && blendTime < delayStartTimer
                       && (brain.ActiveVirtualCamera == (ICinemachineCamera)introCam
                           || (brain.IsBlending && brain.ActiveBlend.Duration - brain.ActiveBlend.TimeInBlend > wakeGridBeforeBlendEnds)))
                {
                    yield return null;
                    blendTime += Time.deltaTime;
                }
            }

            // Fly in has settled onto the player view, wake the grid now (engines unmute, players can rev
            // and bank a burnout, AI opponents start revving), even during the pre countdown delay
            if (raceManager != null) raceManager.WarmUpGrid();

            // The blend comes out of the pre countdown delay so the countdown still starts on schedule
            yield return new WaitForSeconds(Mathf.Max(0f, delayStartTimer - blendTime));

            // Tell the Race Manager that it is starting, so it starts it countdown
            if (raceManager != null) raceManager.StartCountdownState();

            //The Countdown
            countdownText.gameObject.SetActive(true);
            string[] words = { "3", "2", "1", "GO!" };

            foreach (string word in words)
            {
                if (word == "3")
                {
                    if (m_CameraController   != null) m_CameraController.lookEnabled   = true;
                    if (p2CameraController   != null) p2CameraController.lookEnabled   = true;
                }

                countdownText.text = word;

                //If "GO!", make scale bigger and change colour
                float targetScale = (word == "GO!") ? 2.0f : 1.2f;
                if (word == "GO!") countdownText.color = goColor;

                //Breathing text animation
                yield return StartCoroutine(PopText(targetScale));

                if (word != "GO!")
                {
                    if (audioSource != null && countingBeeps != null)
                        audioSource.PlayOneShot(countingBeeps);

                    yield return m_WaitHalfSecond;
                }
            }

            if (m_BikeInput != null)        m_BikeInput.SetFullControl();
            if (m_BoostSystem != null)      m_BoostSystem.boostEnabled  = true;
            if (p2BikeInput != null)        p2BikeInput.SetFullControl();
            if (p2BoostSystem != null)      p2BoostSystem.boostEnabled  = true;

            //Start the Race
            // Tell the Game Manager the race has started
            if (raceManager != null) raceManager.StartRacingState();

            if (audioSource != null && goBeep != null)
                audioSource.PlayOneShot(goBeep);
        
            yield return m_WaitHalfSecond;
            countdownText.gameObject.SetActive(false);

            // The countdown timer only runs in Time Trial, in a Race the result is decided by positions
            if (m_TimerSystem != null && raceManager != null && raceManager.ActiveGameMode == GameMode.TimeTrial)
                m_TimerSystem.StartGame();

            //disable player controls during start up
            //if (playerMovementScript != null) playerMovementScript.enabled = true;
        }

        IEnumerator PopText(float maxScale)
        {
            //"Breathing text animation 
            float elapsed = 0;
            Vector3 startScale = Vector3.zero;
            Vector3 endScale = new Vector3(maxScale, maxScale, maxScale); 

            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popDuration;
            
                t = t * t * (3f - 2f * t); 
            
                countdownText.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }
        
            countdownText.transform.localScale = endScale;
        }

        IEnumerator FadeIn()
        {   
            //Fade in logic
            yield return m_WaitFadeDelay;

            // The grid is placed above the track and settles over the next few frames, so fading up on
            // schedule shows every bike dropping in. Held on the wheels rather than on a fixed delay,
            // which stays right if the spawn height or the surface under the grid ever changes.
            var settling = playerObject != null
                ? playerObject.GetComponentInChildren<BikeController>(true)
                : null;
            float waited = 0f;
            while (settling != null && !settling.bikeIsGrounded && waited < gridSettleTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            float elapsed = 0;

            elapsed = 0;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadePanel.alpha = 1 - (elapsed / fadeDuration);
                yield return null;
            }
        }

        void PlayerSpawningPerfeb()
        {
            //Spawn the player
            spawnedPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

            //Find the Camera Controller script anywhere in the scene
            var controller = FindAnyObjectByType<CameraController>();

            if (controller != null && controller.cameras.Length > 0)
            {
                //Grab the first camera from script
                playerCam = controller.cameras[0]; 
                playerCam.Priority = 10;
            }
            else
            {
                Debug.LogError("Couldn't find the CameraController in the scene");
            }

            //Tell the End Game Manager this is the player being used for this race and this is it's camera
            var endManager = FindFirstObjectByType<EndGameManager>();
            if (endManager != null)
            {
                endManager.SetPlayer(spawnedPlayer, playerCam);
            }
        }

        void PlayerSpawningMove()
        {
            if (playerObject != null && spawnPoint != null)
            {
                // Move the physical bike to the start line
                Rigidbody rb = playerObject.GetComponent<Rigidbody>();
                if (rb != null) {

                    bool wasKinematic = rb.isKinematic;
                    rb.isKinematic = true;
                    rb.position = spawnPoint.position;
                    rb.rotation = spawnPoint.rotation;
                    rb.isKinematic = wasKinematic;
                    rb.linearVelocity = Vector3.zero; // Make sure it's not still moving from a previous test
                }

                // Still tell the EndManager who we are
                var endManager = FindFirstObjectByType<EndGameManager>();
                if (endManager != null)
                {
                    endManager.SetPlayer(playerObject, playerObjectCam);
                }
            }
        }
    }
}