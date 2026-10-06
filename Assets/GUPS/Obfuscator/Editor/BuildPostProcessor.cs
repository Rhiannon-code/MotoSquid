#if UNITY_EDITOR

// System
using System;
using System.Linq;

// Unity
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace GUPS.Obfuscator
{
    public class BuildPostProcessor : IFilterBuildAssemblies, IPostBuildPlayerScriptDLLs, IUnityLinkerProcessor,
        IPreprocessBuildWithReport, IPostprocessBuildWithReport
#if UNITY_ANDROID
    , UnityEditor.Android.IPostGenerateGradleAndroidProject
#endif
    {
        /// <summary>
        /// Defines if an obfuscation process took place.
        /// </summary>
        private static bool hasObfuscated = false;

        /// <summary>
        /// All obfuscator callbacks run as late as possible (int.MaxValue).
        /// If you have tools requiring to run after the obfuscation process, you can set a lower callbackOrder.
        /// </summary>
        public int callbackOrder
        {
            get { return int.MaxValue; }
        }

        /// <summary>
        /// Prepares the obfuscator editor settings.
        /// </summary>
        private static GUPS.Obfuscator.Editor.Settings.Unity.Editor.EditorSettings PrepareEditorSettings()
        {
            GUPS.Obfuscator.Editor.Settings.Unity.Editor.EditorSettings var_EditorSettings = new Editor.Settings.Unity.Editor.EditorSettings();

            return var_EditorSettings;
        }

        /// <summary>
        /// Prepares the obfuscator build settings.
        /// </summary>
        private static GUPS.Obfuscator.Editor.Settings.Unity.Build.BuildSettings PrepareBuildSettings(BuildReport _Report)
        {
            GUPS.Obfuscator.Editor.Settings.Unity.Build.BuildSettings var_BuildSettings = new Editor.Settings.Unity.Build.BuildSettings();
            var_BuildSettings.IsDevelopmentBuild = UnityEditor.EditorUserBuildSettings.development;
            var_BuildSettings.BuildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
            var_BuildSettings.BuildTargetGroup = UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup;
            var_BuildSettings.UnityBuildReport = _Report;

#if UNITY_2021_2_OR_NEWER
            var var_NamedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);

            var_BuildSettings.IsIL2CPPBuild = PlayerSettings.GetScriptingBackend(var_NamedBuildTarget) == ScriptingImplementation.IL2CPP;
#else
            var_BuildSettings.IsIL2CPPBuild = PlayerSettings.GetScriptingBackend(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup) == ScriptingImplementation.IL2CPP;
#endif

            if (_Report == null)
            {
                var_BuildSettings.Compression = (GUPS.Editor.Settings.Unity.Build.CompressionType)typeof(UnityEditor.EditorUserBuildSettings)
                    .GetMethod("GetCompressionType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup });
            }
            else
            {
                var_BuildSettings.Compression = _Report.summary.options.HasFlag(BuildOptions.CompressWithLz4) ? GUPS.Editor.Settings.Unity.Build.CompressionType.Lz4 :
                               _Report.summary.options.HasFlag(BuildOptions.CompressWithLz4HC) ? GUPS.Editor.Settings.Unity.Build.CompressionType.Lz4HC :
                               GUPS.Editor.Settings.Unity.Build.CompressionType.None;
            }

            var_BuildSettings.CreateXcodeProject = UnityEditor.EditorUserBuildSettings.GetPlatformSettings("OSXUniversal", "CreateXcodeProject").Equals("true");
            var_BuildSettings.CreateVisualStudioSolution = UnityEditor.EditorUserBuildSettings.GetPlatformSettings("Standalone", "CreateSolution").Equals("true");
            var_BuildSettings.ExportAsGoogleAndroidProject = UnityEditor.EditorUserBuildSettings.exportAsGoogleAndroidProject;

            var_BuildSettings.AssemblyPathList = GetReportAssemblyPathList(_Report);

            return var_BuildSettings;
        }

        /// <summary>
        /// Collects the file paths of all assemblies the build report knows about.
        /// </summary>
        private static System.Collections.Generic.List<String> GetReportAssemblyPathList(BuildReport _Report)
        {
            System.Collections.Generic.List<String> var_AssemblyPathList = new System.Collections.Generic.List<String>();

            if (_Report == null)
            {
                return var_AssemblyPathList;
            }

#if UNITY_2022_1_OR_NEWER
            BuildFile[] var_FileArray = _Report.GetFiles();
#else
            BuildFile[] var_FileArray = _Report.files;
#endif

            if (var_FileArray == null)
            {
                return var_AssemblyPathList;
            }

            System.Collections.Generic.HashSet<String> var_AssemblyPathHashSet = new System.Collections.Generic.HashSet<String>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < var_FileArray.Length; i++)
            {
                String var_Path = var_FileArray[i].path;

                if (String.IsNullOrEmpty(var_Path))
                {
                    continue;
                }

                if (!var_Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var_Path = System.IO.Path.GetFullPath(var_Path);
                }
                catch
                {
                    continue;
                }

                if (var_AssemblyPathHashSet.Add(var_Path))
                {
                    var_AssemblyPathList.Add(var_Path);
                }
            }

            return var_AssemblyPathList;
        }

        /// <summary>
        /// Runs the obfuscator pre-build process. Used to analyze asset compatibility and other build-time information.
        /// </summary>
        private void PreprocessBuild(BuildReport _Report)
        {
            // Settings
            GUPS.Obfuscator.Editor.Settings.Unity.Editor.EditorSettings var_EditorSettings = PrepareEditorSettings();
            GUPS.Obfuscator.Editor.Settings.Unity.Build.BuildSettings var_BuildSettings = PrepareBuildSettings(_Report);

            // Init
            GUPS.Obfuscator.Editor.Obfuscator.Init();
            hasObfuscated = false;

            try
            {
                // Pre Build
                GUPS.Obfuscator.Editor.Obfuscator.Singleton.PreBuild(var_EditorSettings, var_BuildSettings);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[OPS] Error: " + e.ToString());
            }
        }

        /// <summary>
        /// Callback of the Unity Editor pre-process build.
        /// </summary>
        public void OnPreprocessBuild(BuildReport _Report)
        {
            this.PreprocessBuild(_Report);
        }

        /// <summary>
        /// Filters the assemblies included in the build (Not used yet).
        /// </summary>
        public string[] OnFilterAssemblies(BuildOptions _BuildOptions, string[] _Assemblies)
        {
			// Return all assemblies - Filtered with build.
			return _Assemblies;
        }

        /// <summary>
        /// Runs the obfuscator post-build process. Used to obfuscate the assemblies included in the build.
        /// </summary>
        public void OnPostBuildPlayerScriptDLLs(BuildReport _Report)
        {
            if (!hasObfuscated)
            {
                if (BuildPipeline.isBuildingPlayer && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    try
                    {
                        UnityEditor.EditorApplication.LockReloadAssemblies();

                        // Settings
                        GUPS.Obfuscator.Editor.Settings.Unity.Editor.EditorSettings var_EditorSettings = PrepareEditorSettings();
                        GUPS.Obfuscator.Editor.Settings.Unity.Build.BuildSettings var_BuildSettings = PrepareBuildSettings(_Report);

                        // Obfuscate
                        GUPS.Obfuscator.Editor.Obfuscator.Singleton.PostAssemblyBuild(var_EditorSettings, var_BuildSettings);
                        hasObfuscated = true;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError("[OPS] Error: " + e.ToString());
                    }
                    finally
                    {
                        UnityEditor.EditorApplication.UnlockReloadAssemblies();
                    }
                }
            }
        }

        /// <summary>
        /// Generates an additional link.xml file for the UnityLinker.
        /// </summary>
        public string GenerateAdditionalLinkXmlFile(BuildReport _Report, UnityLinkerBuildPipelineData _Data)
        {
            if (hasObfuscated)
            {
                try
                {
                    // Settings
                    GUPS.Obfuscator.Editor.Settings.Unity.Editor.EditorSettings var_EditorSettings = PrepareEditorSettings();
                    GUPS.Obfuscator.Editor.Settings.Unity.Build.BuildSettings var_BuildSettings = PrepareBuildSettings(_Report);

                    // Post Build
                    GUPS.Obfuscator.Editor.Obfuscator.Singleton.PostAssetsBuild(var_EditorSettings, var_BuildSettings);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[OPS] Error: " + e.ToString());
                }

                try
                {
                    // Pass the additional link.xml generated during obfuscation to the UnityLinker. It preserves
                    // the obfuscated names of types referenced by the project link.xml files.
                    string var_LinkXmlPath = GUPS.Obfuscator.Editor.Project.PostAssemblyBuild.Pipeline.Component.Compatibility.LinkXmlComponent.GetGeneratedLinkXmlFilePath();

                    if (System.IO.File.Exists(var_LinkXmlPath))
                    {
                        return System.IO.Path.GetFullPath(var_LinkXmlPath);
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[OPS] Error: " + e.ToString());
                }
            }

            return null;
        }

#if UNITY_2021_2_OR_NEWER
#else
        public void OnBeforeRun(BuildReport report, UnityLinkerBuildPipelineData data)
        {
        }

        public void OnAfterRun(BuildReport report, UnityLinkerBuildPipelineData data)
        {
        }
#endif

#if UNITY_ANDROID
        /// <summary>
        /// Runs the obfuscator post-gradle build process.
        /// </summary>
        public void OnPostGenerateGradleAndroidProject(string _Path)
        {
            try
            {
                // Settings
                GUPS.Obfuscator.Editor.Settings.Unity.Editor.EditorSettings var_EditorSettings = PrepareEditorSettings();
                GUPS.Obfuscator.Editor.Settings.Unity.Build.BuildSettings var_BuildSettings = PrepareBuildSettings(null);

                // The callback path points at the emitted unityLibrary module, so use parent directory.
                String var_GradleRoot = _Path;
                try
                {
                    System.IO.DirectoryInfo var_Parent = System.IO.Directory.GetParent(_Path);
                    if (var_Parent != null)
                    {
                        var_GradleRoot = var_Parent.FullName;
                    }
                }
                catch
                {
                    var_GradleRoot = _Path;
                }
                var_BuildSettings.GradleProjectPath = var_GradleRoot;

                // Post Gradle Build
                GUPS.Obfuscator.Editor.Obfuscator.Singleton.PostGradleBuild(var_EditorSettings, var_BuildSettings);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[OPS] Error: " + e.ToString());
            }
        }
#endif

        /// <summary>
        /// Runs the obfuscator post-build process. Used for asset and data patching.
        /// </summary>
        private void PostprocessBuild(BuildReport _Report)
        {
            if (hasObfuscated)
            {
                try
                {
                    // Settings
                    GUPS.Obfuscator.Editor.Settings.Unity.Editor.EditorSettings var_EditorSettings = PrepareEditorSettings();
                    GUPS.Obfuscator.Editor.Settings.Unity.Build.BuildSettings var_BuildSettings = PrepareBuildSettings(_Report);

                    // Post Build
                    GUPS.Obfuscator.Editor.Obfuscator.Singleton.PostBuild(var_EditorSettings, var_BuildSettings);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[OPS] Error: " + e.ToString());
                }
            }
        }

        /// <summary>
        /// Callback of the Unity Editor post-process build.
        /// </summary>
        public void OnPostprocessBuild(BuildReport _Report)
        {
            this.PostprocessBuild(_Report);
        }
    }
}
#endif