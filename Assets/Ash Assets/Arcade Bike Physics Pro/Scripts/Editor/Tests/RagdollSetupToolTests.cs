using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcadeBP_Pro.Tests
{
    public sealed class RagdollSetupToolTests
    {
        private const string SourcePrefabPath = "Assets/Models/biker/source/biker_mesh/Biker_A-pose Ragdoll.prefab";
        private const string TargetPrefabPath = "Assets/Ash Assets/Arcade Bike Physics Pro/Prefabs/Ragdolls/Character Ragdolls/Ragdoll_Dummy_Mannequin.prefab";

        [Test]
        public void TryBuildBindingsAcceptsProjectPrefabAsSource()
        {
            GameObject sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            Assert.That(sourceRoot, Is.Not.Null);

            RagdollReference source = sourceRoot.GetComponent<RagdollReference>();
            Assert.That(source, Is.Not.Null);

            GameObject targetRoot = PrefabUtility.LoadPrefabContents(TargetPrefabPath);
            try
            {
                Animator target = targetRoot.GetComponent<Animator>();
                Assert.That(target, Is.Not.Null);

                MethodInfo method = typeof(RagdollSetupTool).GetMethod("TryBuildBindings", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);

                object[] arguments = { source, target, null, null };
                bool success = (bool)method.Invoke(null, arguments);

                Assert.That(success, Is.True, arguments[3] as string);
                Assert.That(((ICollection)arguments[2]).Count, Is.EqualTo(15));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(targetRoot);
            }
        }
    }
}
