using MotoSquid.Characters;
using System;
using UnityEngine;

namespace MotoSquid.Bike
{
    [Serializable]
    public class BikeEntry
    {
        public CharacterDefinition character;
        public string displayName;
        [TextArea] public string description;
        public GameObject previewPrefab;

        public string Identity =>
            character != null && !string.IsNullOrEmpty(character.characterName)
                ? character.characterName
                : displayName;

        public string Label =>
            !string.IsNullOrEmpty(displayName) ? displayName : Identity;
    }
}
