using MotoSquid.UI;
using UnityEngine;
using TMPro;

namespace MotoSquid.Race
{
    public class TimerSystem : MonoBehaviour
    {
        [Header("Timer Settings")]
        [SerializeField] private float timerDuration = 60f;
        [HideInInspector] public float totalTime = 0f;
    
        [Header("Warning Settings")]
        [SerializeField] private float timeLow = 10f;
        [SerializeField, Min(0.1f)] private float timeCritical = 5f;
        [SerializeField] private float flashSpeed = 5f;

        [Header("Bonus Settings")]
        private bool isBonusActive = false;
        [SerializeField] private float bonusEffectDuration = 0.5f;
        [SerializeField] private float lerpSpeed = 10f; 

        [Header("UI Colors")]
        [SerializeField] private Color baseColor = Color.yellow;
        [SerializeField] private Color bonusColor = Color.green;
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField] private Color flashingColor = Color.white;
        [SerializeField] private Color gameOverColor = Color.white;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip bonusSound;
        [SerializeField] private AudioClip criticalSound;

        [Header("UI Reference")]
        [SerializeField] private TMP_Text CountDownText;

        [SerializeField]private EndGameManager endGameManager;

        [HideInInspector]
        public bool isGameActive = false;

        int m_LastDisplayedSeconds = -1;
        Color m_LastColor = new Color(-1, -1, -1, -1);
        Vector3 m_LastScale = Vector3.one * -1f;
    
        void Start()
        {   
            isGameActive = false;
            CountDownText.enabled = false;
        }

        void Update()
        {
            if (isGameActive)
            {
                timerDuration -= Time.deltaTime;

            
                if (timerDuration > 0)
                {
                    totalTime += Time.deltaTime;
                }
                else
                {
                    timerDuration = 0;

                    OnTimeUp();
                    return;
                }

                UpdateTimerUI();
            }
        }

        void UpdateTimerUI()
        {
            //For normal 00
            int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, timerDuration));
            if (displaySeconds != m_LastDisplayedSeconds)
            {
                CountDownText.text = displaySeconds.ToString();
                m_LastDisplayedSeconds = displaySeconds;
            }

            /* For the 00:00.00 layout
            float displayTime = Mathf.Max(0f, timerDuration);

            int minutes = Mathf.FloorToInt(displayTime / 60F);
            int seconds = Mathf.FloorToInt(displayTime % 60F);
            int fractions = Mathf.FloorToInt((displayTime % 1F) * 100F);

        
            CountDownText.text = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fractions);
            */

            HandleVisuals();
        }

        void SetTextColor(Color c)
        {
            if (c != m_LastColor) { CountDownText.color = c; m_LastColor = c; }
        }

        void SetTextScale(float s)
        {
            Vector3 v = new Vector3(s, s, s);
            if (v != m_LastScale) { CountDownText.transform.localScale = v; m_LastScale = v; }
        }

        void HandleVisuals()
        {
            if (timerDuration <= timeCritical && !isBonusActive)
            {
                //Pulsing scale
                float s = 1f + (Mathf.PingPong(Time.time * flashSpeed, 0.2f));
                SetTextScale(s);

                if (audioSource != null && criticalSound != null)
                {
                    if (!audioSource.isPlaying) 
                    {
                        audioSource.clip = criticalSound; 
                        audioSource.loop = true;          
                        audioSource.Play();
                    }
                    else if (audioSource.clip == criticalSound) 
                    {
                        float t = 1 - Mathf.InverseLerp(0, timeCritical, timerDuration); 
                        audioSource.pitch = Mathf.Lerp(1.0f, 2.0f, t);
                    }

                }
            }
            else
            {
                //LERP smoothly brings the scale back to normal
                float s = Vector3.Lerp(m_LastScale, Vector3.one, Time.deltaTime * lerpSpeed).x;
                SetTextScale(s);

                if (audioSource != null && audioSource.isPlaying && audioSource.clip == criticalSound)
                {
                    audioSource.Stop();
                    audioSource.loop = false;
                    audioSource.pitch = 1.0f;
                }
            }

            if (isBonusActive) return; 

            if (timerDuration <= timeLow)
            {
                // Flash between Red and White
                float t = Mathf.PingPong(Time.time * flashSpeed, 1);
                SetTextColor(Color.Lerp(warningColor, flashingColor, t));
            }
            else
            {
                SetTextColor(baseColor);
            }
        }

        public void AddTimeBonus(float bonusAmount)
        {
            if (!isGameActive) return;
            timerDuration += bonusAmount;

            PlayBonusEffects();
        }

        private void PlayBonusEffects()
        {
            isBonusActive = true;
            CountDownText.color = bonusColor;

            //Punch the scale up instantly
            CountDownText.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            if (audioSource != null && bonusSound != null)
            {
                //Randomizes the sound slightly
                //audioSource.pitch = Random.Range(0.9f, 1.1f); 

                //Debug.Log("Playing bonus sound");
                audioSource.pitch = 1.0f;
                audioSource.loop = false;
                audioSource.clip = bonusSound;          
                audioSource.Play();
            }
        
            Invoke(nameof(ResetBonusState), bonusEffectDuration);
        }

        private void ResetBonusState() => isBonusActive = false;

        //Call this to start the timer and game instead of on start if you want to control when the game begins
        public void StartGame()
        {
            isGameActive = true;
            Cursor.visible = false;
            CountDownText.color = baseColor;
            CountDownText.enabled = true;
        }

        public void CleanUp()
        {
            isGameActive = false;

            CancelInvoke(nameof(ResetBonusState));
            CountDownText.text = "0";
            CountDownText.color = gameOverColor;
            CountDownText.transform.localScale = Vector3.one;
            CountDownText.enabled = false;

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.loop = false;
                audioSource.pitch = 1.0f;
            }
        }


        public void StopAndGetTime(out float elapsedTime /*out float bestLap*/)
        {
            elapsedTime = totalTime;
            //bestLap = this.bestLapTime; When I make it track lap times, I'll set this up to return the best lap time as well
            CleanUp();
        }

        void OnTimeUp()
        {

            // Time ran out = DNF, show the survived time but don't write it as a best time record
            endGameManager.StartEndSequence(totalTime, false, false);
            CleanUp();
        }
    }
}