using MotoSquid.AI;
using MotoSquid.Bike;
using MotoSquid.Cameras;
using MotoSquid.Core;
using MotoSquid.Rider;
using MotoSquid.Traffic;
using MotoSquid.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotoSquid.Race
{
    // Runs before RiderSwitch (-50) and RaceManager (10) so the chosen bike exists and carries the
    // chosen rider by the time either of them reads the scene
    [DefaultExecutionOrder(-100)]
    public class PlayerRacerSetup : MonoBehaviour
    {
        [Header("Where the bike spawns, RaceManager moves it to the grid afterwards")]
        [SerializeField] Transform spawnPoint;

        [Header("Used when this scene is entered directly, with no menu selection")]
        [SerializeField] BikeScriptableObject fallbackBike;
        [SerializeField] string fallbackCharacter = "Voodoo";

        [Header("Rebound to whichever bike spawns")]
        [SerializeField] RaceManager raceManager;
        [SerializeField] SplitScreenSetup splitScreen;
        [SerializeField] HUDManager hud;

        public GameObject ActiveBike   { get; private set; }
        public GameObject ActiveBikeP2 { get; private set; }

        void Awake()
        {
            var session = GameSession.Instance;

            var bike = session != null && session.SelectedBike != null ? session.SelectedBike : fallbackBike;
            if (bike == null)
            {
                Debug.LogError("[PlayerRacerSetup] No bike selected and no fallback assigned.", this);
                return;
            }

            if (bike.playerPrefab == null)
            {
                Debug.LogError($"[PlayerRacerSetup] '{bike.bikeName}' has no Player Prefab assigned on its " +
                               "Bike Scriptable Object.", bike);
                return;
            }

            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            ActiveBike = Instantiate(bike.playerPrefab, position, rotation);
            ActiveBike.name = bike.bikeName;

            // Same reason as the AI grid: the active scene during an additive load is the loading
            // scene, which unloads a few seconds later
            if (ActiveBike.scene != gameObject.scene)
                SceneManager.MoveGameObjectToScene(ActiveBike, gameObject.scene);

            string character = session != null && !string.IsNullOrEmpty(session.SelectedCharacterName)
                ? session.SelectedCharacterName
                : fallbackCharacter;

            ApplyRider(character);
            Rebind();

            Debug.Log($"[PlayerRacerSetup] {bike.bikeName} spawned for {character} " +
                      $"into scene '{gameObject.scene.name}'.", ActiveBike);

            if (session != null && session.IsSplitScreen) SpawnPlayer2(session, bike);
        }
    
        void SpawnPlayer2(GameSession session, BikeScriptableObject p1Bike)
        {
            var bike = session.SelectedBikeP2;
            string drawnCharacter = null;

            // No 2P select screen yet, so rather than mirroring P1, P2 draws from the same roster the
            // AI use and is biased toward differing in BOTH bike and character.
            if (bike == null)
            {
                var randomiser = FindFirstObjectByType<AIRacerRandomiser>();
                if (randomiser != null &&
                    randomiser.TryDrawDistinctFrom(p1Bike, session.SelectedCharacterName, out var pick))
                {
                    bike            = pick.bike;
                    drawnCharacter  = pick.character;
                    Debug.Log($"[PlayerRacerSetup] P2 drew {pick.character} on {pick.bike.bikeName}.", this);
                }
                else bike = p1Bike;
            }

            if (bike == null || bike.playerPrefab == null)
            {
                Debug.LogError("[PlayerRacerSetup] Split screen is on but P2 has no usable bike.", this);
                return;
            }

            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            ActiveBikeP2 = Instantiate(bike.playerPrefab, position, rotation);
            ActiveBikeP2.name = bike.bikeName + " (P2)";

            if (ActiveBikeP2.scene != gameObject.scene)
                SceneManager.MoveGameObjectToScene(ActiveBikeP2, gameObject.scene);

            string character = drawnCharacter
                            ?? (!string.IsNullOrEmpty(session.SelectedCharacterNameP2)
                                ? session.SelectedCharacterNameP2
                                : fallbackCharacter);

            var switcher = ActiveBikeP2.GetComponentInChildren<RiderSwitch>(true);
            if (switcher != null && !switcher.SetRider(character))
                Debug.LogError($"[PlayerRacerSetup] P2 bike carries no rider for '{character}'.", ActiveBikeP2);

            var controller = ActiveBikeP2.GetComponentInChildren<BikeController>(true);
            if (controller == null)
            {
                Debug.LogError($"[PlayerRacerSetup] '{ActiveBikeP2.name}' has no BikeController.", ActiveBikeP2);
                return;
            }

            if (splitScreen != null) splitScreen.BindPlayer2(controller);
            else Debug.LogWarning("[PlayerRacerSetup] No SplitScreenSetup assigned, P2 keeps the prefab's " +
                                  "device filter and camera channel.", this);

            if (raceManager != null)
            {
                raceManager.playerTransform2 = ActiveBikeP2.transform;
                raceManager.playerBike2      = controller;
                raceManager.ApplyPreRaceLock(controller);
            }

            if (hud != null)
                hud.BindPlayer2(controller, ActiveBikeP2.GetComponentInChildren<BoostSystem>(true));

            var rig = SplitScreenSetup.CameraRigOf(controller);
            if (rig != null) rig.name = "CameraController_" + ActiveBikeP2.name;

            var intro = FindFirstObjectByType<StartGameManager>();
            if (intro != null) intro.BindPlayer2(ActiveBikeP2);
            else Debug.LogWarning("[PlayerRacerSetup] No StartGameManager in the scene, P2 will reach GO " +
                                  "with boost disabled and only the throttle unlocked.", this);

            Debug.Log($"[PlayerRacerSetup] P2 {bike.bikeName} spawned for {character}.", ActiveBikeP2);
        }

        void ApplyRider(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return;

            var switcher = ActiveBike.GetComponentInChildren<RiderSwitch>(true);
            if (switcher == null)
            {
                Debug.LogError($"[PlayerRacerSetup] '{ActiveBike.name}' has no RiderSwitch.", ActiveBike);
                return;
            }

            if (!switcher.SetRider(characterName))
                Debug.LogError($"[PlayerRacerSetup] '{ActiveBike.name}' carries no rider for '{characterName}'.", ActiveBike);
        }

        void Rebind()
        {
            var controller = ActiveBike.GetComponentInChildren<BikeController>(true);
            if (controller == null)
            {
                Debug.LogError($"[PlayerRacerSetup] '{ActiveBike.name}' has no BikeController.", ActiveBike);
                return;
            }

            // Without this the spawned bike keeps the prefab's playerDeviceIndex of -1, which accepts
            // every device, and its vcams stay off the player camera's Cinemachine channel
            if (splitScreen != null) splitScreen.BindPlayer1(controller);
            else Debug.LogWarning("[PlayerRacerSetup] No SplitScreenSetup assigned: the spawned bike keeps " +
                                  "the prefab's device filter and camera channel.", this);

            if (raceManager != null)
            {
                raceManager.playerTransform = ActiveBike.transform;
                raceManager.playerBike = controller;
                raceManager.ApplyPreRaceLock(controller);
            }
            else Debug.LogError("[PlayerRacerSetup] No RaceManager assigned: this bike will not be " +
                                "tracked and will not be held on the grid.", this);

            if (hud != null)
                hud.BindPlayer1(controller, ActiveBike.GetComponentInChildren<BoostSystem>(true));

            // Traffic spawns around anchors, and its serialized playerTransform points at whatever
            // bike used to sit in the scene
            // The rig named itself from the bike's name before this script renamed the bike, so it is
            // still carrying the "(Clone)" Instantiate gave it
            var rig = SplitScreenSetup.CameraRigOf(controller);
            if (rig != null)
            {
                rig.name = "CameraController_" + ActiveBike.name;

                // Awake unparented it so it would not inherit lean, but the bike ROOT does not lean
                // (Rotator -> Wheelie Transform -> Lean Transform do) and the vcams are driven by their
                // Follow/LookAt targets, so it can live under the bike and travel with it
                rig.transform.SetParent(ActiveBike.transform, worldPositionStays: true);
            }

            var intro = FindFirstObjectByType<StartGameManager>();
            if (intro != null) intro.BindPlayer(ActiveBike);
            else Debug.LogWarning("[PlayerRacerSetup] No StartGameManager in the scene, the intro camera " +
                                  "will not hand over to the player.", this);

            var traffic = FindFirstObjectByType<TrafficSpawner>();
            if (traffic != null) traffic.playerTransform = ActiveBike.transform;
            else Debug.LogWarning("[PlayerRacerSetup] No TrafficSpawner in the scene, traffic will not " +
                                  "anchor to the player.", this);
        }
    }
}
