using UnityEngine;

namespace MotoSquid.Characters
{
    [CreateAssetMenu(fileName = "CharacterLibrary", menuName = "MotoSquid/Character Library")]
    public class CharacterLibrary : ScriptableObject
    {
        public CharacterDefinition[] characters = new CharacterDefinition[0];

        public int Count => characters != null ? characters.Length : 0;

        public CharacterDefinition Get(int index) =>
            characters != null && index >= 0 && index < characters.Length ? characters[index] : null;

        public CharacterDefinition Find(string characterName)
        {
            if (characters == null || string.IsNullOrEmpty(characterName)) return null;
            foreach (var c in characters)
                if (c != null && string.Equals(c.characterName, characterName,
                                               System.StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
        }

        public int IndexOf(string characterName)
        {
            if (characters == null || string.IsNullOrEmpty(characterName)) return -1;
            for (int i = 0; i < characters.Length; i++)
                if (characters[i] != null && string.Equals(characters[i].characterName, characterName,
                                                           System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        public GameObject GetRacerPrefab(string characterName, bool isPlayer)
        {
            var def = Find(characterName);
            if (def == null) return null;
            return isPlayer ? def.builtPlayerPrefab : def.builtAIPrefab;
        }
    }
}
