using UnityEngine;

namespace MotoSquid.Characters
{
    [CreateAssetMenu(fileName = "New Character", menuName = "Selector/Character")]
    public class CharacterScriptableObject : ScriptableObject
    {
        public string characterName;
        public Sprite characterPortrait;
        [TextArea(1, 5)]
        public string characterDescription;
    }
}
