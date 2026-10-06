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
using UnityEngine.UI;
using TMPro;

namespace SkrilStudio.RES2
{
    public class DemoSceneController : MonoBehaviour
    {
        [Header("Manages the UI elements in the demo scene.")]
        public RealisticEngineSound[] allChildren;
        private RealisticEngineSound chosenRES2Prefab;
        public GameObject throttleButton; // UI button
        public Slider rpmSlider; // UI slider to set RPM
        public Slider pitchSlider; // UI sliter to set maximum pitch
        public Slider aggressivenessSlider;
        public Slider currentSpeedSlider;
        public TextMeshProUGUI pitchText; // UI text to show pitch multiplier value
        public TextMeshProUGUI currentSpeed;
        public TextMeshProUGUI aggressivenessText;
        public Toggle gasPedalPressing;
        public Image[] carSoundButtons;

        // wind noise & reversing sfx
        public Toggle hasReverseGearSound;
        public Toggle hasReverseBeepSound;
        public Toggle hasWindSound;
        public Button[] nextPreviousButtons; // 0 = wind previous button, 1 = wind next button, 2 = reversing gear previous button, 3 = reversing gear next button, 4 = reversing beep previous button, 5 = reversing beep next button
        public TextMeshProUGUI[] soundEffectsTexts; // 0 = wind noise selection text, 1 reversing gear sfx selection text, 2 reversing beep sfx selection text
        public AudioClip[] windClips;
        private int windID = 1; // set wind clip id
        public AudioClip[] reverseClips;
        private int reverseID = 1; // set wind clip id
        public AudioClip[] reverseBeepClips;
        private int reverseBeepID = 1; // set wind clip id

        public bool simulated = true; // is rpm simulated with gaspedal button or with rpm slider by hand
        public int currentSoundpackID = 0; // id for currently selected soundpack
        private int currentSoundpakcIDandCam = 0; // id for exterior / interior soundpack
        private bool isInterior = false;

        Color32 selectedButtonColor = new Color32(5, 10, 80, 255);
        Color32 originalButtonColor = new Color32(0x0C, 0x17, 0x20, 255);
        public GameObject[] carIcons; // 0 = exterior, 1 = interior
        public GameObject earListener;
        public GameObject cameraGameObject;

        // simulator
        public Vehicle res2Vehicle;
        public VehicleInput simulatorInput;

        public GameObject manualModeText;
        public TextMeshProUGUI infoCurrentSpeed;
        public TextMeshProUGUI infoCurrentGear;
        public TextMeshProUGUI infoVehicleBehaviourMode;

