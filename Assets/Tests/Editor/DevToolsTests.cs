using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class DevToolsTests
    {
        bool devToolsWereEnabled;

        [SetUp]
        public void SetUp()
        {
            devToolsWereEnabled = DevTools.Enabled;
        }

        [TearDown]
        public void TearDown()
        {
            DevTools.Enabled = devToolsWereEnabled;
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
    }
}
