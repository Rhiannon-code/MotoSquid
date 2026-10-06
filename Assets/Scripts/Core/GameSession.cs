using MotoSquid.AI;
using MotoSquid.Bike;
using MotoSquid.Traffic;
using UnityEngine;
namespace MotoSquid.Core
{
    public enum TrackType { Circuit, PointToPoint }
    // SplitScreen is a menu-level choice only: MainMenuSelectManager resolves it to Race + IsSplitScreen
    // so no gameplay code has to learn a third mode. Appended, so the saved indices of the first two hold
    public enum GameMode  { Race, TimeTrial, SplitScreen }

    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        // Race selection
        public int    SelectedBikeIndex     { get; set; } = 0;
        public int    SelectedBikeIndexP2   { get; set; } = 0;
        public BikeScriptableObject SelectedBike   { get; set; }
        public BikeScriptableObject SelectedBikeP2 { get; set; }
        public string SelectedCharacterName   { get; set; } = "Mace";  // Player 1
        public string SelectedCharacterNameP2 { get; set; } = "Mace";  // Player 2 (split screen)
        public string SelectedTrackScene    { get; set; } = "Main 2";

        // Mode
        public bool      IsSplitScreen     { get; set; } = false;
        public TrackType SelectedTrackType { get; set; } = TrackType.PointToPoint;
        public GameMode  SelectedGameMode  { get; set; } = GameMode.Race;

        // Difficulty
        public int   DifficultyPreset { get; set; } = 1;  // 0=Easy  1=Normal  2=Hard
        public float AIDifficulty     { get; set; } = 0.6f;
        public int   TrafficDensity   { get; set; } = 1;  // 0=Light  1=Normal  2=Heavy

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("GameSession").AddComponent<GameSession>();
        }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            DifficultyPreset = PlayerPrefs.GetInt("DifficultyPreset", 1);
            AIDifficulty     = PlayerPrefs.GetFloat("AIDifficulty", 0.6f);
            TrafficDensity   = PlayerPrefs.GetInt("TrafficDensity", 1);
        }

        public void ApplyDifficultyPreset(int index)
        {
            DifficultyPreset = index;
            float[] aiValues      = { 0.3f, 0.6f, 1.0f };
            int[]   trafficValues = { 0,    1,    2    };
            AIDifficulty   = aiValues[Mathf.Clamp(index, 0, 2)];
            TrafficDensity = trafficValues[Mathf.Clamp(index, 0, 2)];
        }
    }
}