        private void Start()
        {
            currentSoundpakcIDandCam = currentSoundpackID;
            allChildren = GetComponentsInChildren<RealisticEngineSound>(true);
            ChangeCarSound(currentSoundpakcIDandCam);
            // turn off all interior prefabs
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (i % 2 == 0)
                    allChildren[i + 1].gameObject.SetActive(false);
            }
            // set values
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i].GetComponent<RealisticEngineSound>() != null)
                    allChildren[i].GetComponent<RealisticEngineSound>().carMaxSpeed = 250;
            }
            // change buttons color
            carSoundButtons[currentSoundpackID / 2].color = selectedButtonColor; // set the choosed soundpack button's color
        }

        private void Update()
        {
            pitchText.text = "" + pitchSlider.value.ToString("F2"); // set pitch multiplier value for ui text
            if (!simulated)
                currentSpeed.text = "" + currentSpeedSlider.value;
            aggressivenessText.text = "" + aggressivenessSlider.value.ToString("F2");
            UpdateInfoTexts();

            // pitch multiplier value
            if (chosenRES2Prefab != null)
                chosenRES2Prefab.pitchMultiplier = pitchSlider.value;

            // aggressiveness SFX value (volume)
            if (chosenRES2Prefab != null)
                chosenRES2Prefab.aggressivnessMaster = aggressivenessSlider.value;

            if (simulated) // rpm is simulated
            {
                rpmSlider.value = res2Vehicle.engine.realRpm; // update ui slider value to the simulated rpm value

                // converting simulated values to RES2 values
                if (chosenRES2Prefab != null)
                    chosenRES2Prefab.engineCurrentRPM = res2Vehicle.engine.realRpm; // Set the engine RPM to the simulated rpm value

                if (res2Vehicle.engine.throttle > 0.15f)
                    chosenRES2Prefab.gasPedalPressing = true;
                else
                    chosenRES2Prefab.gasPedalPressing = false;

                if (res2Vehicle.drivetrain.isShifting && !res2Vehicle.drivetrain.isDownShift)
                    chosenRES2Prefab.gasPedalPressing = false;

                if (chosenRES2Prefab != null)
                    chosenRES2Prefab.isShifting = res2Vehicle.drivetrain.isShifting;

                if (chosenRES2Prefab != null)
                    chosenRES2Prefab.carCurrentSpeed = res2Vehicle.speedKmh;
            }
            else // controll rpm via UI slider
            {
                if (chosenRES2Prefab != null)
                    chosenRES2Prefab.engineCurrentRPM = rpmSlider.value; // Set the engine RPM to the slider value

                if (chosenRES2Prefab != null)
                    chosenRES2Prefab.carCurrentSpeed = currentSpeedSlider.value;
            }
        }

        // enable/disable rev limiter
        public void UpdateRPM(Toggle togl)
        {
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i].GetComponent<RealisticEngineSound>() != null)
                    allChildren[i].GetComponent<RealisticEngineSound>().useRPMLimit = togl.isOn;
            }
        }
        // is reversing
        public void IsReversing(Toggle isReversingTgl)
        {
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i].GetComponent<RealisticEngineSound>() != null)
                    allChildren[i].GetComponent<RealisticEngineSound>().isReversing = isReversingTgl.isOn;
            }
            // reversing gear sfx
            if (hasReverseGearSound.isOn)
            {
                nextPreviousButtons[2].gameObject.SetActive(isReversingTgl.isOn);
                nextPreviousButtons[3].gameObject.SetActive(isReversingTgl.isOn);
                soundEffectsTexts[1].text = "REVERSE GEAR: " + reverseID + "/6";
            }
            // reversing beep warning sfx

            hasReverseBeepSound.interactable = isReversingTgl.isOn;
            if (hasReverseBeepSound.isOn)
            {
                nextPreviousButtons[4].gameObject.SetActive(isReversingTgl.isOn);
                nextPreviousButtons[5].gameObject.SetActive(isReversingTgl.isOn);
                soundEffectsTexts[2].text = "BEEPING SFX: " + reverseBeepID + "/4";
            }
        }
        public void IsReverseGear(Toggle isGear)
        {
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i].GetComponent<RealisticEngineSound>() != null)
                    allChildren[i].GetComponent<RealisticEngineSound>().enableReverseGear = isGear.isOn;
            }
            // gear
            nextPreviousButtons[2].interactable = isGear.isOn;
            nextPreviousButtons[3].interactable = isGear.isOn;
            // update text
            soundEffectsTexts[1].text = "REVERSE GEAR: " + reverseID + "/6";
        }
        public void IsReverseBeep(Toggle isBeep)
        {
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i].GetComponent<RealisticEngineSound>() != null)
                    allChildren[i].GetComponent<RealisticEngineSound>().enableReverseBeep = isBeep.isOn;
            }
            // beep
            nextPreviousButtons[4].interactable = isBeep.isOn;
            nextPreviousButtons[5].interactable = isBeep.isOn;
            // update text
            soundEffectsTexts[2].text = "BEEPING SFX: " + reverseBeepID + "/4";
        }
        // is simulated rpm
        public void RpmSimulationMode(TMP_Dropdown drpDown) // used to change the RPM simulation mode: manual acceleration with automatic gearbox or with manual gearbox, or manual RPM control via a UI slider without using a gearbox
        {
            if (drpDown.value == 0) // automatic shifting
            {
                simulated = true;
                throttleButton.SetActive(true); // enable throttle button
                manualModeText.SetActive(false); // disable manual mode text
                gasPedalPressing.gameObject.SetActive(false); // disable slider mode toggle - gas pedal toggle

                currentSpeedSlider.gameObject.SetActive(false); // disable current speed slider

                // enable info texts ( current speed, current gear, car behaviour mode text)
                infoCurrentSpeed.gameObject.SetActive(true);
                infoCurrentGear.gameObject.SetActive(true);
                infoVehicleBehaviourMode.gameObject.SetActive(true);

                simulatorInput.isManual = false; // change simulation mode in the rpm simulator
            }
            if (drpDown.value == 1) // manual shifting
            {
                simulated = true;
                throttleButton.SetActive(false); // disable throttle button
                manualModeText.SetActive(true); // enable manual mode text
                gasPedalPressing.gameObject.SetActive(false); // disable slider mode toggle - gas pedal toggle

                currentSpeedSlider.gameObject.SetActive(false); // disable current speed slider

                // enable info texts ( current speed, current gear, car behaviour mode text)
                infoCurrentSpeed.gameObject.SetActive(true);
                infoCurrentGear.gameObject.SetActive(true);
                infoVehicleBehaviourMode.gameObject.SetActive(true);

                simulatorInput.isManual = true; // change simulation mode in the rpm simulator
            }
            if (drpDown.value == 2) // set rpm with a slider, no shifting available
            {
                simulated = false;
                throttleButton.SetActive(false); // disable throttle button
                manualModeText.SetActive(false); // disable manual mode text
                gasPedalPressing.gameObject.SetActive(true); // enable slider mode toggle - gas pedal toggle

                currentSpeedSlider.gameObject.SetActive(true); // enable current speed slider
                UpdateGasPedal(gasPedalPressing); // update gas pedal state to UI toggle state

                // disable info texts ( current speed, current gear, car behaviour mode text)
                infoCurrentSpeed.gameObject.SetActive(false);
                infoCurrentGear.gameObject.SetActive(false);
                infoVehicleBehaviourMode.gameObject.SetActive(false);

                simulatorInput.isManual = false; // change simulation mode in the rpm simulator
            }
        }
        // change car sound buttons
        public void ChangeCarSound(int a) // a = exterior, a+1 = interior prefabs id numbers in allChildren[]
        {
            // enable currently choosed sound pack and disable those that are not
            currentSoundpackID = a;
            if (isInterior)
                currentSoundpakcIDandCam = a + 1; // id for interior
            else
                currentSoundpakcIDandCam = a;
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (allChildren[i].GetComponent<RealisticEngineSound>() != null)
                {
                    if (i != a && i != a + 1)
                        allChildren[i].GetComponent<RealisticEngineSound>().enabled = false;
                }
                if (allChildren[a].GetComponent<RealisticEngineSound>() != null)
                    allChildren[a].GetComponent<RealisticEngineSound>().enabled = true;
                if (allChildren[a + 1].GetComponent<RealisticEngineSound>() != null)
                    allChildren[a + 1].GetComponent<RealisticEngineSound>().enabled = true;
            }
            // change buttons color
            carSoundButtons[a / 2].color = selectedButtonColor; // set the choosed button's color
            for (int i = 0; i < carSoundButtons.Length; i++)
            {
                if (i * 2 != a) // set all other buttons color to white
                    carSoundButtons[i].color = originalButtonColor;
            }
            chosenRES2Prefab = allChildren[currentSoundpakcIDandCam].GetComponent<RealisticEngineSound>(); // learn which RES2 prefab is active
            if (!simulated)
                UpdateGasPedal(gasPedalPressing); // update gas pedal state to UI toggle state
            res2Vehicle.speedKmh = 0;
            res2Vehicle.speedMs = 0;
        }
        public void ExteriorInterior(Toggle togl) // true = interior false = exterior
        {
            if (!togl.isOn)
            {
                isInterior = true;
                // turn off all exterior prefabs
                for (int i = 0; i < allChildren.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        allChildren[i].gameObject.SetActive(false); // exterior
                        allChildren[i + 1].gameObject.SetActive(true); // interior
                    }
                }
                currentSoundpakcIDandCam = currentSoundpackID + 1; // id for interior soundpacks
                carIcons[0].SetActive(false);
                carIcons[1].SetActive(true);
                earListener.SetActive(false);
                gameObject.transform.position = cameraGameObject.transform.position;
            }
            else
            {
                isInterior = false;
                // turn off all interior prefabs
                for (int i = 0; i < allChildren.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        allChildren[i].gameObject.SetActive(true); // exterior
                        allChildren[i + 1].gameObject.SetActive(false); // interior
                    }
                }
                currentSoundpakcIDandCam = currentSoundpackID; // id for exterior soundpacks
                carIcons[0].SetActive(true);
                carIcons[1].SetActive(false);
                earListener.SetActive(true);
                gameObject.transform.position = new Vector3(0, 0, 0);
            }
            chosenRES2Prefab = allChildren[currentSoundpakcIDandCam].GetComponent<RealisticEngineSound>(); // learn which RES2 prefab is active
        }
        // send gas pedal checkbox state to RES2 prefab
        public void UpdateGasPedal(Toggle togl)
        {
            if (chosenRES2Prefab != null)
                chosenRES2Prefab.gasPedalPressing = togl.isOn;
        }
        private void UpdateInfoTexts()
        {
            infoCurrentSpeed.text = "Current Speed: " + Mathf.Round(res2Vehicle.speedKmh);
            if (res2Vehicle.drivetrain.gear > 0)
                infoCurrentGear.text = "Current Gear: " + res2Vehicle.drivetrain.gear;
            else
                infoCurrentGear.text = "Current Gear: N";
            infoVehicleBehaviourMode.text = "Vehicle Behaviour: " + simulatorInput.GetConfiguration(simulatorInput.vehicleType).name;
        }
        public void WindSound(Toggle isWind) // enable/disable rolling sound
        {
            for (int i = 0; i < allChildren.Length; i++)
            {
                if (hasWindSound.isOn)
                    allChildren[i].windNoiseEnabled = DynamicSoundController.WindNoiseEnum.On;
                else
                    allChildren[i].windNoiseEnabled = DynamicSoundController.WindNoiseEnum.Off;
            }
            nextPreviousButtons[0].interactable = isWind.isOn;
            nextPreviousButtons[1].interactable = isWind.isOn;
            // update isWind
            soundEffectsTexts[0].text = "WIND SFX: " + windID + "/6";
        }
        public void ChangeWindClip(bool nextOrPrevious) // true = next, false = previous
        {
            if (!nextOrPrevious) // previous sound clip
            {
                if (windID > 1)
                {
                    windID -= 1;
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        allChildren[i].windClipID = windID;
                        if (i % 2 == 0) // exterior
                            allChildren[i].windNoiseClip = windClips[(windID - 1)];
                        if (i % 2 == 1) // interior
                            allChildren[i].windNoiseClip = windClips[(windID - 1 + 5)]; // + 5 are interior sound clips
                    }
                    nextPreviousButtons[0].interactable = true; // enable button
                    nextPreviousButtons[1].interactable = true; // enable button
                }
                if (windID == 1)
                {
                    nextPreviousButtons[0].interactable = false; // disable button
                }
            }
            else // next sound clip
            {
                if (windID < 6)
                {
                    windID += 1;
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        allChildren[i].windClipID = windID;
                        if (i % 2 == 0) // exterior
                            allChildren[i].windNoiseClip = windClips[(windID - 1)];
                        if (i % 2 == 1) // interior
                            allChildren[i].windNoiseClip = windClips[(windID - 1 + 6)]; // + 5 are interior sound clips
                    }
                    nextPreviousButtons[0].interactable = true; // enable button
                    nextPreviousButtons[1].interactable = true; // enable button
                }
                if (windID == 6)
                {
                    nextPreviousButtons[1].interactable = false; // disable button
                }
            }
            // update text
            soundEffectsTexts[0].text = "WIND SFX: " + windID + "/6";
        }
        public void ChangeReversingClip(bool nextOrPrevious) // true = next, false = previous
        {
            if (!nextOrPrevious) // previous sound clip
            {
                if (reverseID > 1)
                {
                    reverseID -= 1;
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        allChildren[i].reverseClipID = reverseID;
                        if (i % 2 == 0) // exterior
                            allChildren[i].reversingClip = reverseClips[(reverseID - 1)];
                        if (i % 2 == 1) // interior
                            allChildren[i].reversingClip = reverseClips[(reverseID - 1 + 6)]; // + 6 are interior sound clips
                    }
                    nextPreviousButtons[2].interactable = true; // enable button
                    nextPreviousButtons[3].interactable = true; // enable button
                }
                if (reverseID == 1)
                {
                    nextPreviousButtons[2].interactable = false; // disable button
                }
            }
            else // next sound clip
            {
                if (reverseID < 6)
                {
                    reverseID += 1;
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        allChildren[i].reverseClipID = reverseID;
                        if (i % 2 == 0) // exterior
                            allChildren[i].reversingClip = reverseClips[(reverseID - 1)];
                        if (i % 2 == 1) // interior
                            allChildren[i].reversingClip = reverseClips[(reverseID - 1 + 6)]; // + 6 are interior sound clips
                    }
                    nextPreviousButtons[2].interactable = true; // enable button
                    nextPreviousButtons[3].interactable = true; // enable button
                }
                if (reverseID == 6)
                {
                    nextPreviousButtons[3].interactable = false; // disable button
                }
            }
            // update text
            soundEffectsTexts[1].text = "REVERSE GEAR: " + reverseID + "/6";
        }
        public void ChangeReversingBeepClip(bool nextOrPrevious) // true = next, false = previous
        {
            if (!nextOrPrevious) // previous sound clip
            {
                if (reverseBeepID > 1)
                {
                    reverseBeepID -= 1;
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        allChildren[i].reverseBeepClipID = reverseBeepID;
                        if (i % 2 == 0) // exterior
                            allChildren[i].reversingBeepClip = reverseBeepClips[(reverseBeepID - 1)];
                        if (i % 2 == 1) // interior
                            allChildren[i].reversingBeepClip = reverseBeepClips[(reverseBeepID - 1 + 4)]; // + 4 are interior sound clips
                    }
                    nextPreviousButtons[4].interactable = true; // enable button
                    nextPreviousButtons[5].interactable = true; // enable button
                }
                if (reverseBeepID == 1)
                {
                    nextPreviousButtons[4].interactable = false; // disable button
                }
            }
            else // next sound clip
            {
                if (reverseBeepID < 4)
                {
                    reverseBeepID += 1;
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        allChildren[i].reverseBeepClipID = reverseBeepID;
                        if (i % 2 == 0) // exterior
                            allChildren[i].reversingBeepClip = reverseBeepClips[(reverseBeepID - 1)];
                        if (i % 2 == 1) // interior
                            allChildren[i].reversingBeepClip = reverseBeepClips[(reverseBeepID - 1 + 4)]; // + 4 are interior sound clips
                    }
                    nextPreviousButtons[4].interactable = true; // enable button
                    nextPreviousButtons[5].interactable = true; // enable button
                }
                if (reverseBeepID == 4)
                {
                    nextPreviousButtons[5].interactable = false; // disable button
                }
            }
            // update text
            soundEffectsTexts[2].text = "BEEPING SFX: " + reverseBeepID + "/4";
        }
    }
}
