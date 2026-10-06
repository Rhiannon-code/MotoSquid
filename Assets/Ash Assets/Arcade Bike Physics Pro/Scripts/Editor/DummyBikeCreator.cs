using UnityEngine;
using UnityEditor;

namespace ArcadeBP_Pro
{
    public class DummyBikeCreator : EditorWindow
    {
        private ArcadeBikeControllerPro bikeController;

        [MenuItem("Tools/Ash Tools/Arcade Bike Physics Pro/Create Dummy Bike")]
        public static void ShowWindow()
        {
            GetWindow<DummyBikeCreator>("Create Dummy Bike");
        }

        private void OnGUI()
        {
            GUILayout.Label("Dummy Bike Creator", EditorStyles.boldLabel);


            bikeController = (ArcadeBikeControllerPro)EditorGUILayout.ObjectField("Arcade Bike Controller", bikeController, typeof(ArcadeBikeControllerPro), true);


            if (GUILayout.Button("Create Dummy Bike"))
            {
                CreateDummyBike();
            }
        }

        private void CreateDummyBike()
        {
            Transform dummybike = Instantiate(bikeController.bikeReferences.BikeModel.gameObject, bikeController.transform.position + new Vector3(3, 0, 0), bikeController.transform.rotation).transform;

            dummybike.name = "DummyBike_" + bikeController.bikeReferences.BodyMesh.GetChild(0).name;

            if (dummybike.GetComponentInChildren<BikerAnimationController>() != null)
            {
                DestroyImmediate(dummybike.GetComponentInChildren<BikerAnimationController>().gameObject);
            }

            if (dummybike.GetComponentInChildren<BikerAnimationTargets>() != null)
            {
                DestroyImmediate(dummybike.GetComponentInChildren<BikerAnimationTargets>().gameObject);
            }


            var rb = dummybike.gameObject.AddComponent<Rigidbody>();

            rb.mass = bikeController.bikeReferences.BikeRb.mass;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Transform frontWheel = dummybike.Find("Bike Steering Parent").Find("Bike Steering").Find("Front Wheel Parent").Find("Front Wheel");
            Transform rearWheel = dummybike.Find("Rear Wheel Parent").Find("Rear Wheel");
            frontWheel.rotation = Quaternion.identity;
            rearWheel.rotation = Quaternion.identity;



            SphereCollider sphereCollider_f = frontWheel.gameObject.AddComponent<SphereCollider>();
            frontWheel.GetComponentInChildren<MeshRenderer>().transform.rotation = Quaternion.identity;
            sphereCollider_f.radius = frontWheel.GetComponentInChildren<MeshRenderer>().bounds.extents.y;

            SphereCollider sphereCollider_r = rearWheel.gameObject.AddComponent<SphereCollider>();
            rearWheel.GetComponentInChildren<MeshRenderer>().transform.rotation = Quaternion.identity;
            sphereCollider_r.radius = rearWheel.GetComponentInChildren<MeshRenderer>().bounds.extents.y;

            frontWheel.localRotation = Quaternion.identity;
            rearWheel.localRotation = Quaternion.identity;

        }
    }

}
