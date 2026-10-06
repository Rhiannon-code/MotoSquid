using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace ArcadeBP_Pro
{
    public class UiButton_ABP_Pro : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool isPressed;
        public UnityEvent onButtonDown;
        public UnityEvent onButtonUp;

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            onButtonDown.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            onButtonUp.Invoke();
        }
    }

}
