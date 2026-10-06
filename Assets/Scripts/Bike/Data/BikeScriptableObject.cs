using UnityEngine;

namespace MotoSquid.Bike
{
    [CreateAssetMenu(fileName = "New Bike", menuName = "Selector/Bike")]
    public class BikeScriptableObject : ScriptableObject
    {
        public string bikeName;
        public Sprite bikeSprite;

        [Header("Racer prefabs the race scene spawns for this bike")]
        public GameObject playerPrefab;
        public GameObject aiPrefab;
    }
}
