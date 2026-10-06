using UnityEditor;
using UnityEngine;

namespace ArcadeBP_Pro
{
    public class ArcadeBikeCreatorPro : EditorWindow
    {
        private GameObject preset;
        private Transform BikeParent;
        private Transform Handle;
        private Transform frontWheel;
        private Transform backWheel;
        private MeshRenderer bodyMesh;
        private MeshRenderer frontWheelMesh;
        private MeshRenderer backWheelMesh;
        private GameObject NewBike;
        private GUIStyle titleStyle;

        [MenuItem("Tools/Ash Tools/Arcade Bike Physics Pro/Bike Creator")]
        private static void OpenWindow()
        {
            ArcadeBikeCreatorPro window = (ArcadeBikeCreatorPro)GetWindow(typeof(ArcadeBikeCreatorPro));
            window.minSize = new Vector2(400, 350);
            window.Show();
        }

        [MenuItem("Tools/Ash Tools/Arcade Bike Physics Pro/Online Documentation")]
        private static void OpenDocumentationLink()
        {
            Application.OpenURL("https://soft-pilot-e91.notion.site/Documentation-40c4b6d5135b4fa080694e38d8a1b1d3");
        }

        private void OnEnable()
        {
            titleStyle = null;
        }

        private void OnGUI()
        {
            titleStyle ??= CreateTitleStyle();

            GUILayout.Label("Arcade Bike Creator Pro", titleStyle);
            preset = EditorGUILayout.ObjectField("Bike preset", preset, typeof(GameObject), true) as GameObject;
            ValidateBikePreset(preset);

            GUILayout.Label("Your Bike", titleStyle);
            BikeParent = EditorGUILayout.ObjectField("Bike Parent", BikeParent, typeof(Transform), true) as Transform;
            ValidatePrefabStatus();
            ValidateBikeParentOrientation();

            Handle = EditorGUILayout.ObjectField("Handle", Handle, typeof(Transform), true) as Transform;
            ValidateHandleOrientation();

            frontWheel = EditorGUILayout.ObjectField("Wheel Front", frontWheel, typeof(Transform), true) as Transform;
            backWheel = EditorGUILayout.ObjectField("Wheel Back", backWheel, typeof(Transform), true) as Transform;

            bodyMesh = EditorGUILayout.ObjectField("Body Mesh", bodyMesh, typeof(MeshRenderer), true) as MeshRenderer;
            frontWheelMesh = EditorGUILayout.ObjectField("Front Wheel Mesh", frontWheelMesh, typeof(MeshRenderer), true) as MeshRenderer;
            backWheelMesh = EditorGUILayout.ObjectField("Back Wheel Mesh", backWheelMesh, typeof(MeshRenderer), true) as MeshRenderer;

            bool canCreate = ValidateCreateInputs(false);
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("Create Bike"))
                {
                    CreateBike();
                }
            }

