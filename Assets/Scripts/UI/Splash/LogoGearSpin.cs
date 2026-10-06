using UnityEngine;

namespace MotoSquid.UI
{
    public class LogoGearSpin : MonoBehaviour
    {
        [Header("Degrees per second, negative reads clockwise on screen")]
        public float degreesPerSecond = -20f;

        void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
