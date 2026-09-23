using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class CloudBuild
{
    // Unity Build Automation pre-export method; never invokes BuildPipeline recursively.
    public static void PreExport()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            throw new InvalidOperationException("This cloud target must be configured for iOS.");
        string bundle = Environment.GetEnvironmentVariable("LG_BUNDLE_ID");
        string number = Environment.GetEnvironmentVariable("BUILD_NUMBER");
        if (string.IsNullOrWhiteSpace(bundle) || !System.Text.RegularExpressions.Regex.IsMatch(bundle, @"^[A-Za-z][A-Za-z0-9-]*(\.[A-Za-z0-9-]+)+$"))
            throw new InvalidOperationException("Set LG_BUNDLE_ID to the registered Apple bundle ID.");
        if (!int.TryParse(number, out int build) || build < 1)
            throw new InvalidOperationException("BUILD_NUMBER must be a positive cloud build number.");
        PrototypeBuild.Configure();
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundle);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.buildNumber = number;
        // This first tablet prototype supports landscape, not iPad split-view layouts.
        PlayerSettings.iOS.requiresFullScreen = true;
        var iconImporter = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/GeorgieIcon.png");
        iconImporter.alphaSource = TextureImporterAlphaSource.None;
        iconImporter.SaveAndReimport();
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        string version = Environment.GetEnvironmentVariable("LG_VERSION");
        if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
        PrototypeBuild.Validate();
        AuctionChecks.Validate();
        Debug.Log("LITTLE_GEORGIES_SETTLEMENT: " + SettlementChecks.Validate() + " checks passed");
        BuildIdentity.Write(false);
        AssetDatabase.SaveAssets();
        Debug.Log("LITTLE_GEORGIES_IOS_PREFLIGHT: passed");
    }
}
