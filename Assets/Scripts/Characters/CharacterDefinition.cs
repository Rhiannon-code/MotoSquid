using MotoSquid.Audio;
using MotoSquid.Combat;
using UnityEngine;

namespace MotoSquid.Characters
{
    public enum RidingStyle { SuperSport, Upright }
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "MotoSquid/Character Definition")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string characterName = "Voodoo";
        [TextArea] public string description;
        public Sprite portrait;

        [Header("Source art")]
        public GameObject riderPrefab;      // Humanoid character
        public GameObject bikePrefab;       // Bike art carrying the anchors listed above
        public GameObject ragdollPrefab;    // Spawned in this character's place on a crash
        public RidingStyle style = RidingStyle.SuperSport;

        [Header("Loadout")]
        public MeleeWeapon defaultWeapon;

        [Header("Audio")]
        public CharacterVoice voice;
        public CharacterScore score;

        [Header("Build sources")]
        public GameObject playerDonor;
        public GameObject aiDonor;
        public string outputFolder = "Assets/Prefabs/Characters";

        [Header("Build output (filled in by CharacterBuilder)")]
        public GameObject builtPlayerPrefab;
        public GameObject builtAIPrefab;
        public GameObject previewPrefab;
        public GameObject Preview => previewPrefab != null ? previewPrefab : builtPlayerPrefab;
        public string PlayerPrefabName => $"{SafeName}_Player";
        public string AIPrefabName     => $"{SafeName}_AI";

        string SafeName => string.IsNullOrWhiteSpace(characterName) ? name : characterName.Replace(' ', '_');
    }
}
