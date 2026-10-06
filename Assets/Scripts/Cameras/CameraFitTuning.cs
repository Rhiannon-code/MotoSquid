using UnityEngine;

namespace MotoSquid.Cameras
{
    [CreateAssetMenu(fileName = "CameraFitTuning", menuName = "Rig Lab/Camera Fit Tuning")]
    public class CameraFitTuning : ScriptableObject
    {
        [Header("Third person")]
        public float chaseGap = 0.60f;
        public float aimAhead = 0.27f;
        public float aimHeight = 0.925f;
        public float lookBehindDistance = 5.00f;
        public float lookBehindHeight   = 1.50f;

        [Header("First person: Forward")]
        public float eyeAboveScreen = 0.10f;
        public float eyeDropNoScreen = 0.25f;
        public float gazeDistance = 1.79f;
        public float gazeDrop     = 0.50f;

        [Header("First person: Rear")]
        public float rearCamAbove   = 0.55f;
        public float rearCamForward = 0.25f;
        public float rearClearance = 0.12f;
        public float rearAimDistance = 2.50f;
        public float rearAimDrop     = 0.90f;
    }
}
