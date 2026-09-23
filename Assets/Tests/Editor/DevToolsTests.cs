using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class DevToolsTests
    {
        readonly TestObjects objects = new TestObjects();
        bool devToolsWereEnabled;
        string playtestFolder;

        [SetUp]
        public void SetUp()
        {
            devToolsWereEnabled = DevTools.Enabled;
            playtestFolder = Path.Combine(Path.GetTempPath(), "doa-playtest-" + System.Guid.NewGuid().ToString("N"));
            QAManager.DataDirectory = playtestFolder;
        }

        [TearDown]
        public void TearDown()
        {
            DevTools.Enabled = devToolsWereEnabled;
            QAManager.DataDirectory = null;
            if (Directory.Exists(playtestFolder))
                Directory.Delete(playtestFolder, true);
            objects.DestroyAll();
        }

        [Test]
        public void GetKeyDown_WhenDevToolsAreOff_IgnoresEveryKey()
        {
            DevTools.Enabled = false;

            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
                Assert.IsFalse(DevTools.GetKeyDown(key), key.ToString());
        }

        [Test]
        public void GameplayScripts_ReadDebugKeysOnlyThroughDevTools()
        {
            string[] offenders = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/OUTDATED/") && Path.GetFileName(f) != "DevTools.cs")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"Input\.GetKeyDown\(\s*KeyCode\."))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void QAManager_RecordsPlaytestDataInItsDataDirectory()
        {
            DevTools.Enabled = true;
            QAManager qa = objects.Add<QAManager>();

            Reflect.Invoke(qa, "SendData");

            Assert.IsTrue(File.Exists(Path.Combine(playtestFolder, "QAData.csv")));
        }

        [Test]
        public void QAManager_WhenDevToolsAreOff_RecordsNothing()
        {
            DevTools.Enabled = false;
            QAManager qa = objects.Add<QAManager>();

            Reflect.Invoke(qa, "SendData");

            Assert.IsFalse(Directory.Exists(playtestFolder));
        }

        [Test]
        public void StreamingAssets_ShipNoPlaytestData()
        {
            Assert.IsFalse(File.Exists("Assets/StreamingAssets/QAData.csv"), "QAData.csv would ship inside the build");
            Assert.IsFalse(Directory.Exists("Assets/StreamingAssets/HeatMaps"), "HeatMaps would ship inside the build");
        }
    }
}
