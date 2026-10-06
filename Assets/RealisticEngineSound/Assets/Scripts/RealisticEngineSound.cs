// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SkrilStudio
{
    [System.Serializable]
    public class RealisticEngineSound : MonoBehaviour
    {
        // master volume setting
        [HideInInspector]
        [Range(0.1f, 1.0f)]
        public float masterVolume = 1f;
        private float _masterVolume;
        [HideInInspector]
        public AudioMixerGroup audioMixer;
        [HideInInspector]
        public AudioMixerGroup opponentAudioMixer;
        [HideInInspector]
        public AudioMixerGroup customAudioMixer;
        private AudioMixerGroup _choosedAudioMixer;
        GameObject audioSources;
        GameObject audioSourcesUnfiltered; // Used for reverse gear SFX as a workaround for a known Unity Engine bug that triggers a warning message about audio filters...
        public DynamicSoundController.OptimiseAudioSources optimiseAudioSources;
        public enum DynamicAudioMixer { On, Off }
        [HideInInspector]
        public DynamicAudioMixer dynamicAudioMixer = new DynamicAudioMixer();
        [HideInInspector]
        public float eqMedBoost = 1;
        [HideInInspector]
        public float highFreqMin = 0.9f; // the minimum value for high frequencies when dynamic audio mixer is enabled

        // sound level setting
        public DynamicSoundController.SoundLevels soundLevels;

        [HideInInspector]
        public float engineCurrentRPM = 0.0f;
        [HideInInspector]
        public bool gasPedalPressing = false;
        [HideInInspector]
        [Range(0.0f, 1.0f)]
        public float engineLoad = 1; // (simulated or not simulated) 0 = engine is off load, 0.5 = half engine load, 1 = engine is on full load
                                     // enum for gas pedal setting
        [HideInInspector]
        public enum GasPedalValue { Simulated, NotSimulated } // NotSimulated setting is recommended for professional car physics controllers that have value controlling for "Engine Load"
        [HideInInspector]
        public GasPedalValue gasPedalValueSetting = new GasPedalValue();
        //
        [Range(1.0f, 15.0f)]
        [HideInInspector]
        public float gasPedalSimSpeed = 5.5f; // simulates how fast the player hit the gas pedal
        [HideInInspector]
        public float maxRPMLimit = 7000;
        [HideInInspector]
        [Range(0.0f, 5.0f)]
        public float dopplerLevel = 1;
        [Range(0.0f, 1.0f)]
        [HideInInspector] // remove this line if you want to set custom values for spatialBlend
        public float spatialBlend = 1f; // this value should always be 1. If you want custom value, remove spatialBlend = 1f; line from Start()
        [HideInInspector]
        [Range(0.1f, 2.0f)]
        public float pitchMultiplier = 1.0f; // pitch value multiplier
        public bool idlePitchMultiplier = true;
        [HideInInspector]
        public AudioReverbPreset reverbZoneSetting;
        private AudioReverbPreset reverbZoneControll;

        [HideInInspector]
        [Range(0.0f, 0.25f)]
        public float optimisationLevel = 0.01f; // audio source with volume level below this value will be stopped or destroyed when optimisation is enabled
        [HideInInspector]
        public AudioRolloffMode audioRolloffMode = AudioRolloffMode.Logarithmic;
        [HideInInspector]
        public AudioVelocityUpdateMode audioVelocityUpdateMode = AudioVelocityUpdateMode.Dynamic;
        // play sounds within this distances
        [HideInInspector]
        public float minDistance = 2; // within the minimum distance the audiosources will cease to grow louder in volume
        [HideInInspector]
        public float maxDistance = 250; // maxDistance is the distance a sound stops attenuating at

        // other settings
        [HideInInspector]
        public bool isReversing = false; // is car in reverse gear
        [HideInInspector]
        public bool useRPMLimit = true; // enable rpm limit at maximum rpm
        [HideInInspector]
        public bool enableReverseGear = true; // enable wistle sound for reverse gear
        [HideInInspector]
        public bool isInterior = false;
        public enum PrefabType { Player, Opponent, Custom }
        [HideInInspector]
        public PrefabType prefabType = new PrefabType();
        [HideInInspector]
        public bool customReverseClip = false;
        [HideInInspector]
        public int reverseClipID = 1;
        private int _reverseClipID = 1;
        public enum ReverseGearSFX { Off, On }
        [HideInInspector]
        public ReverseGearSFX reverseGearSFX = new ReverseGearSFX();
        [HideInInspector]
        public float reverseBeepMaster = 1;
        [HideInInspector]
        public bool enableReverseBeep = false;
        [HideInInspector]
        public bool customReverseBeepClip = false;
        [HideInInspector]
        public int reverseBeepClipID = 1;
        private int _reverseBeepClipID = 1;
        public enum ReverseBeepSFX { Off, On }
        [HideInInspector]
        public ReverseBeepSFX reverseBeepSFX = new ReverseBeepSFX();

        // hiden public stuff
        [HideInInspector]
        public float carCurrentSpeed = 1f; // needed for aditional prefabs
        [HideInInspector]
        public float carMaxSpeed = 250f; // needed for aditional prefabs
        [HideInInspector]
        public bool isShifting = false; // needed for aditional prefabs

        // audio clips
        [HideInInspector]
        public string prefabName;
        [HideInInspector]
        public string parentFolder = "RealisticEngineSound";
        // idle clip sound
        [HideInInspector]
        public AudioClip idleClip;
        [HideInInspector]
        public AnimationCurve idleVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve idlePitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // low rpm clip sounds
        [HideInInspector]
        public AudioClip lowOffClip;
        [HideInInspector]
        public AudioClip lowOnClip;
        [HideInInspector]
        public AnimationCurve lowVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve lowPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // medium rpm clip sounds
        [HideInInspector]
        public AudioClip medOffClip;
        [HideInInspector]
        public AudioClip medOnClip;
        [HideInInspector]
        public AnimationCurve medVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve medPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // high rpm clip sounds
        [HideInInspector]
        public AudioClip highOffClip;
        [HideInInspector]
        public AudioClip highOnClip;
        [HideInInspector]
        public AnimationCurve highVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve highPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // maximum rpm clip sound - if RPM limit is enabled
        [HideInInspector]
        public AudioClip maxRPMClip;
        [HideInInspector]
        public AnimationCurve maxRPMVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // reverse gear clip sound
        [HideInInspector]
        public AudioClip reversingClip;
        [HideInInspector]
        public AudioClip reversingBeepClip;
        [HideInInspector]
        public AnimationCurve reversingVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve reversingPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);

        // wind sound sfx
        public DynamicSoundController.WindNoiseEnum windNoiseEnabled;
        [HideInInspector]
        public bool customWindClip = false;
        [HideInInspector]
        public AnimationCurve volumeCurveWind;
        [HideInInspector]
        public AnimationCurve pitchCurveWind;
        [SerializeField] private AudioSource windNoise;
        [HideInInspector]
        public AudioClip windNoiseClip;
        [HideInInspector]
        [Range(0.01f, 1.0f)]
        public float windMasterVolume = 1;
        [HideInInspector]
        public float windMinDistance = 1;
        [HideInInspector]
        public float windMaxDistance = 300;
        [HideInInspector]
        public int windClipID = 1;
        private int _windClipID = 1;
        [HideInInspector]
        public GameObject windPosition;
        [HideInInspector]
        public AudioMixerGroup windMixer;

        // sound level seven:
        // idle-low
        [HideInInspector]
        public AudioClip idle_lowOffClip;
        [HideInInspector]
        public AudioClip idle_lowOnClip;
        [HideInInspector]
        public AnimationCurve idle_lowVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve idle_lowPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // low-med
        [HideInInspector]
        public AudioClip low_medOffClip;
        [HideInInspector]
        public AudioClip low_medOnClip;
        [HideInInspector]
        public AnimationCurve low_medVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve low_medPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // med-high
        [HideInInspector]
        public AudioClip med_highOffClip;
        [HideInInspector]
        public AudioClip med_highOnClip;
        [HideInInspector]
        public AnimationCurve med_highVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve med_highPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // very-high
        [HideInInspector]
        public AudioClip very_highOffClip;
        [HideInInspector]
        public AudioClip very_highOnClip;
        [HideInInspector]
        public AnimationCurve very_highVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve very_highPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        // aggressivness fx
        public enum AggressivnessEnum { Off, On }
        [HideInInspector]
        public AggressivnessEnum enableAggressivness = new AggressivnessEnum();
        [HideInInspector]
        public AudioClip aggressivnessOnClip;
        [HideInInspector]
        public AudioClip aggressivnessOffClip;
        [HideInInspector]
        public AnimationCurve aggressivnessVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve aggressivnessPitchCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        [Range(0.01f, 1.0f)]
        public float aggressivnessMaster = 1;
        [SerializeField] private AudioSource aggressivnessOn;
        [SerializeField] private AudioSource aggressivnessOff;
        // intake sfx
        public enum IntakeEnum { Off, On }
        [HideInInspector]
        public IntakeEnum enableIntakeSFX = new IntakeEnum();
        [HideInInspector]
        public AudioClip intakeOnClip;
        [HideInInspector]
        public AudioClip intakeOffClip;
        [HideInInspector]
        [Range(0.01f, 1.0f)]
        public float intakeMaster = 1;
        private float intakeMixing = 0;
        [SerializeField] private AudioSource intakeOnAudio;
        [SerializeField] private AudioSource intakeOffAudio;
        [HideInInspector]
        public bool hasIntakeoffload = true;
        [HideInInspector]
        public AnimationCurve intakeOnVolCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        public AnimationCurve intakeOffVolCurve = AnimationCurve.Linear(0.3f, 0, 1, 0.6f);
        [HideInInspector]
        public bool enableIntakeStart;
        [HideInInspector]
        public float intakeStartMaster = 0.4f;
        [HideInInspector]
        public AudioClip[] intakeStart;
        private AudioSource intakeStartAudio;
        private int limitRuns = 0; // used to limit the number of "intake opening" sound
        [HideInInspector]
        public float intakeOnMin = 1, intakeOnMax = 300, intakeOffMin = 1, intakeOffMax = 50, intakeStartMin = 0.1f, intakeStartMax = 1;

        // global volume by rpm
        [HideInInspector]
        public AnimationCurve onLoadVolumeByRPM = AnimationCurve.Linear(0, .6f, .6f, 1);
        [HideInInspector]
        public AnimationCurve offLoadVolumeByRPM = AnimationCurve.Linear(0, .4f, .6f, 1);
        [HideInInspector]
        public bool showCurvesVolumeRPM;
        [HideInInspector]
        public bool showCurvesOffVolumeRPM;
        public enum CurvesByVolume { ShowRawCurves, ShowWithOnLoadVolume, ShowWithOffLoadVolume } // enable or disable: destroy unused or too far away audio sources that are currently can't be heard
        [HideInInspector]
        public CurvesByVolume curvesByVolume = new CurvesByVolume();

        // valves
        public DynamicSoundController.EngineValve enableValves;
        [HideInInspector]
        public AudioClip[] valveClips;
        AudioSource valveShot;
        [HideInInspector]
        public float allValvesVolume = 0.25f;
        [HideInInspector]
        public AnimationCurve valvesRPMVolume = AnimationCurve.Linear(0, 0, 1, 1);
        [HideInInspector]
        [Range(0.0005f, 0.0025f)]
        public float valveSpeed = 0.0015f;
        [HideInInspector]
        public GameObject valvePosition; // gameobject to position valve sound fx (optional)
        [HideInInspector]
        public float valvesMinDistance = 1;
        [HideInInspector]
        public float valvesMaxDistance = 50;
        [HideInInspector]
        [Range(0.5f, 2f)]
        public float valvesPitch = 1;

        // idle audio source
        [SerializeField] private AudioSource engineIdle;

        // low rpm audio sources
        [SerializeField] private AudioSource lowOff;
        [SerializeField] private AudioSource lowOn;

        // medium rpm audio sources
        [SerializeField] private AudioSource medOff;
        [SerializeField] private AudioSource medOn;

        // high rpm audio sources
        [SerializeField] private AudioSource highOff;
        [SerializeField] private AudioSource highOn;

        //maximum rpm audio source
        [HideInInspector]
        public AudioSource maxRPM;

        // sound level seven:
        // idle-low
        [SerializeField] private AudioSource idle_lowOff;
        [SerializeField] private AudioSource idle_lowOn;
        // low-med
        [SerializeField] private AudioSource low_medOff;
        [SerializeField] private AudioSource low_medOn;
        // med-high
        [SerializeField] private AudioSource med_highOff;
        [SerializeField] private AudioSource med_highOn;
        // very-high
        [SerializeField] private AudioSource very_highOff;
        [SerializeField] private AudioSource very_highOn;
        // reverse gear audio source
        [HideInInspector]
        public AudioSource reversing;
        [HideInInspector]
        public AudioSource reversingBeep;

        // private Audio Source getter
        public AudioSource EngineIdle => engineIdle;
        public AudioSource LowOff => lowOff;
        public AudioSource LowOn => lowOn;
        public AudioSource MedOff => medOff;
        public AudioSource MedOn => medOn;
        public AudioSource HighOff => highOff;
        public AudioSource HighOn => highOn;

        public AudioSource IdleLowOff => idle_lowOff;
        public AudioSource IdleLowOn => idle_lowOn;
        public AudioSource LowMedOff => low_medOff;
        public AudioSource LowMedOn => low_medOn;
        public AudioSource MedHighOff => med_highOff;
        public AudioSource MedHighOn => med_highOn;
        public AudioSource VeryHighOff => very_highOff;
        public AudioSource VeryHighOn => very_highOn;

        public AudioSource AggressivnessOn => aggressivnessOn;
        public AudioSource AggressivnessOff => aggressivnessOff;
        public AudioSource IntakeSFXOn => intakeOnAudio;
        public AudioSource IntakeSFXOff => intakeOffAudio;
        public AudioSource WindNoise => windNoise;

        //private settings
        private float clipsValue;
        // get listener for audio optimisation
        [HideInInspector]
        public AudioListener audioListener;
        [HideInInspector]
        public bool isAudible; // is the listener near

        // shake engine sound settings
        public DynamicSoundController.EngineShake engineShakeSetting;
        public DynamicSoundController.ShakeRateType shakeRateSetting;
        public DynamicSoundController.ShakeLengthType shakeLengthSetting;

        public enum OffLoadType { Prerecorded, Simulated }
        [HideInInspector]
        public OffLoadType offLoadType = new OffLoadType();
        private int _offloadTypeChecker = 0; // 0 = prerecorded, 1 = simulated

        [HideInInspector]
        public float shakeRate = 50f;
        [HideInInspector]
        public float shakeVolumeChange = 0.35f;
        [HideInInspector]
        public float randomChance = 0.5f;
        [HideInInspector]
        public float shakeLength = 1;
        [HideInInspector]
        public float shakeCarMinSpeed = 0;
        [HideInInspector]
        public float shakeCarMinRPM = 0;
        [HideInInspector]
        public float revLimiterTweaker = 3f; // modifies the very_highOn audio source's volume when maxRPM is playing [1f, 3f]
        [HideInInspector]
        public float revLimiterAggressTweaker = 2f; // modifies the aggressivnessOn audio source's volume when maxRPM is playing [1f, 3f]

        private float _endRange = 1;
        private float shakeVolumeChangeDetect; // detect value change on runtime
        private float _oscillateRange;
        private WaitForSeconds _wait;
        [HideInInspector]
        public float[] audioFloats = new float[4];
        private float[] dynamicFloats = new float[8];
        private float[] valveFloats = new float[5];

        private IEnumerator valvesCoroutine; // valves

        // Distance Acoustics
        public bool distanceAcoustics = true;
        public bool spreadAudioByDistance = true;
        public float Distance { get; private set; }
        private float lastDistance = -1; // start value
        private AudioLowPassFilter lowPassFilter;
        private AudioEchoFilter echoFilter;
        private AudioDistortionFilter distorFilter;
        private AudioSource[] allAudioSources;

        // Engine load
        private float rpmSample;
        private float rpmSampleTimer;
        private float engineWorkload;
        public float EngineWorkload => engineWorkload;
        public float adsrVol;
        public float effortSensitivity = 1f;
        public float sustainAdsrVol = .4f;

        private void Start()
        {
            if (audioSources == null)
            {
                audioSources = new GameObject("Audio Sources"); // used for filteres audio sources
                audioSources.transform.parent = this.gameObject.transform;
                audioSources.transform.position = audioSources.transform.parent.position; // set position to the center of RES prefab
                audioSources.transform.rotation = audioSources.transform.parent.rotation;
            }
            if (audioSourcesUnfiltered == null)
            {
                audioSourcesUnfiltered = new GameObject("Audio Sources Unfiltered"); // used for unfiltered audio sources
                audioSourcesUnfiltered.transform.parent = audioSources.transform;
                audioSourcesUnfiltered.transform.position = audioSources.transform.parent.position; // set position to the center of RES prefab
                audioSourcesUnfiltered.transform.rotation = audioSourcesUnfiltered.transform.parent.rotation;
            }
            spatialBlend = 1f; // value for spatialBlend
            _wait = new WaitForSeconds(0.15f); // setup wait for secconds

            if (optimiseAudioSources != DynamicSoundController.OptimiseAudioSources.Off && audioListener == null) // knowing where is the audio listener is not required when optimising audio is turned off
            {
                try
                {
                    audioListener = Camera.main.GetComponent<AudioListener>(); // try to get audio listener
                }
                catch
                {
                    Debug.Log("Audio Listener not found at scene start!");
                }
            }

            clipsValue = DynamicSoundController.BasicClipsValue(engineCurrentRPM, maxRPMLimit);
            UpdateStartRange();
            valvesCoroutine = ValvesWait();

            // set reverb
            reverbZoneControll = reverbZoneSetting;
            SetReverbZone();
            if (offLoadType == OffLoadType.Prerecorded)
                _offloadTypeChecker = 0;
            else
                _offloadTypeChecker = 1;

            // audio mixer
            if (prefabType == PrefabType.Player)
                _choosedAudioMixer = audioMixer;
            if (prefabType == PrefabType.Opponent)
                _choosedAudioMixer = opponentAudioMixer;
            if (prefabType == PrefabType.Custom)
                _choosedAudioMixer = customAudioMixer;
        }
        private void Update()
        {
            float master = masterVolume * _masterVolume;
            adsrVol = Mathf.Lerp(Mathf.Max(1 - clipsValue, sustainAdsrVol), Mathf.Lerp(sustainAdsrVol, 1f, engineWorkload), engineLoad) * master;
            DynamicSoundController.AudioValueControll(audioFloats, minDistance, maxDistance, dopplerLevel, spatialBlend);
            DynamicSoundController.DynamicSoundValues(dynamicFloats, clipsValue, adsrVol, engineLoad, pitchMultiplier, optimisationLevel, aggressivnessMaster, revLimiterTweaker, revLimiterAggressTweaker);
            DynamicSoundController.ValveValuesControll(valveFloats, allValvesVolume, valvesPitch, valvesMinDistance, valvesMaxDistance);
            engineWorkload = EngineEffort();

            // valves sfx
            if (enableValves == DynamicSoundController.EngineValve.On)
                if (valveShot == null)
                    Valves();
                else
                if (valveShot != null)
                    StopCoroutine(valvesCoroutine);

            // idle
            engineIdle = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, engineIdle, idleClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, idleVolCurve, idlePitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, idlePitchMultiplier);

            if (soundLevels != DynamicSoundController.SoundLevels.One)
            {
                if (offLoadType == OffLoadType.Prerecorded)
                {
                    // low rpm
                    lowOn = DynamicSoundController.DynamicSound(audioSources, lowOn, maxRPM, lowOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, lowVolCurve, lowPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                    lowOff = DynamicSoundController.DynamicSound(audioSources, lowOff, maxRPM, lowOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, lowVolCurve, lowPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                    // medium
                    medOn = DynamicSoundController.DynamicSound(audioSources, medOn, maxRPM, medOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, medVolCurve, medPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                    medOff = DynamicSoundController.DynamicSound(audioSources, medOff, maxRPM, medOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, medVolCurve, medPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                    // very_high rpm
                    very_highOn = DynamicSoundController.DynamicSound(audioSources, very_highOn, maxRPM, very_highOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, very_highVolCurve, very_highPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                    very_highOff = DynamicSoundController.DynamicSound(audioSources, very_highOff, maxRPM, very_highOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, very_highVolCurve, very_highPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                }
                else
                {
                    // low rpm
                    lowOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, lowOn, lowOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, lowVolCurve, lowPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                    // medium
                    medOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, medOn, medOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, medVolCurve, medPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                    // very_high rpm
                    very_highOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, very_highOn, very_highOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, very_highVolCurve, very_highPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                }
                // Sound level seven
                if (soundLevels == DynamicSoundController.SoundLevels.Seven)
                {

                    if (offLoadType == OffLoadType.Prerecorded)
                    {
                        // idle-low
                        idle_lowOn = DynamicSoundController.DynamicSound(audioSources, idle_lowOn, maxRPM, idle_lowOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, idle_lowVolCurve, idle_lowPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                        idle_lowOff = DynamicSoundController.DynamicSound(audioSources, idle_lowOff, maxRPM, idle_lowOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, idle_lowVolCurve, idle_lowPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                        // low-medium
                        low_medOn = DynamicSoundController.DynamicSound(audioSources, low_medOn, maxRPM, low_medOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, low_medVolCurve, low_medPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                        low_medOff = DynamicSoundController.DynamicSound(audioSources, low_medOff, maxRPM, low_medOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, low_medVolCurve, low_medPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                        // high-medium
                        med_highOn = DynamicSoundController.DynamicSound(audioSources, med_highOn, maxRPM, med_highOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, med_highVolCurve, med_highPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                        med_highOff = DynamicSoundController.DynamicSound(audioSources, med_highOff, maxRPM, med_highOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, med_highVolCurve, med_highPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                        // high
                        highOn = DynamicSoundController.DynamicSound(audioSources, highOn, maxRPM, highOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, highVolCurve, highPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                        highOff = DynamicSoundController.DynamicSound(audioSources, highOff, maxRPM, highOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, highVolCurve, highPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                    }
                    else
                    {
                        // idle-low
                        idle_lowOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, idle_lowOn, idle_lowOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, idle_lowVolCurve, idle_lowPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                        // low-medium
                        low_medOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, low_medOn, low_medOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, low_medVolCurve, low_medPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                        // high-medium
                        med_highOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, med_highOn, med_highOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, med_highVolCurve, med_highPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                        // high
                        highOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, highOn, highOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, highVolCurve, highPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                    }
                }
            }

            // rpm limiting
            maxRPM = DynamicSoundController.DynamicSoundMax(audioSources, maxRPM, maxRPMClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, maxRPMVolCurve, very_highPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, soundLevels, useRPMLimit);

            // reversing gear sound
            reversing = DynamicSoundController.DynamicSoundReverseGear(audioSourcesUnfiltered, reversing, reversingClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, reversingVolCurve, reversingPitchCurve, dynamicFloats, optimiseAudioSources, isAudible, enableReverseGear, isReversing, carCurrentSpeed);
            // reversing beep sound
            reversingBeep = DynamicSoundController.DynamicSoundReverseBeep(audioSourcesUnfiltered, reversingBeep, reversingBeepClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, reverseBeepMaster, _choosedAudioMixer, optimiseAudioSources, isAudible, enableReverseBeep, isReversing);

            // aggressivness sfx
            if (enableAggressivness == AggressivnessEnum.On) // aggressivness is enabled
            {
                if (offLoadType == OffLoadType.Prerecorded)
                {
                    aggressivnessOn = DynamicSoundController.DynamicSound(audioSources, aggressivnessOn, maxRPM, aggressivnessOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, aggressivnessVolCurve, aggressivnessPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, true);
                    aggressivnessOff = DynamicSoundController.DynamicSound(audioSources, aggressivnessOff, maxRPM, aggressivnessOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, aggressivnessVolCurve, aggressivnessPitchCurve, offLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, true);
                }
                else
                {
                    aggressivnessOn = DynamicSoundController.DynamicSoundUnloaded(audioSources, maxRPM, aggressivnessOn, aggressivnessOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, aggressivnessVolCurve, aggressivnessPitchCurve, offLoadVolumeByRPM, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, true);
                }
            }
            else // aggressivness sfx is disabled on runtime
            {
                Destroy(aggressivnessOn);
                Destroy(aggressivnessOff);
            }
            // intake sfx
            if (enableIntakeSFX == IntakeEnum.On && intakeOnClip != null) // intake SFX is enabled and check if sound clip exist just in case
            {
                intakeOnAudio = DynamicSoundController.DynamicSound(audioSources, intakeOnAudio, maxRPM, intakeOnClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, intakeOnVolCurve, aggressivnessPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, true, false);
                intakeOnAudio.volume *= intakeMaster * intakeMixing; // intake audio real volume
                if (intakeOnAudio.minDistance != intakeOnMin || intakeOnAudio.maxDistance != intakeOnMax) // intakeOn min/max distance values are modified during runtime
                {
                    intakeOnAudio.minDistance = intakeOnMin;
                    intakeOnAudio.maxDistance = intakeOnMax;
                }
                // off load
                if (hasIntakeoffload)
                {
                    intakeOffAudio = DynamicSoundController.DynamicSound(audioSources, intakeOffAudio, maxRPM, intakeOffClip, audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, intakeOffVolCurve, aggressivnessPitchCurve, onLoadVolumeByRPM, dynamicFloats, optimiseAudioSources, isAudible, false, false);
                    intakeOffAudio.volume *= intakeMaster * intakeMixing; // intake audio real volume
                    if (intakeOffAudio.minDistance != intakeOffMin || intakeOffAudio.maxDistance != intakeOffMax) // intakeOff min/max distance values are modified during runtime
                    {
                        intakeOffAudio.minDistance = intakeOffMin;
                        intakeOffAudio.maxDistance = intakeOffMax;
                    }
                }
                if (!hasIntakeoffload && intakeOffAudio != null) // intake off-load sfx got disabled during runtime
                {
                    Destroy(intakeOffAudio);
                }
                // intake start sfx
                if (enableIntakeStart)
                {
                    if (intakeStartAudio != null)
                    {
                        if (gasPedalPressing && limitRuns == 0)
                        {
                            intakeStartAudio.PlayOneShot(intakeStart[Random.Range(0, intakeStart.Length)]); // play a random intake start sfx when gas pedal just got pressed down
                            intakeStartAudio.volume = intakeStartMaster * intakeMixing;
                            limitRuns = 1;
                        }
                        if (!gasPedalPressing)
                            limitRuns = 0;
                        if (intakeStartAudio.minDistance != intakeStartMin || intakeStartAudio.maxDistance != intakeStartMax) // intakeStart min/max distance values are modified during runtime
                        {
                            intakeStartAudio.minDistance = intakeStartMin;
                            intakeStartAudio.maxDistance = intakeStartMax;
                        }
                    }
                    else
                    {
                        // create audio source for intake start sfx, in case if feature got enabled during runtime
                        intakeStartAudio = DynamicSoundController.CreateAudioSource(audioSources, intakeStartAudio, intakeStart[Random.Range(0, intakeStart.Length)], audioRolloffMode, audioVelocityUpdateMode, audioFloats, _choosedAudioMixer, false, false);
                    }
                }
            }
            else // intake sfx is disabled on runtime
            {
                if (intakeOnAudio != null)
                    Destroy(intakeOnAudio);
                if (intakeOffAudio != null)
                    Destroy(intakeOffAudio);
            }

            // wind noise
            windNoise = DynamicSoundController.WindNoise(windPosition, audioSourcesUnfiltered, windNoise, windNoiseClip, volumeCurveWind, pitchCurveWind, windNoiseEnabled, audioRolloffMode, audioVelocityUpdateMode, windMixer, _choosedAudioMixer, optimiseAudioSources, audioListener, dynamicFloats, windMasterVolume * masterVolume, windMinDistance, windMaxDistance, audioFloats, carCurrentSpeed, carMaxSpeed);

            // smothly increase master volume when prefab got enabled
            SmoothMasterVolume();
        }
        private float EngineEffort()
        {
            rpmSampleTimer += Time.deltaTime;

            if (rpmSampleTimer >= 0.05f)
            {
                if (engineCurrentRPM < 900f)
                {
                    engineWorkload = 0f;
                }
                else
                {
                    float rpmChange =
                    engineCurrentRPM - rpmSample;

                    float rpmPerSecond =
                        rpmChange / rpmSampleTimer;

                    rpmPerSecond = Mathf.Max(0f, rpmPerSecond);

                    // Relative RPM acceleration
                    float rpmRate =
                        rpmPerSecond / Mathf.Max(engineCurrentRPM, 1f);

                    // Speed increases RPM-rate sensitivity
                    float speedFactor = Mathf.InverseLerp(
                        0f,
                        carMaxSpeed,
                        carCurrentSpeed
                    );

                    float adjustedRPMRate =
                        rpmRate * Mathf.Lerp(1f, 2f, speedFactor);

                    adjustedRPMRate *= effortSensitivity;

                    float rpmEffort = Mathf.InverseLerp(
                        0.03f,
                        0.35f,
                        adjustedRPMRate
                    );

                    // Additional effort at high speed
                    float highSpeedEffort = Mathf.InverseLerp(
                        carMaxSpeed / 2,
                        carMaxSpeed,
                        carCurrentSpeed
                    );

                    // Max RPM influence
                    float maxRPMFactor = Mathf.InverseLerp(
                        0.75f, // starts normalizing
                        0.9f, // normalized to maximum
                        engineCurrentRPM / maxRPMLimit
                    );

                    // Combine RPM, speed and max RPM effort
                    float targetEffort = Mathf.Max(
                        rpmEffort,
                        highSpeedEffort,
                        maxRPMFactor
                    );

                    float speed = targetEffort > engineWorkload
                        ? 10f   // Attack
                        : 2f;   // Release

                    engineWorkload = Mathf.Lerp(
                        engineWorkload,
                        targetEffort,
                        speed * rpmSampleTimer
                    );
                }
                if (distorFilter != null)
                    distorFilter.distortionLevel = (engineWorkload * adsrVol) / 3.3f;
                rpmSample = engineCurrentRPM;
                rpmSampleTimer = 0f;
            }

            return engineWorkload;
        }
        private void OnEnable() // recreate all audio sources if Realistic Engine Sound's script is reEnabled
        {
            StartCoroutine(WaitForStart());
            SetReverbZone();
            _masterVolume = 0;
        }
        private void OnDisable() // destroy audio sources if Realistic Engine Sound's script is disabled
        {
            DestroyAll();
        }
        public void DestroyAll() // destroy audio sources
        {
            if (lowPassFilter != null) // remove Distance Acoustics components
            {
                Destroy(lowPassFilter);
                Destroy(echoFilter);
                Destroy(distorFilter);
            }
            if (engineIdle != null)
                Destroy(engineIdle);
            if (lowOn != null)
                Destroy(lowOn);
            if (lowOff != null)
                Destroy(lowOff);
            if (medOn != null)
                Destroy(medOn);
            if (medOff != null)
                Destroy(medOff);
            if (highOn != null)
                Destroy(highOn);
            if (highOff != null)
                Destroy(highOff);
            if (idle_lowOn != null)
                Destroy(idle_lowOn);
            if (idle_lowOff != null)
                Destroy(idle_lowOff);
            if (low_medOn != null)
                Destroy(low_medOn);
            if (low_medOff != null)
                Destroy(low_medOff);
            if (med_highOn != null)
                Destroy(med_highOn);
            if (med_highOff != null)
                Destroy(med_highOff);
            if (very_highOn != null)
                Destroy(very_highOn);
            if (very_highOff != null)
                Destroy(very_highOff);
            if (aggressivnessOn != null)
                Destroy(aggressivnessOn);
            if (intakeOnAudio != null)
                Destroy(intakeOnAudio);
            if (intakeOffAudio != null)
                Destroy(intakeOffAudio);
            if (intakeStartAudio != null)
                Destroy(intakeStartAudio);
            if (aggressivnessOff != null)
                Destroy(aggressivnessOff);
            if (valveShot != null)
                Destroy(valveShot);
            if (windNoise != null)
                Destroy(windNoise);
            if (useRPMLimit)
                if (maxRPM != null)
                    Destroy(maxRPM);
            if (enableReverseGear)
                if (reversing != null)
                    Destroy(reversing);
            if (enableReverseBeep)
                if (reversingBeep != null)
                    Destroy(reversingBeep);
            if (audioSources != null)
                if (audioSources.GetComponent<AudioReverbZone>() != null)
                    Destroy(audioSources.GetComponent<AudioReverbZone>());
        }
        private void FixedUpdate()
        {
            // engine load calculation
            if (gasPedalValueSetting == RealisticEngineSound.GasPedalValue.Simulated)
            {
                if (shakeVolumeChangeDetect != shakeVolumeChange)
                    UpdateStartRange();
                var _engineLoad = DynamicSoundController.DynamicEngineLoad(engineShakeSetting, shakeLengthSetting, shakeRateSetting, gasPedalPressing, engineLoad, maxRPMLimit, engineCurrentRPM, clipsValue, gasPedalSimSpeed, carCurrentSpeed, shakeVolumeChange, shakeCarMinSpeed, shakeCarMinRPM, shakeLength, shakeRate, _oscillateRange, randomChance);
                clipsValue = _engineLoad.clipsValue;
                engineLoad = _engineLoad.gasPedalValue;
            }
            else
            {
                clipsValue = DynamicSoundController.BasicClipsValue(engineCurrentRPM, maxRPMLimit);
            }
        }
        private void LateUpdate()
        {
            if (optimiseAudioSources != DynamicSoundController.OptimiseAudioSources.Off)
            {
                if (audioListener != null)
                    isAudible = DynamicSoundController.AudioDistanceOptimiser(isAudible, audioListener, audioSources, maxDistance);
                else
                    Debug.LogWarning("Audio Listener not found! The 'Optimise Audio Sources' feature requires an active Audio Listener in the scene. The Audio Listener may be missing or may have been removed at runtime. Try assigning it manually in the engine sound prefab.", gameObject);
            }

            // Distance Acoustics is enabled
            if (distanceAcoustics)
            {
                if (lowPassFilter == null) // setting up and adding components
                {
                    if (EngineIdle != null) // make sure audio sources are already created - Unity's AudioSource creation runs slower than the RES2 script
                    {
                        lowPassFilter = audioSources.AddComponent<AudioLowPassFilter>();
                        echoFilter = audioSources.AddComponent<AudioEchoFilter>();
                        distorFilter = audioSources.AddComponent<AudioDistortionFilter>();
                    }
                    // get all engine sound related audio sources from prefab
                    allAudioSources = audioSources.GetComponentsInChildren<AudioSource>(true);
                    if (echoFilter != null) // still need to check for null even if echoFilter creation is called above, Unity Engine bug...
                        echoFilter.delay = 20;
                }
                else // effects ready and runing
                {
                    // tweak effects based on distance (distance between the audio listener and res2 prefab)
                    float t = Mathf.InverseLerp(minDistance, maxDistance, Distance);
                    lowPassFilter.cutoffFrequency = Mathf.Lerp(18000f, 2000f, t);
                    echoFilter.wetMix = Distance / 25;
                    if (0.3f + Distance / 500 < 0.7f) // if current decay < 0.7f
                        echoFilter.decayRatio = 0.3f + Distance / 500;

                    // apply spread effect to each playing audio
                    if (spreadAudioByDistance)
                    {
                        // exterior spread
                        if (!isInterior)
                        {
                            // only update spread in audioSources if distance change is more than 0.1f
                            if (Mathf.Abs(Distance - lastDistance) >= 0.1f)
                            {
                                lastDistance = Distance;
                                for (int i = 0; i < allAudioSources.Length; i++)
                                {
                                    if (allAudioSources[i] != null && allAudioSources[i].isPlaying)
                                    {
                                        allAudioSources[i].spread = Distance * 4;
                                    }
                                }
                            }
                        }
                        // interior spread
                        if (isInterior && allAudioSources[allAudioSources.Length - 1].spread != 180)
                        {
                            for (int i = 0; i < allAudioSources.Length; i++)
                            {
                                if (allAudioSources[i] != null && allAudioSources[i].isPlaying)
                                {
                                    allAudioSources[i].spread = 180;
                                }
                            }
                        }
                    }
                }
            }
            if (!distanceAcoustics && lowPassFilter != null) // Distance Acoustics got disabled, remove their components
            {
                Destroy(lowPassFilter);
                Destroy(echoFilter);
                Destroy(distorFilter);
            }

            // these audio clips got changed at runtime, needs updating their playing
            if (windNoiseEnabled == DynamicSoundController.WindNoiseEnum.On)
            {
                if (_windClipID != windClipID) // clip got changed on runtime
                {
                    if (windNoise != null)
                    {
                        windNoise.Stop();
                        windNoise.clip = windNoiseClip;
                        windNoise.Play();
                        _windClipID = windClipID;
                    }
                }
            }
            if (enableReverseGear)
            {
                if (_reverseClipID != reverseClipID) // clip got changed on runtime
                {
                    if (reversing != null)
                    {
                        reversing.Stop();
                        reversing.clip = reversingClip;
                        reversing.Play();
                        _reverseClipID = reverseClipID;
                    }
                }
            }
            if (enableReverseBeep)
            {
                if (_reverseBeepClipID != reverseBeepClipID) // clip got changed on runtime
                {
                    if (reversingBeep != null)
                    {
                        reversingBeep.Stop();
                        reversingBeep.clip = reversingBeepClip;
                        reversingBeep.Play();
                        _reverseBeepClipID = reverseBeepClipID;
                    }
                }
            }
            Distance = DynamicSoundController.RelativePosCalculation(audioListener, gameObject); // used by Dynamic Audio Mixer, Intake SFX and Distance Acoustics
            if (dynamicAudioMixer == DynamicAudioMixer.On && prefabType == PrefabType.Player)
            {
                if (audioMixer != null)
                {
                    if (!isInterior)
                    {
                        if (audioListener != null)
                            DynamicSoundController.DynamicSoundMixing(engineCurrentRPM, true, eqMedBoost, highFreqMin, audioMixer); // dynamic audio mixer changes the sound mixing based on how you look at player's vehicle - it will sound differently when looking at the front and at the back - currently this only works with player vehicle
                        else
                            Debug.LogWarning("Dynamic Audio Mixer could not find the Audio Listener. Disable 'Dynamic Audio Mixer' or include the missing Audio Listener in the engine sound prefab.", gameObject);
                    }
                }
                else
                {
                    Debug.LogWarning("Dynamic Audio Mixer is requiring the stock 'exterior' audio mixer that is shipped with the asset! Each 'exterior' prefab using this audiomixer by default. Please use that audio mixer, or disable 'Dynamic Audio Mixer'.", gameObject);
                }
            }
            else
            {
                if (audioMixer != null)
                    DynamicSoundController.ResetDynamicSoundMixing(audioMixer); // reset player audio mixer values to default
            }
            if (enableIntakeSFX == IntakeEnum.On) // intake is only heard when the camera is looking at the front of the vehicle engine and can be heard in interior view too regardless of camera position
            {
                if (!isInterior)
                    intakeMixing = DynamicSoundController.IntakeMixing();
                else
                    intakeMixing = 1;
            }
            if (reverbZoneControll != reverbZoneSetting)
                SetReverbZone();
            if (_offloadTypeChecker == 0 && offLoadType != OffLoadType.Prerecorded) // offLoadType got changed on runtime
            {
                if (lowOff != null)
                    Destroy(lowOff);
                if (medOff != null)
                    Destroy(medOff);
                if (highOff != null)
                    Destroy(highOff);
                if (idle_lowOff != null)
                    Destroy(idle_lowOff);
                if (low_medOff != null)
                    Destroy(low_medOff);
                if (med_highOff != null)
                    Destroy(med_highOff);
                if (very_highOff != null)
                    Destroy(very_highOff);
                if (aggressivnessOff != null)
                    Destroy(aggressivnessOff);
                _offloadTypeChecker = 1;
            }
            if (_offloadTypeChecker == 1 && offLoadType != OffLoadType.Simulated) // offLoadType got changed on runtime
                _offloadTypeChecker = 0;
            if (prefabType == PrefabType.Player)
                DynamicSoundController.OffLoadSimulator(dynamicFloats, audioMixer, offLoadVolumeByRPM, onLoadVolumeByRPM); // offload simulation inside audio mixer - for this it is required to use that audio mixer which comes with the asset

            if (prefabType == PrefabType.Player && _choosedAudioMixer != audioMixer) // prefab player/opponent type got changed on runtime, change audio mixer too on runtime
            {
                _choosedAudioMixer = audioMixer;
                DestroyAll(); // re-create audio sources with the newly choosed audio mixer
            }
            if (prefabType == PrefabType.Opponent && _choosedAudioMixer != opponentAudioMixer)
            {
                _choosedAudioMixer = opponentAudioMixer;
                DestroyAll(); // re-create audio sources with the newly choosed audio mixer
            }
            if (prefabType == PrefabType.Custom && _choosedAudioMixer != customAudioMixer)
            {
                _choosedAudioMixer = customAudioMixer;
                DestroyAll(); // re-create audio sources with the newly choosed audio mixer
            }
            if (maxRPMClip == null && useRPMLimit)
            {
                useRPMLimit = false;
                Debug.LogWarning("RES2 Warning. RPM Limiter audio clip is missing! To prevent errors Rev Limiter SFX got disabled automatically. To make this warning message disappear add the missing audio clip to the prefab, or disable Rev Limiter in the engine sound prefab before entering Play mode.", maxRPM);
            }
        }
        void SmoothMasterVolume()
        {
            if (_masterVolume < masterVolume)
                _masterVolume += .3f;
            else
                _masterVolume = masterVolume;
        }
        public void UpdateStartRange() // needed for engine load calculation
        {
            _oscillateRange = (_endRange - (1 - shakeVolumeChange));
            shakeVolumeChangeDetect = shakeVolumeChange; // detect value change on runtime
        }
        void SetReverbZone()
        {
            if (reverbZoneSetting == AudioReverbPreset.Off)
            {
                if (audioSources != null)
                    if (audioSources.GetComponent<AudioReverbZone>() != null)
                        Destroy(audioSources.GetComponent<AudioReverbZone>());
            }
            else
            {
                if (audioSources != null)
                {
                    if (audioSources.GetComponent<AudioReverbZone>() == null)
                        audioSources.AddComponent<AudioReverbZone>().reverbPreset = reverbZoneSetting;
                    else
                        audioSources.GetComponent<AudioReverbZone>().reverbPreset = reverbZoneSetting;
                }
            }
            reverbZoneControll = reverbZoneSetting;
        }
        IEnumerator WaitForStart()
        {
            while (true)
            {
                yield return _wait; // this is needed to avoid duplicate audio sources when gameobject is just enabled
                if (engineIdle == null)
                    Start();
                break;
            }
        }
        // valves
        void Valves()
        {
            if (valveClips != null)
                StartCoroutine(ValvesWait());
        }
        public IEnumerator ValvesWait()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f / (engineCurrentRPM * valveSpeed) + 0.001f); // wait value must be more than 0
                if (audioListener != null)
                    valveShot = DynamicSoundController.ValvesSFX(valvePosition, audioSources, valveShot, valveClips, audioRolloffMode, audioVelocityUpdateMode, enableValves, _choosedAudioMixer, valvesRPMVolume, audioListener, valveFloats, audioFloats, dynamicFloats);
                else
                    Debug.LogWarning("Valve SFX: Audio Listener is missing or got deleted on runtime! Adding the currently active Audio Listener to the engine sound prefab is required when using the Valve SFX.");
                if (valveShot == null)
                {
                    yield break;
                }
                if (enableValves == DynamicSoundController.EngineValve.Off)
                {
                    yield break;
                }
            }
        }
    }
}
