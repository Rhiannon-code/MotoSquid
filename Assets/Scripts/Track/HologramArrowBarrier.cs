using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MotoSquid.Track
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class HologramArrowBarrier : MonoBehaviour
    {
        public Color gizmoColour = new Color(0f, 1f, 1f, 0.8f);

        private void Awake() => SyncCollider();

        private void SyncCollider()
        {
            var mf  = GetComponent<MeshFilter>();
            var col = GetComponent<MeshCollider>();
            if (mf.sharedMesh == null) return;

            col.sharedMesh = mf.sharedMesh;
            col.convex     = false;
            col.isTrigger  = false;
        }

#if UNITY_EDITOR
        private void OnValidate() => SyncCollider();

        private void OnDrawGizmos()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color  = gizmoColour;
            Gizmos.DrawWireMesh(mf.sharedMesh);
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(HologramArrowBarrier))]
    public class HologramArrowBarrierEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var barrier = (HologramArrowBarrier)target;

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("Sync Collider", GUILayout.Height(30)))
            {
                var mf  = barrier.GetComponent<MeshFilter>();
                var col = barrier.GetComponent<MeshCollider>();
                if (mf.sharedMesh != null)
                {
                    col.sharedMesh = null;
                    col.sharedMesh = mf.sharedMesh;
                    col.convex     = false;
                    col.isTrigger  = false;
                    EditorUtility.SetDirty(barrier.gameObject);
                }
                else
                {
                    Debug.LogWarning("[HologramArrowBarrier] No mesh assigned to MeshFilter.", barrier);
                }
            }
            GUI.backgroundColor = Color.white;
        }
    }
#endif
}
