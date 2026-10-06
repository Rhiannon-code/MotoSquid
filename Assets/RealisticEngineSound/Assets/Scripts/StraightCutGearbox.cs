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
    public class StraightCutGearbox : MonoBehaviour
    {
        RealisticEngineSound res;
        private float clipsValue;
        // master volume setting
        [Range(0.1f, 1.0f)]
        public float masterVolume = 1f;
        private float _masterVolume = 1f;
        // audio mixer
        public AudioMixerGroup audioMixer;
        private AudioMixerGroup _audioMixer;
        // audio clips
        public AudioClip onLoopClip;
        public AudioClip offLoopClip;
        public bool useOneShotClips = true;
        public AudioClip[] oneShotOnClips;
        public AudioClip[] oneShotOffClips;
        // curve settings
        public AnimationCurve onLoadVolCurve;
        public AnimationCurve offLoadVolCurve;
        public AnimationCurve loopPitchCurve;
        // sound playing distance settings
        public float minDistance = 1;
        public float maxDistance = 50;
        // audio sources
        private AudioSource onLoop;
        private AudioSource offLoop;
        private AudioSource oneShot;
        // one shot controllers
        private int pedalPressedDown = 0;
        private int pedalReleased = 0;
        private WaitForSeconds _playtime;
        void Start()
        {
            res = gameObject.transform.parent.GetComponent<RealisticEngineSound>(); // find res
            // audio mixer settings
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
            _playtime = new WaitForSeconds(0.05f);
        }
        private void OnDisable() // destroy audio sources if disabled
        {
            DestroyAll();
        }
        private void OnEnable() // recreate all audio sources if script is reEnabled
        {
            StartCoroutine(WaitForStart());
        }
        private void DestroyAll()
        {
            if (onLoop != null)
                Destroy(onLoop);
            if (offLoop != null)
                Destroy(offLoop);
            if (oneShot != null)
                Destroy(oneShot);
        }
        void Update()
        {
            clipsValue = DynamicSoundController.BasicClipsValue(res.carCurrentSpeed, res.carMaxSpeed);
            _masterVolume = masterVolume * Mathf.Lerp(1f, res.adsrVol, 0.5f);
            if (res.isAudible && res.enabled)
            {
                if (!res.isShifting)
                {
                    // gas pedal is pressed
                    if (res.gasPedalPressing)
                    {
                        pedalReleased = 0;
                        if (offLoop != null)
                        {
                            if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                                Destroy(offLoop);
                            else
                                offLoop.Pause();
                        }
                        // create one shot_on shaking sound
                        if (pedalPressedDown == 0)
                        {
                            if (useOneShotClips)
                                CreateOneShot();
                            pedalPressedDown = 1;
                        }
                        // set one shot sound's volume and pitch
                        if (oneShot != null)
                        {
                            oneShot.volume = onLoadVolCurve.Evaluate(clipsValue) * res.engineLoad * _masterVolume;
                            oneShot.pitch = loopPitchCurve.Evaluate(clipsValue);
                        }
                        // create on loop audio source
                        if (onLoop == null)
                        {
                            CreateOnLoop();
                        }
                        else
                        {
                            onLoop.volume = onLoadVolCurve.Evaluate(clipsValue) * res.engineLoad * _masterVolume;
                            onLoop.pitch = loopPitchCurve.Evaluate(clipsValue);
                            if (!onLoop.isPlaying)
                                onLoop.Play();
                        }
                    }
                    else
                    {
                        pedalPressedDown = 0;
                        // destroy or pause on loop because pedal is released
                        if (onLoop != null)
                        {
                            if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                                Destroy(onLoop);
                            else
                                onLoop.Pause();
                        }
                        // create one shot_off shaking sound
                        if (pedalReleased == 0)
                        {
                            if (useOneShotClips)
                                CreateOneShot();
                            pedalReleased = 1;
                        }
                        // set one shot off's volume and pitch
                        if (oneShot != null)
                        {
                            oneShot.volume = offLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                            oneShot.pitch = loopPitchCurve.Evaluate(clipsValue);
                        }
                        // create off loop sound
                        if (offLoop == null)
                        {
                            CreateOffLoop();
                        }
                        else
                        {
                            offLoop.volume = offLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                            offLoop.pitch = loopPitchCurve.Evaluate(clipsValue);
                            if (!offLoop.isPlaying)
                                offLoop.Play();
                        }
                    }
                }
                else
                {
                    pedalPressedDown = 0;
                    // destroy or pause on loop because pedal is released
                    if (onLoop != null)
                    {
                        if (res.optimiseAudioSources == DynamicSoundController.OptimiseAudioSources.Destroy)
                            Destroy(onLoop);
                        else
                            onLoop.Pause();
                    }
                    // create one shot_off shaking sound
                    if (pedalReleased == 0)
                    {
                        if (useOneShotClips)
                            CreateOneShot();
                        pedalReleased = 1;
                    }
                    // set one shot off's volume and pitch
                    if (oneShot != null)
                    {
                        oneShot.volume = offLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                        oneShot.pitch = loopPitchCurve.Evaluate(clipsValue);
                    }
                    // create off loop sound
                    if (offLoop == null)
                    {
                        CreateOffLoop();
                    }
                    else
                    {
                        offLoop.volume = offLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                        offLoop.pitch = loopPitchCurve.Evaluate(clipsValue);
                        if (!offLoop.isPlaying)
                            offLoop.Play();
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
            if (onLoop != null && onLoop.velocityUpdateMode != res.audioVelocityUpdateMode)
                DestroyAll();
        }
#endif
        IEnumerator WaitForStart()
        {
            while (true)
            {
                yield return _playtime; // this is needed to avoid duplicate audio sources
                if (oneShot == null)
                    Start();
                break;
            }
        }
        // create audio sources
        void CreateOnLoop()
        {
            if (onLoopClip != null)
            {
                onLoop = gameObject.AddComponent<AudioSource>();
                onLoop.rolloffMode = res.audioRolloffMode;
                onLoop.dopplerLevel = res.dopplerLevel;
                onLoop.volume = onLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                onLoop.pitch = loopPitchCurve.Evaluate(clipsValue);
                onLoop.minDistance = minDistance;
                onLoop.maxDistance = maxDistance;
                onLoop.spatialBlend = res.spatialBlend;
                onLoop.velocityUpdateMode = res.audioVelocityUpdateMode;
                onLoop.loop = true;
                if (_audioMixer != null)
                    onLoop.outputAudioMixerGroup = _audioMixer;
                onLoop.clip = onLoopClip;
                onLoop.Play();
            }
        }
        void CreateOffLoop()
        {
            if (offLoopClip != null)
            {
                offLoop = gameObject.AddComponent<AudioSource>();
                offLoop.rolloffMode = res.audioRolloffMode;
                offLoop.dopplerLevel = res.dopplerLevel;
                offLoop.volume = offLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                offLoop.pitch = loopPitchCurve.Evaluate(clipsValue);
                offLoop.minDistance = minDistance;
                offLoop.maxDistance = maxDistance;
                offLoop.spatialBlend = res.spatialBlend;
                offLoop.velocityUpdateMode = res.audioVelocityUpdateMode;
                offLoop.loop = true;
                if (_audioMixer != null)
                    offLoop.outputAudioMixerGroup = _audioMixer;
                offLoop.clip = offLoopClip;
                offLoop.Play();
            }
        }
        void CreateOneShot()
        {
            if (oneShot != null)
            {
                if (!res.gasPedalPressing || res.isShifting)
                {
                    if (oneShotOffClips != null)
                    {

                        oneShot.volume = offLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                        oneShot.pitch = loopPitchCurve.Evaluate(clipsValue);
                        oneShot.loop = false;
                        oneShot.PlayOneShot(oneShotOffClips[Random.Range(0, oneShotOffClips.Length)]);
                    }
                }
                if(res.gasPedalPressing)
                {
                    if (oneShotOnClips != null)
                    {

                        oneShot.volume = onLoadVolCurve.Evaluate(clipsValue) * _masterVolume;
                        oneShot.pitch = loopPitchCurve.Evaluate(clipsValue);
                        oneShot.loop = false;
                        oneShot.PlayOneShot(oneShotOnClips[Random.Range(0, oneShotOnClips.Length)]);
                    }
                }
            }
            else
            {
                oneShot = gameObject.AddComponent<AudioSource>();
                if (_audioMixer != null)
                    oneShot.outputAudioMixerGroup = _audioMixer;
                oneShot.rolloffMode = res.audioRolloffMode;
                oneShot.dopplerLevel = res.dopplerLevel;
                oneShot.minDistance = minDistance;
                oneShot.maxDistance = maxDistance;
                oneShot.spatialBlend = res.spatialBlend;
                oneShot.velocityUpdateMode = res.audioVelocityUpdateMode;
            }
        }
    }
}
