using System;
using System.Collections.Generic;
using System.IO;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Tests
{
    // Test-only save destinations: scene integration tests must never read or write a player's collection.
    [SetUpFixture] public sealed class MvpTestIsolation
    {
        readonly List<string> paths=new List<string>();
        [OneTimeSetUp] public void Before(){SceneManager.sceneLoaded+=Loaded;}
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            foreach(var root in scene.GetRootGameObjects())foreach(var mvp in root.GetComponentsInChildren<MvpCollection>(true))
            {var path=Path.Combine(Path.GetTempPath(),"HalfSceneTest-"+Guid.NewGuid()+".json");paths.Add(path);mvp.SavePath=path;}
        }
        [OneTimeTearDown] public void After()
        {
            SceneManager.sceneLoaded-=Loaded;
            foreach(var path in paths)foreach(var suffix in new[]{"",".bak",".tmp",".queue",".queue.bak",".queue.tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
        }
    }
}
