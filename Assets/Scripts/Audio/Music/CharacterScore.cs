using UnityEngine;

namespace MotoSquid.Audio
{
    [CreateAssetMenu(fileName = "CharacterScore", menuName = "MotoSquid/Character Score")]
    public class CharacterScore : ScriptableObject
    {
        // Must match GameSession SelectedCharacterName (case insensitive), e.g. "Mace".
        public string characterName = "Mace";

        [Header("Loop Stems")]
        public AudioClip ambience;
        public AudioClip baseStem;   // 'base' is a C# keyword
        public AudioClip percussion;
        public AudioClip intensity;
        public AudioClip tension;
        public AudioClip rhythmGuitar;   // Slick only sixth stem, leave null for other characters

        [Header("One shot Stingers (optional)")]
        public AudioClip countdownStinger;
        public AudioClip raceStartStinger;
        public AudioClip combatHitStinger;
        public AudioClip winStinger;
        public AudioClip loseStinger;
    }
}