            if (!canCreate)
            {
                EditorGUILayout.HelpBox("Assign a valid preset, source bike hierarchy, handle, wheels, and mesh renderers before creating the bike.", MessageType.Info);
            }
        }

        private static GUIStyle CreateTitleStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.green }
            };
        }

        private bool ValidateCreateInputs(bool logErrors)
        {
            bool valid = preset && BikeParent && Handle && frontWheel && backWheel && bodyMesh && frontWheelMesh && backWheelMesh
                         && TryGetPresetController(out ArcadeBikeControllerPro controller)
                         && ValidateBikeReferenceShape(controller)
                         && !PrefabUtility.IsPartOfPrefabInstance(BikeParent.gameObject)
                         && IsBikeParentOrientationValid()
                         && IsHandleOrientationValid();

            if (!valid && logErrors)
            {
                Debug.LogError("Arcade Bike Creator Pro has missing or invalid setup references.");
            }

            return valid;
        }

        private void CreateBike()
        {
            if (!ValidateCreateInputs(true))
            {
                return;
            }

            NewBike = CreateBikeInstance();
            if (!NewBike || !NewBike.TryGetComponent(out ArcadeBikeControllerPro bikeController))
            {
                Debug.LogError("Failed to instantiate a valid ArcadeBikeControllerPro preset.");
                return;
            }

            ArcadeBikeControllerPro.BikeReferences bikeRefs = bikeController.bikeReferences;
            ArcadeBikeControllerPro.BikeGeometry bikeGeometry = bikeController.bikeGeometry;

            Undo.RecordObject(NewBike.transform, "Create Arcade Bike Pro");
            NewBike.name = "ABP_Pro_" + BikeParent.name;
            NewBike.transform.SetPositionAndRotation(GetBikeRootPosition(), BikeParent.rotation);

            DestroyFirstChild(bikeRefs.BodyMesh);

            Undo.SetTransformParent(BikeParent, bikeRefs.BodyMesh, "Create Arcade Bike Pro");
            BikeParent.SetSiblingIndex(0);

            if (bikeRefs.BikeSteering)
            {
                Undo.RecordObjects(new Object[] { bikeRefs.BikeSteeringParent, bikeRefs.SteeringMeshes }, "Create Arcade Bike Pro");
                bikeRefs.BikeSteeringParent.SetPositionAndRotation(Handle.position, Handle.rotation);
                DestroyFirstChild(bikeRefs.SteeringMeshes);
                Undo.SetTransformParent(Handle, bikeRefs.SteeringMeshes, "Create Arcade Bike Pro");
            }

            DestroyFirstChild(bikeRefs.FrontWheel);
            Undo.RecordObject(bikeRefs.FrontWheelParent, "Create Arcade Bike Pro");
            bikeRefs.FrontWheelParent.position = frontWheel.position;
            bikeRefs.FrontWheelParent.localRotation = Quaternion.identity;
            Undo.SetTransformParent(frontWheel, bikeRefs.FrontWheel, "Create Arcade Bike Pro");

            DestroyFirstChild(bikeRefs.RearWheel);
            Undo.RecordObject(bikeRefs.RearWheelParent, "Create Arcade Bike Pro");
            bikeRefs.RearWheelParent.position = backWheel.position;
            Undo.SetTransformParent(backWheel, bikeRefs.RearWheel, "Create Arcade Bike Pro");

            Undo.RecordObject(bikeController, "Configure Arcade Bike Pro");
            ApplyWheelGeometry(bikeRefs, bikeGeometry);
            ApplyBodyCollider(bikeRefs.collider);
            DisableMotionBlurForAllChildren(NewBike);

            EditorUtility.SetDirty(bikeController);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bikeController);
            Selection.activeGameObject = NewBike;
        }

        private GameObject CreateBikeInstance()
        {
            GameObject instance = null;
            if (PrefabUtility.IsPartOfPrefabAsset(preset))
            {
                instance = PrefabUtility.InstantiatePrefab(preset) as GameObject;
            }

            if (!instance)
            {
                instance = Instantiate(preset);
            }

            if (instance)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Create Arcade Bike Pro");
            }

            return instance;
        }

        private Vector3 GetBikeRootPosition()
        {
            return new Vector3(BikeParent.position.x, backWheel.position.y - backWheelMesh.bounds.extents.y, (frontWheel.position.z + backWheel.position.z) / 2f);
        }

        private void ApplyWheelGeometry(ArcadeBikeControllerPro.BikeReferences bikeRefs, ArcadeBikeControllerPro.BikeGeometry bikeGeometry)
        {
            Quaternion frontRotation = frontWheelMesh.transform.rotation;
            Quaternion rearRotation = backWheelMesh.transform.rotation;

            try
            {
                frontWheelMesh.transform.rotation = Quaternion.identity;
                backWheelMesh.transform.rotation = Quaternion.identity;

                bikeGeometry.FrontWheelRadius = frontWheelMesh.bounds.extents.y;
                bikeGeometry.RearWheelRadius = backWheelMesh.bounds.extents.y;
                bikeGeometry.FrontWheelWidth = frontWheelMesh.bounds.extents.x;
                bikeGeometry.RearWheelWidth = backWheelMesh.bounds.extents.x;
                bikeGeometry.FrontWheelAngle = Vector3.Angle(bikeRefs.FrontWheelParent.up, NewBike.transform.up);
                bikeGeometry.RearWheelAngle = Vector3.Angle(bikeRefs.RearWheelParent.up, NewBike.transform.up);
            }
            finally
            {
                frontWheelMesh.transform.rotation = frontRotation;
                backWheelMesh.transform.rotation = rearRotation;
            }
        }

        private void ApplyBodyCollider(CapsuleCollider bikeCollider)
        {
            Undo.RecordObject(bikeCollider, "Configure Arcade Bike Pro Collider");
            bikeCollider.transform.position = bodyMesh.bounds.center;
            bikeCollider.transform.localPosition = new Vector3(0f, bikeCollider.transform.localPosition.y, bikeCollider.transform.localPosition.z);
            bikeCollider.center = Vector3.zero;
            bikeCollider.height = bodyMesh.bounds.extents.z * 2f;
            bikeCollider.radius = bodyMesh.bounds.extents.x;
            EditorUtility.SetDirty(bikeCollider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bikeCollider);
        }

        private static void DestroyFirstChild(Transform parent)
        {
            if (parent && parent.childCount > 0)
            {
                Undo.DestroyObjectImmediate(parent.GetChild(0).gameObject);
            }
        }

        private bool TryGetPresetController(out ArcadeBikeControllerPro controller)
        {
            controller = preset ? preset.GetComponent<ArcadeBikeControllerPro>() : null;
            return controller;
        }

        private static bool ValidateBikeReferenceShape(ArcadeBikeControllerPro controller)
        {
            if (!controller || controller.bikeReferences == null || controller.bikeGeometry == null)
            {
                return false;
            }

            ArcadeBikeControllerPro.BikeReferences refs = controller.bikeReferences;
            return refs.BodyMesh
                   && refs.FrontWheelParent
                   && refs.FrontWheel
                   && refs.RearWheelParent
                   && refs.RearWheel
                   && refs.collider
                   && (!refs.BikeSteering || (refs.BikeSteeringParent && refs.SteeringMeshes));
        }

        private void ValidateBikePreset(GameObject bikePreset)
        {
            if (!bikePreset)
            {
                return;
            }

            if (!bikePreset.TryGetComponent(out ArcadeBikeControllerPro controller))
            {
                EditorGUILayout.HelpBox("Bike preset does not have an ArcadeBikeControllerPro script attached. Please assign a valid bike preset.", MessageType.Error);
                return;
            }

            if (!ValidateBikeReferenceShape(controller))
            {
                EditorGUILayout.HelpBox("Bike preset is missing required bike references. Please assign a complete Arcade Bike Physics Pro preset.", MessageType.Error);
            }
        }

        private void ValidateBikeParentOrientation()
        {
            if (BikeParent && frontWheel && backWheel && !IsBikeParentOrientationValid())
            {
                EditorGUILayout.HelpBox("Bike Parent orientation is incorrect. Please fix it before creating the bike. Refer to the bike model that comes with this pack", MessageType.Error);
            }
        }

        private bool IsBikeParentOrientationValid()
        {
            if (!BikeParent || !frontWheel || !backWheel)
            {
                return false;
            }

            Vector3 wheelDirection = (frontWheel.position - backWheel.position).normalized;
            return Vector3.Dot(BikeParent.forward, wheelDirection) >= 0 && Vector3.Dot(BikeParent.right, -Vector3.Cross(wheelDirection, Vector3.up)) >= 0;
        }

        private void ValidateHandleOrientation()
        {
            if (Handle && frontWheel && backWheel && BikeParent && !IsHandleOrientationValid())
            {
                EditorGUILayout.HelpBox("Handle orientation is incorrect. Please fix it before creating the bike. Refer to the bike model that comes with this pack", MessageType.Error);
            }
        }

        private bool IsHandleOrientationValid()
        {
            if (!Handle || !frontWheel || !backWheel || !BikeParent)
            {
                return false;
            }

            Vector3 wheelDirection = (frontWheel.position - backWheel.position).normalized;
            return Vector3.Dot(Handle.right, -Vector3.Cross(wheelDirection, BikeParent.up)) >= 0 && Vector3.Dot(Handle.forward, wheelDirection) >= 0;
        }

        private void ValidatePrefabStatus()
        {
            if (BikeParent && PrefabUtility.IsPartOfPrefabInstance(BikeParent.gameObject))
            {
                EditorGUILayout.HelpBox("Bike Parent is a prefab instance. Please unpack the prefab completely.", MessageType.Error);
            }
        }

        private static void DisableMotionBlurForAllChildren(GameObject parent)
        {
            MeshRenderer[] meshRenderers = parent.GetComponentsInChildren<MeshRenderer>();

            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                Undo.RecordObject(meshRenderer, "Disable Bike Motion Vectors");
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                EditorUtility.SetDirty(meshRenderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(meshRenderer);
            }
        }
    }
}
