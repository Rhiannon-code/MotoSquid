using UnityEngine;

namespace MotoSquid.Roads
{
    // The corridor scene's tuning: edit it, then run Update Roads In Open Scene.
    [CreateAssetMenu(menuName = "MotoSquid/Roads/Corridor Layout", fileName = "Corridor Layout")]
    public class CorridorLayoutAsset : ScriptableObject
    {
        public CorridorSettings settings = new CorridorSettings();
    }
}
