using System;
using System.IO;
using System.Linq;
using LittleGeorgies;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrototypeBuild
{
    [MenuItem("Little Georgies/Validate economy")]
    public static void Validate() => Debug.Log("LITTLE_GEORGIES_ECONOMY: " + SocietyChecks.Validate() + " checks passed");

    static void Require(bool value, string explanation) { if (!value) throw new Exception(explanation); }

    [MenuItem("Little Georgies/Build Windows prototype")]
    public static void Build()
    {
        Configure();
        Validate();
        AuctionChecks.Validate();
        Debug.Log("LITTLE_GEORGIES_SETTLEMENT: " + SettlementChecks.Validate() + " checks passed");
        var args = Environment.GetCommandLineArgs();
        int outputIndex = Array.IndexOf(args, "-lg-build-output");
        string output = outputIndex >= 0 ? args[outputIndex + 1] : "Builds/Windows/LittleGeorgies.exe";
        var buildRoot = Path.GetFullPath("Builds") + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(output).StartsWith(buildRoot, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Build output must be inside the project Builds folder.");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildIdentity.Write(false);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/Settlement.unity" },
            locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        if (result.summary.result != BuildResult.Succeeded) throw new Exception("Windows build: " + result.summary.result);
        File.Copy("AUCTION.md", Path.Combine(Path.GetDirectoryName(output), "AUCTION.md"), true);
        File.Copy(BuildIdentity.PathInProject, Path.Combine(Path.GetDirectoryName(output), "build-info.json"), true);
        Debug.Log("LITTLE_GEORGIES_BUILD: " + result.summary.outputPath);
    }

    public static void Configure()
    {
        AuctionPluginImport.ConfigureAll();
        PlayerSettings.companyName = "Georgist.org";
        PlayerSettings.productName = "Little Georgies";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        // The bootstrap creates all UI at runtime, so no scene material otherwise keeps this shader.
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
        var uiShader = Shader.Find("UI/Default");
        Require(uiShader != null, "UGUI shader is available");
        bool included = false;
        for (int i = 0; i < shaders.arraySize; i++)
            if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == uiShader) included = true;
        if (!included) { shaders.InsertArrayElementAtIndex(shaders.arraySize); shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = uiShader; }
        graphics.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        foreach (string path in new[] { "Assets/Resources/Art/Village.png", "Assets/Resources/Art/GeorgieSheet.png", "Assets/Resources/Art/OrchardField.png", "Assets/Resources/Art/BuildingSheet.png", "Assets/Resources/Art/GeorgieIcon.png" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/GeorgieIcon.png");
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        int iconSizes = PlayerSettings.GetIconSizes(UnityEditor.Build.NamedBuildTarget.Standalone, IconKind.Any).Length;
        if (iconSizes > 0) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Standalone, Enumerable.Repeat(icon, iconSizes).ToArray(), IconKind.Any);
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Little Georgies").AddComponent<VillageGame>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Settlement.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Settlement.unity", true) };
    }
}
