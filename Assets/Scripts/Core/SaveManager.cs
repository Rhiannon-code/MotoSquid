using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace MotoSquid.Core
{
    // These classes hold the actual data to save
    [System.Serializable]
    public class LevelRecord
    {
        public string levelName;
        public float bestTime;
    }

    [System.Serializable]
    public class SaveData
    {
        // A list to save times for multiple tracks
        public List<LevelRecord> records = new List<LevelRecord>();

        // Player progress/last used selections (expanded save file)
        public string lastSelectedCharacter = "Mace";
        public string lastSelectedTrack     = "";
        public int    winCount              = 0;
        public int    totalRaces            = 0;
    }

    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance;

        private string savePath;
        public SaveData currentData;

        private void Awake()
        {
            // Singleton Setup
            // If one already exists, destroy this new one. If not, keep this one alive forever
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject); // What keeps it alive across scenes


            // Define where the file lives
            savePath = Path.Combine(Application.persistentDataPath, "racing_save.json");
            LoadGame();
        }

        public void SaveLevelTime(string levelName, float time)
        {
            //Check if there is already a record for this level
            LevelRecord record = currentData.records.Find(r => r.levelName == levelName);

            if (record == null)
            {
                // First time finishing this level, make a new entry
                currentData.records.Add(new LevelRecord { levelName = levelName, bestTime = time });
            }
            else if (time < record.bestTime)
            {
                // If found an entry, and the new time is faster, update it
                record.bestTime = time;
            }

            // Save the updated data to the file
            SaveGame();
        }

        // Expanded save helpers (last selections + win/race tallies)

        public void SetLastSelectedCharacter(string characterName)
        {
            if (string.IsNullOrEmpty(characterName) ||
                currentData.lastSelectedCharacter == characterName) return;
            currentData.lastSelectedCharacter = characterName;
            SaveGame();
        }

        public void SetLastSelectedTrack(string trackName)
        {
            if (string.IsNullOrEmpty(trackName) ||
                currentData.lastSelectedTrack == trackName) return;
            currentData.lastSelectedTrack = trackName;
            SaveGame();
        }

        // Call once when a race finishes. won = true if the local player placed first
        public void RecordRaceResult(bool won)
        {
            currentData.totalRaces++;
            if (won) currentData.winCount++;
            SaveGame();
        }

        public float GetBestTime(string levelName)
        {
            // Search our data for this specific level
            LevelRecord record = currentData.records.Find(r => r.levelName == levelName);

            // If we found a record, return that time. If not, return 0
            if (record != null)
            {
                return record.bestTime;
            }
            return 0f;
        }

        // File Writing/Reading logic
        private void SaveGame()
        {
            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                File.WriteAllText(savePath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveManager: failed to write save file, {e.Message}");
            }
        }

        private void LoadGame()
        {
            if (!File.Exists(savePath))
            {
                currentData = new SaveData();
                return;
            }

            try
            {
                string json = File.ReadAllText(savePath);
                currentData = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveManager: failed to read save file, starting fresh, {e.Message}");
                currentData = new SaveData();
            }
        }
    }
}
