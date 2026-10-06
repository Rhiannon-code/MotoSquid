using MotoSquid.Bike;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Michsky.UI.Heat;

namespace MotoSquid.UI
{
    public class BikeSelect : MonoBehaviour
    {
        [Header("Bike Selection")]
        [SerializeField] List<BikeScriptableObject> bikeList = new();
        [SerializeField] TMP_Text bikeNameText;
        [SerializeField] Image bikePortraitImage;

        [Header("Selector")]
        [SerializeField] HorizontalSelector bikeSelector;

        public int currentIndex = 0;

        public BikeScriptableObject CurrentBike =>
            currentIndex >= 0 && currentIndex < bikeList.Count ? bikeList[currentIndex] : null;

        void Start()
        {
            bikeList.RemoveAll(b => b == null);
            if (bikeSelector == null || bikeList.Count == 0)
            {
                Debug.LogError("[BikeSelect] No selector or no bikes assigned.", this);
                return;
            }

            bikeSelector.items.Clear();

            foreach (var bike in bikeList)
                bikeSelector.CreateNewItem(bike.bikeName);

            bikeSelector.onValueChanged.AddListener(OnSelectorChanged);
            bikeSelector.InitializeSelector();

            // InitializeSelector lands on defaultIndex, not zero, so read back what it chose
            OnSelectorChanged(bikeSelector.index);
        }

        void DisplayBike(int index)
        {
            BikeScriptableObject bike = bikeList[index];

            if (bikeNameText != null) bikeNameText.text = bike.bikeName;

            if (bikePortraitImage != null)
            {
                bikePortraitImage.sprite  = bike.bikeSprite;
                bikePortraitImage.enabled = bike.bikeSprite != null;
            }
        }

        void OnSelectorChanged(int index)
        {
            if (index < 0 || index >= bikeList.Count) return;
            currentIndex = index;
            DisplayBike(index);
        }
    }
}
