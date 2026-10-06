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
    public class TurboCharger : MonoBehaviour
    {

        RealisticEngineSound res;
        private float clipsValue;
        // master volume setting
        [Range(0.1f, 1.0f)]
        public float masterVolume = 1f;
        [Range(0.1f, 1.0f)]
        public float loopVolume = .8f;
        // audio mixer
        public AudioMixerGroup audioMixer;
        private AudioMixerGroup _audioMixer;
        // turbo loop
        public AudioClip turboLoopClip;
        public AudioClip maxRpmLoopClip; // played with rev limiter
        public AnimationCurve chargerVolCurve;
        public AnimationCurve chargerPitchCurve;
        // long shoot settings
        [Range(0.4f, 1.0f)]
        public float longShotTreshold = 0.8f; // after this % of current rpm, long shots are played when gas pedal is released
        public AudioClip[] longShotClips;
        public AudioClip[] shortShotClips;
        public AnimationCurve oneShotVolCurve;
        //
        private AudioSource turboLoop;
        private AudioSource oneShot;
        private AudioSource maxTurboLoop;

        private int oneShotController = 0;
        private WaitForSeconds _playtime;

        void Start()
        {
            res = gameObject.transform.parent.GetComponent<RealisticEngineSound>();
            if (audioMixer != null) // user is using a seperate audio mixer for this prefab
            {
                _audioMixer = audioMixer;
            }
            else
            {
                if (res.audioMixer != null) // use engine sound's audio mixer for this prefab
                {
                    _audioMixer = res.audioMixer;
                    audioMixer = _audioMixer;
                }
            }
            _playtime = new WaitForSeconds(0.15f);
        }
        void Update()
        {
            clipsValue = DynamicSoundController.BasicClipsValue(res.engineCurrentRPM, res.maxRPMLimit);
            if (res.enabled && res.gameObject.activeSelf)
            {
                if (res.isAudible)
                {
                    // if gas pedal on play turbo loop sound
                    if (res.gasPedalPressing)
                    {
                        if (oneShot != null)
                            oneShot.Stop(); // stop blowoff sound if it is still playing currently
                        oneShotController = 1; // prepare for one shoot
                        if (res.maxRPMVolCurve.Evaluate(clipsValue) < 0.5f)
                        {
                            if (turboLoop == null)
                                CreateTurboLoop();
                            if (!turboLoop.isPlaying)
                                turboLoop.Play();
                            turboLoop.volume = chargerVolCurve.Evaluate(clipsValue) * masterVolume * loopVolume * res.engineLoad;
                            turboLoop.pitch = chargerPitchCurve.Evaluate(clipsValue);
                            if (maxTurboLoop != null)
                            {
                                if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                                    Destroy(maxTurboLoop);
                                else
                                    maxTurboLoop.Pause();
                            }
                        }
                        else // play max rpm turbo loop
                        {
                            if (res.useRPMLimit)
                            {
                                if (maxTurboLoop == null)
                                    CreateMaxTurboLoop();
                                if (!maxTurboLoop.isPlaying)
                                    maxTurboLoop.Play();
                                maxTurboLoop.volume = chargerVolCurve.Evaluate(clipsValue) * masterVolume * loopVolume;
                                maxTurboLoop.pitch = chargerPitchCurve.Evaluate(clipsValue);
                                if (turboLoop != null)
                                {
                                    if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                                        Destroy(turboLoop);
                                    else
                                        turboLoop.Pause();
                                }
                            }
                        }
                    }
                    else// if gas released play one shoot
                    {
                        // destroy turbo loops
                        if (turboLoop != null)
                        {
                            if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                                Destroy(turboLoop);
                            else
                                turboLoop.Pause();
                        }
                        if (maxTurboLoop != null)
                        {
                            if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                                Destroy(maxTurboLoop);
                            else
                                maxTurboLoop.Pause();
                        }
                        // play one shot
                        if (oneShotController == 1)
                        {
                            CreateOneShot();
                            oneShotController = 0; // one shot is played, do not play more
                        }
                    }
                }
                else
                {
                    if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                    {
                        if (turboLoop != null)
                            Destroy(turboLoop);
                        if (maxTurboLoop != null)
                            Destroy(maxTurboLoop);
                        if (oneShot != null)
                            Destroy(oneShot);
                    }
                    else
                    {
                        if (turboLoop != null)
                            turboLoop.Pause();
                        if (maxTurboLoop != null)
                            maxTurboLoop.Pause();
                        if (oneShot != null)
                            oneShot.Pause();
                    } 
                }
            }
            else
            {
                DestroyAll();
            }
        }
#if UNITY_EDITOR
        private void LateUpdate()
        {
            // velocity update mode got changed on runtime, remake all audio sources
            if (turboLoop != null && turboLoop.velocityUpdateMode != res.audioVelocityUpdateMode)
                DestroyAll();
        }
#endif
        private void OnDisable() // destroy audio sources if disabled
        {
            DestroyAll();
        }
        private void OnEnable() // recreate audio sources if reEnabled
        {
            StartCoroutine(WaitForStart());
        }
        private void DestroyAll()
        {
            if (turboLoop != null)
                Destroy(turboLoop);
            if (oneShot != null)
                Destroy(oneShot);
            if (maxTurboLoop != null)
                Destroy(maxTurboLoop);
        }
        IEnumerator WaitForStart()
        {
            while (true)
            {
                yield return _playtime; // this is needed to avoid duplicate audio sources at scene start
                if (oneShot == null)
                    Start();
                break;
            }
        }
        // create audio sources
        void CreateTurboLoop()
        {
            if (turboLoopClip != null)
            {
                turboLoop = gameObject.AddComponent<AudioSource>();
                turboLoop.rolloffMode = res.audioRolloffMode;
                turboLoop.dopplerLevel = res.dopplerLevel;
                turboLoop.volume = chargerVolCurve.Evaluate(clipsValue) * masterVolume * loopVolume;
                turboLoop.pitch = chargerPitchCurve.Evaluate(clipsValue);
                turboLoop.minDistance = res.minDistance;
                turboLoop.maxDistance = res.maxDistance;
                turboLoop.spatialBlend = res.spatialBlend;
                turboLoop.velocityUpdateMode = res.audioVelocityUpdateMode;
                turboLoop.loop = true;
                if (_audioMixer != null)
                    turboLoop.outputAudioMixerGroup = _audioMixer;
                turboLoop.clip = turboLoopClip;
                turboLoop.Play();
            }
        }
        void CreateOneShot()
        {
            if (oneShot == null)
            {
                oneShot = gameObject.AddComponent<AudioSource>();
                oneShot.rolloffMode = res.audioRolloffMode;
                oneShot.dopplerLevel = res.dopplerLevel;
                oneShot.spatialBlend = res.spatialBlend;
                oneShot.minDistance = res.minDistance;
                oneShot.maxDistance = res.maxDistance;
                oneShot.velocityUpdateMode = res.audioVelocityUpdateMode;
                oneShot.loop = false;
                if (_audioMixer != null)
                    oneShot.outputAudioMixerGroup = _audioMixer;
                oneShot.volume = oneShotVolCurve.Evaluate(clipsValue) * masterVolume;
                oneShot.pitch = 1;
                if (clipsValue > longShotTreshold)
                {
                    oneShot.clip = longShotClips[Random.Range(0, longShotClips.Length)];
                }
                else
                {
                    oneShot.clip = shortShotClips[Random.Range(0, shortShotClips.Length)];
                }
                oneShot.Play();
            }
            else
            {
                oneShot.volume = oneShotVolCurve.Evaluate(clipsValue) * masterVolume;
                oneShot.pitch = 1;
                if (clipsValue > longShotTreshold)
                {
                    oneShot.clip = longShotClips[Random.Range(0, longShotClips.Length)];
                }
                else
                {
                    oneShot.clip = shortShotClips[Random.Range(0, shortShotClips.Length)];
                }
                oneShot.Play();
            }
        }
        void CreateMaxTurboLoop()
        {
            if (maxRpmLoopClip != null)
            {
                maxTurboLoop = gameObject.AddComponent<AudioSource>();
                maxTurboLoop.rolloffMode = res.audioRolloffMode;
                maxTurboLoop.dopplerLevel = res.dopplerLevel;
                maxTurboLoop.volume = chargerVolCurve.Evaluate(clipsValue) * masterVolume* loopVolume;
                maxTurboLoop.pitch = chargerPitchCurve.Evaluate(clipsValue);
                maxTurboLoop.minDistance = res.minDistance;
                maxTurboLoop.maxDistance = res.maxDistance;
                maxTurboLoop.spatialBlend = res.spatialBlend;
                maxTurboLoop.velocityUpdateMode = res.audioVelocityUpdateMode;
                maxTurboLoop.loop = true;
                if (_audioMixer != null)
                    maxTurboLoop.outputAudioMixerGroup = _audioMixer;
                maxTurboLoop.clip = maxRpmLoopClip;
                maxTurboLoop.Play();
            }
        }
    }
}
