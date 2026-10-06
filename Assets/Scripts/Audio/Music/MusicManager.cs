using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;

namespace MotoSquid.Audio
{
    public class MusicManager : MonoBehaviour
    {
        [Header("Testing Controls")]
        [SerializeField] private string playerCharacterName = "Mace";
        private bool startMusic = false;
    
        [Header("Audio References")]
        public AudioSource musicSource;

        [Header("Character Music")]
        [SerializeField] private AudioClip[] songs;

        [Header("HUD Settings")]
        [SerializeField] private TMP_Text songNameText;
        [SerializeField] private Vector2 targetOnScreenPos = new Vector2(60f, 0f);
        [SerializeField] private float holdOnScreenTime = 3f;
        [SerializeField] private float textScrollDuration = 1f;
        [SerializeField] private Image logoImage;
        [SerializeField] private float logoFadeOutTime = 1f;
        private Vector2 startingPos;
        private Vector2 offScreenPos;
          private Coroutine songNameCoroutine;
        private Coroutine activeLerp;

        [Header("HotKeys")]
        [SerializeField] private InputActionAsset inputActions;
        private InputAction _skipAction;
    
        // HYBRID LOADER VARIABLES
        private List<string> customPaths = new List<string>(); // Stores 100+ strings (cheap)
        private AudioClip[] bufferedClips; // Stores max 5 actual audio files (heavy)
        private int maxBufferSize = 5;
        private int fileIndex = 0;   // Where we are in the massive list of paths
        private int bufferIndex = 0; // Which slot (0-4) is currently playing
    
        private bool isPreloading = false;
        private List<AudioClip> activeInternalPlaylist = new List<AudioClip>();
        private int currentInternalIndex = 0;
        private bool usingCustomMusic = false;
        private bool isFading = false;

        // Cached, null filtered copy of songs, built once in Start so the default playlist isn't
        // rebuilt from the Inspector array every time it's needed
        private List<AudioClip> playableSongs;

        void Awake() 
        {
            var map = inputActions.FindActionMap("Menu", throwIfNotFound: true);
            _skipAction = map.FindAction("Skip", throwIfNotFound: true);
        }

        void Start() 
        {
            musicSource.volume = 1f;
            PrewarmMusicCache();
            BuildPlayableSongsList();

            startingPos = targetOnScreenPos;
            offScreenPos = new Vector2(startingPos.x - 500f, startingPos.y);

            songNameText.gameObject.SetActive(false);
            songNameText.rectTransform.anchoredPosition = offScreenPos;

            if (startMusic)
            {
                PreparePlaylist(playerCharacterName);
            }
        }

        void Update()
        {
            // A paused listener, or a window without focus (Alt+Tab with Run In Background off), reports every
            // source as not playing, which isn't the end of the song
            if (startMusic && !isFading && !musicSource.isPlaying && !isPreloading && !AudioListener.pause
                && !AudioSuspendedByFocus())
            {
                PlayNextSong();
            }

            if (_skipAction != null && _skipAction.WasPressedThisFrame())
            {
                SkipTrack();
            }
        }

