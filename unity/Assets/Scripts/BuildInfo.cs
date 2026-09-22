using System;
using UnityEngine;

namespace LittleGeorgies
{
    [Serializable]
    public sealed class BuildInfo
    {
        public string version, buildNumber, revision, builtUtc, unityVersion, target;
        public bool openEconomy;
        public static BuildInfo Load()
        {
            var asset = Resources.Load<TextAsset>("BuildInfo");
            return asset == null ? new BuildInfo() : JsonUtility.FromJson<BuildInfo>(asset.text);
        }
    }
}
