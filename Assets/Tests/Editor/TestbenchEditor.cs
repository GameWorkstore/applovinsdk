using System.IO;
using Aws.GameLift.Server;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class TestbenchEditor
{
    [Test]
    public void BuildLinuxGameServer() {
        var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build/Server/");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir,true);
        }

        var location = Path.Combine(dir, "Build/Server/LinuxServer");

        var options = new BuildPlayerOptions
        {
            target = BuildTarget.StandaloneLinux64,
            locationPathName = location,
            options = BuildOptions.EnableHeadlessMode
        };
        BuildPipeline.BuildPlayer(options);
        Assert.AreEqual(true, File.Exists(location));
    }

    [Test]
    public void GameLiftIsIncluded()
    {
        Assert.AreEqual("4.0.2",GameLiftServerAPI.GetSdkVersion().Result);
    }
}
