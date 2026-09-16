using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Digi.Prototype.Editor
{
    public static class PrototypeTools
    {
        [MenuItem("Digi/Create Prototype Scene (preserves SampleScene)")]
        public static void CreateScene()
        {
            const string scenePath = "Assets/Prototype/Prototype.unity";
            var config = AssetDatabase.LoadAssetAtPath<PrototypeConfig>("Assets/Prototype/Rules.asset");
            if (config == null) { config = ScriptableObject.CreateInstance<PrototypeConfig>(); AssetDatabase.CreateAsset(config, "Assets/Prototype/Rules.asset"); }
            if (config.surfaceShader == null)
            { config.surfaceShader = Shader.Find("Universal Render Pipeline/Lit"); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets(); }
            if (File.Exists(scenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Prototype Session").AddComponent<PrototypeSession>().rules = config;
            EditorSceneManager.SaveScene(scene, scenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath); scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        public static void Build()
        {
            PrototypeVerification.Run();
            CreateScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Prototype/Prototype.unity" },
                locationPathName = "Builds/DigiPrototype.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Prototype build failed: " + report.summary.result);
        }
        public static void Verify()
        {
            PrototypeVerification.Run();
            CreateScene();
            Debug.Log("DIGI_VERIFICATION_PASSED");
        }
    }
}
