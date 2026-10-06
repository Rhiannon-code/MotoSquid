using MotoSquid.Characters;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Michsky.UI.Heat;

namespace MotoSquid.UI
{
    public class CharacterSelect : MonoBehaviour
    {
        [Header("Character Selection")]
        [SerializeField] List<CharacterDefinition> characterList = new();
        [SerializeField] TMP_Text characterNameText;
        [SerializeField] TMP_Text characterDescriptionText;
        [SerializeField] Image characterPortraitImage;

        [Header("Selector")]
        [SerializeField] HorizontalSelector characterSelector;

        public int currentIndex = 0;

        public CharacterDefinition CurrentCharacter =>
            currentIndex >= 0 && currentIndex < characterList.Count ? characterList[currentIndex] : null;

        void Start()
        {
            characterList.RemoveAll(c => c == null);
            if (characterSelector == null || characterList.Count == 0)
            {
                Debug.LogError("[CharacterSelect] No selector or no characters assigned.", this);
                return;
            }

            characterSelector.items.Clear();

            foreach (var character in characterList)
                characterSelector.CreateNewItem(character.characterName);

            characterSelector.onValueChanged.AddListener(OnSelectorChanged);
            characterSelector.InitializeSelector();

            // InitializeSelector lands on defaultIndex, not zero, so read back what it chose
            OnSelectorChanged(characterSelector.index);
        }

        void DisplayCharacter(int index)
        {
            var character = characterList[index];

            if (characterNameText        != null) characterNameText.text        = character.characterName;
            if (characterDescriptionText != null) characterDescriptionText.text = character.description;

            if (characterPortraitImage != null)
            {
                characterPortraitImage.sprite  = character.portrait;
                characterPortraitImage.enabled = character.portrait != null;
            }
        }

        void OnSelectorChanged(int index)
        {
            if (index < 0 || index >= characterList.Count) return;
            currentIndex = index;
            DisplayCharacter(index);
        }
    }
}
