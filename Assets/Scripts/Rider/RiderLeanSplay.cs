using UnityEngine;

namespace MotoSquid.Rider
{

    [DefaultExecutionOrder(50)]
    public class RiderLeanSplay : MonoBehaviour
    {
        public BikeAnimationController controller;
        public BikerRigReferences rig;

        [Header("Elbows")]
        public Vector3 elbowOut = new Vector3(-0.12f, 0.04f, 0f);
        public bool probeElbows;
        public float elbowDirectional = 1f;

        [Header("Knees")]
        public bool driveKnees = true;
        public Vector3 kneeOut = new Vector3(0.25f, 0f, 0f);
        public float kneeDirectional = 1f;

        [Header("Shared")]
        [Range(0f, 1.5f)] public float hipFollow = 1f;
        public bool invertSide;
        public float speed = 20f;

        Vector3 leftHandRest, rightHandRest, leftLegRest, rightLegRest;
        bool ready;

        public bool Ready { get { return ready; } }

        void Start()
        {
            if (rig == null || controller == null) return;
            if (rig.leftHandHint != null) leftHandRest = rig.leftHandHint.localPosition;
            if (rig.rightHandHint != null) rightHandRest = rig.rightHandHint.localPosition;
            if (rig.leftLegHint != null) leftLegRest = rig.leftLegHint.localPosition;
            if (rig.rightLegHint != null) rightLegRest = rig.rightLegHint.localPosition;
            ready = true;
        }

        void OnDisable()
        {
            if (controller != null) controller.externalKneeControl = false;
        }

        void Update()
        {
            if (!ready) return;
            controller.externalKneeControl = driveKnees;
            var bike = controller.bikeControllerRhiannon;
            if (bike == null) return;

            float lean = Mathf.Clamp(controller.currentLeanAngle /
                                     Mathf.Max(1f, bike.bikeSettings.maxLeanAngle), -1f, 1f);
            if (invertSide) lean = -lean;
            float both = Mathf.Abs(lean);

            float l = probeElbows ? 1f : Mathf.Lerp(both, lean > 0f ? both : 0f, elbowDirectional);
            float r = probeElbows ? 1f : Mathf.Lerp(both, lean < 0f ? both : 0f, elbowDirectional);
            Splay(rig.leftHandHint, leftHandRest, Mirror(elbowOut), l);
            Splay(rig.rightHandHint, rightHandRest, elbowOut, r);

            if (!driveKnees) return;
            l = Mathf.Lerp(both, lean > 0f ? both : 0f, kneeDirectional);
            r = Mathf.Lerp(both, lean < 0f ? both : 0f, kneeDirectional);

            // Follow the hip across so the knee the splay does not reach keeps a valid pole vector.
            // The hip targets are pitched about X, so their local X is still the rider's X
            var drift = new Vector3(controller.LeanHipOffset.x * hipFollow, 0f, 0f);
            Splay(rig.leftLegHint, leftLegRest + drift, Mirror(kneeOut), l);
            Splay(rig.rightLegHint, rightLegRest + drift, kneeOut, r);
        }

        static Vector3 Mirror(Vector3 v) { return new Vector3(-v.x, v.y, v.z); }

        void Splay(Transform hint, Vector3 rest, Vector3 full, float amount)
        {
            if (hint == null) return;
            hint.localPosition = Vector3.Lerp(hint.localPosition, rest + full * amount,
                                              Time.deltaTime * speed);
        }
    }
}
