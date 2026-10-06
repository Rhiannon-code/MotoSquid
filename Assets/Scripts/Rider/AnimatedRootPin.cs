using UnityEngine;

namespace MotoSquid.Rider
{

    [DefaultExecutionOrder(10000)]
    public class AnimatedRootPin : MonoBehaviour
    {
        public bool pinPosition = true;
        public bool pinRotation = false;

        Vector3 _p;
        Quaternion _r;

        void OnEnable()
        {
            _p = transform.localPosition;
            _r = transform.localRotation;
        }

        // LateUpdate runs after the Animator has applied its pose; the high execution order makes it
        // run after other LateUpdate writers so the root is the last thing set
        void LateUpdate()
        {
            if (pinPosition) transform.localPosition = _p;
            if (pinRotation) transform.localRotation = _r;
        }
    }
}
