using System;
using System.IO;
using LittleGeorgies;
using UnityEditor;
using UnityEngine;

public static class BuildIdentity
{
    public const string PathInProject = "Assets/Resources/BuildInfo.json";
    public static void Write(bool openEconomy)
    {
        var info = new BuildInfo { version = PlayerSettings.bundleVersion,
            buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "local",
            revision = Environment.GetEnvironmentVariable("GIT_COMMIT")
                ?? Environment.GetEnvironmentVariable("BUILD_REVISION") ?? "local-uncommitted",
            builtUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
            target = EditorUserBuildSettings.activeBuildTarget.ToString(), openEconomy = openEconomy };
        Directory.CreateDirectory("Assets/Resources");
        File.WriteAllText(PathInProject, JsonUtility.ToJson(info, true));
        AssetDatabase.ImportAsset(PathInProject);
        Debug.Log("LITTLE_GEORGIES_BUILD_INFO: " + JsonUtility.ToJson(info));
    }
}
