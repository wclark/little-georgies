using System.IO;
using UnityEditor;

public sealed class AuctionPluginImport : AssetPostprocessor
{
    void OnPreprocessAsset()
    {
        if (assetPath.StartsWith("Assets/Plugins/AuctionSolver/") && assetImporter is PluginImporter plugin)
            Configure(plugin);
    }

    public static void ConfigureAll()
    {
        if (!Directory.Exists("Assets/Plugins/AuctionSolver")) return;
        foreach (string path in Directory.GetFiles("Assets/Plugins/AuctionSolver", "*.dll", SearchOption.AllDirectories))
        {
            var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as PluginImporter;
            if (importer == null) continue;
            Configure(importer);
            importer.SaveAndReimport();
        }
    }

    static void Configure(PluginImporter plugin)
    {
        plugin.SetCompatibleWithAnyPlatform(false);
        plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
        plugin.SetCompatibleWithPlatform(BuildTarget.iOS, false);
        plugin.SetCompatibleWithEditor(true);
        plugin.SetEditorData("OS", "Windows");
        plugin.SetEditorData("CPU", plugin.isNativePlugin ? "x86_64" : "AnyCPU");
    }
}
