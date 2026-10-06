using UnityEngine;
using TMPro;

namespace MotoSquid.Race
{
    public class NumberedGate : MonoBehaviour
    {
        [SerializeField] TMP_Text frontText;
        [SerializeField] TMP_Text backText;
        [SerializeField] int number;

        void OnValidate()
        {
            if (frontText) frontText.text = number.ToString();
            if (backText) backText.text = number.ToString();
        }
    }
}