        const float RefocusGrace = 1f;
        float _refocusedAt = -99f;

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) _refocusedAt = Time.unscaledTime;
        }

        // Audio can take a frame or more to resume after focus returns, so the first frames back still read as stopped
        bool AudioSuspendedByFocus() =>
            !Application.runInBackground &&
            (!Application.isFocused || Time.unscaledTime - _refocusedAt < RefocusGrace);

        // Public skip entry point so a HUD "skip" button (OnClick) and the hotkey share one path.
        // No op while the streaming buffer is still preloading to avoid racing the loader
        public void SkipTrack()
        {
            if (isPreloading) return;
            PlayNextSong();
        }

        // Name of the track currently playing (for a now playing HUD readout), or "" if none
        public string CurrentTrackName =>
            musicSource != null && musicSource.clip != null ? musicSource.clip.name : "";

        public void OpenUserMusicFolder()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "UserMusic");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Application.OpenURL("file://" + path);
        }

        public static bool UserMusicAvailable()
        {
            if (PlayerPrefs.GetInt("UseUserMusic", 0) != 1) return false;
            string path = Path.Combine(Application.streamingAssetsPath, "UserMusic");
            if (!Directory.Exists(path)) return false;
            return Directory.GetFiles(path, "*.mp3").Length > 0
                || Directory.GetFiles(path, "*.wav").Length > 0
                || Directory.GetFiles(path, "*.ogg").Length > 0;
        }

        public void PrewarmMusicCache()
        {
            if (songs == null) return;
            foreach (AudioClip clip in songs)
                if (clip != null) clip.LoadAudioData();
        }

        // Filters out any empty Inspector slots once, so the default playlist is built from
        // this instead of re-filtering the raw array every time
        void BuildPlayableSongsList()
        {
            playableSongs = new List<AudioClip>();
            if (songs != null)
                foreach (AudioClip c in songs)
                    if (c != null) playableSongs.Add(c);
        }

        public void PreparePlaylist(string characterName)
        {
            isFading = false;

            // The player's own imported music takes priority when they've opted in and it's present
            if (UserMusicAvailable())
            {
                string customPath = Path.Combine(Application.streamingAssetsPath, "UserMusic");

                if (Directory.Exists(customPath))
                {
                    List<string> files = new List<string>();
                    files.AddRange(Directory.GetFiles(customPath, "*.mp3"));
                    files.AddRange(Directory.GetFiles(customPath, "*.wav"));
                    files.AddRange(Directory.GetFiles(customPath, "*.ogg"));

                    if (files.Count > 0)
                    {
                        customPaths = new List<string>(files);
                        ShuffleList(customPaths);
                        usingCustomMusic = true;

                        // Cap the buffer size if they have less than 5 songs
                        int actualBufferSize = Mathf.Min(maxBufferSize, customPaths.Count);
                        bufferedClips = new AudioClip[actualBufferSize];

                        StartCoroutine(PreloadInitialBuffer());
                        return;
                    }
                }
            }

            // No user music opted in/present, use the shared default playlist
            usingCustomMusic = false;
            PlayDefaultPlaylist();
        }

        // Builds a shuffled playlist from the default songs pool and starts it, reusing the
        // same sequential/modulo advance PlayNextSong already does for activeInternalPlaylist
        void PlayDefaultPlaylist()
        {
            if (playableSongs == null || playableSongs.Count == 0)
            {
                Debug.LogWarning("MusicManager: no songs assigned in the Inspector, nothing to play.");
                return;
            }

            activeInternalPlaylist = new List<AudioClip>(playableSongs);
            ShuffleList(activeInternalPlaylist);
            currentInternalIndex = 0;

            musicSource.clip = activeInternalPlaylist[currentInternalIndex];
            musicSource.Play();
            UpdateUI(activeInternalPlaylist[currentInternalIndex].name);
            startMusic = true;
        }

        IEnumerator PreloadInitialBuffer()
        {
            isPreloading = true;
            fileIndex = 0;
            bufferIndex = 0;

            // Load the first batch (up to 5 songs) into RAM
            for (int i = 0; i < bufferedClips.Length; i++)
            {
                yield return StartCoroutine(LoadSingleClipToBuffer(i));
            }

            isPreloading = false;
            startMusic = true;
        
            // Play the first song now that the buffer is full
            if (bufferedClips[bufferIndex] != null)
            {
                musicSource.clip = bufferedClips[bufferIndex];
                musicSource.Play();
                UpdateUI(bufferedClips[bufferIndex].name);
            }
        }

        public void PlayNextSong()
        {

            // If there's only one song, just let it loop naturally
            if (usingCustomMusic && customPaths.Count == 1)
            {
                if (!musicSource.isPlaying) musicSource.Play();

                if (bufferedClips != null && bufferIndex < bufferedClips.Length && bufferedClips[bufferIndex] != null)
                    UpdateUI(bufferedClips[bufferIndex].name);
                return;
            }

            if (usingCustomMusic && bufferedClips.Length > 0)
            {
                // Remember the slot we are leaving so we can overwrite it
                int oldSlot = bufferIndex;

                // Move to the next slot buffer
                bufferIndex = (bufferIndex + 1) % bufferedClips.Length;


                if (bufferedClips[bufferIndex] == null)
                {
                    StartCoroutine(LoadSingleClipToBuffer(oldSlot));
                    return;
                }

                // Play the new song
                musicSource.clip = bufferedClips[bufferIndex];
                musicSource.Play();
                UpdateUI(bufferedClips[bufferIndex].name);

                // Load a brand new song into the old slot in the background
                StartCoroutine(LoadSingleClipToBuffer(oldSlot));
                return;
            }
        
            if (activeInternalPlaylist.Count > 0)
            {
                currentInternalIndex = (currentInternalIndex + 1) % activeInternalPlaylist.Count;
                musicSource.clip = activeInternalPlaylist[currentInternalIndex];
                musicSource.Play();
                UpdateUI(activeInternalPlaylist[currentInternalIndex].name);
            }
        }

        IEnumerator LoadSingleClipToBuffer(int slotToFill)
        {
            string path = customPaths[fileIndex];
            string url = "file://" + path;
        
            AudioType audioType = AudioType.MPEG;
            string extension = Path.GetExtension(path).ToLower();

            // the file type it grabs
            if (extension == ".wav") audioType = AudioType.WAV;
            else if (extension == ".ogg") audioType = AudioType.OGGVORBIS;
            else if (extension == ".mp3") audioType = AudioType.MPEG;

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    // Destroy the old audio clip to overwrite it
                    if (bufferedClips[slotToFill] != null)
                    {
                        Destroy(bufferedClips[slotToFill]);
                    }

                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    clip.name = Path.GetFileNameWithoutExtension(path);
                    bufferedClips[slotToFill] = clip;
                }
            }

            // Advance the file index to grab the next song next time
            fileIndex = (fileIndex + 1) % customPaths.Count;
        }

        void UpdateUI(string songName)
        {
            if (songNameCoroutine != null) StopCoroutine(songNameCoroutine);
            if (activeLerp != null) StopCoroutine(activeLerp);

            songNameText.rectTransform.anchoredPosition = offScreenPos;

            songNameText.text = songName;
            songNameCoroutine = StartCoroutine(FadeSongName(holdOnScreenTime, logoFadeOutTime));
        }

        // EFFECTS & HELPERS
        IEnumerator FadeSongName(float displayingTime, float fadingTime)
        {
            songNameText.gameObject.SetActive(true);
            songNameText.alpha = 1f;

            Color resetColor = logoImage.color;
            resetColor.a = 1f;
            logoImage.color = resetColor;

            activeLerp = StartCoroutine(LerpLogic(offScreenPos, startingPos));
            yield return activeLerp;

            yield return new WaitForSeconds(displayingTime);

            activeLerp = StartCoroutine(LerpLogic(startingPos, offScreenPos));
            yield return activeLerp;
                
            float elapsed = 0;
            while (elapsed < fadingTime)
            {
                elapsed += Time.deltaTime;
                songNameText.alpha = Mathf.Lerp(1f, 0f, elapsed / fadingTime);

                Color c = logoImage.color;
                c.a = Mathf.Lerp(1f, 0f, elapsed / fadingTime);
                logoImage.color = c;

                yield return null;
            }
            songNameText.alpha = 0f;
            songNameText.gameObject.SetActive(false);
        }

        IEnumerator LerpLogic(Vector2 start, Vector2 end)
        {
            float elapsed = 0f;
            while (elapsed < textScrollDuration)
            {
                elapsed += Time.deltaTime;
                songNameText.rectTransform.anchoredPosition = Vector2.Lerp(start, end, elapsed / textScrollDuration);
                yield return null;
            }

            songNameText.rectTransform.anchoredPosition = end;
        }

        void ShuffleList<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T temp = list[i];
                int randomIndex = Random.Range(i, list.Count);
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        private void OnEnable() { _skipAction?.Enable(); }
        private void OnDisable() { _skipAction?.Disable(); }
    }
}