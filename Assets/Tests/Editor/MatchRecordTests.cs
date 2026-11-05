using System.Globalization;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>What a recorded match's file says (MatchRecord): only recording when asked, in numbers any language can read back</summary>
    public class MatchRecordTests
    {
        [Test]
        public void Wanted_WhenAskedFor_OrPlayingInTheEditor()
        {
            Assert.IsTrue(MatchRecord.Wanted(new[] { "game.exe", "-RECORDMATCHES" }, false, false));
            Assert.IsFalse(MatchRecord.Wanted(new[] { "game.exe" }, false, false), "players' builds don't record");
            Assert.IsFalse(MatchRecord.Wanted(null, false, false));
            Assert.IsTrue(MatchRecord.Wanted(null, true, false), "playing in the editor");
            Assert.IsFalse(MatchRecord.Wanted(null, true, true), "the editor's batch runs don't");
        }

        [Test]
        public void Numbers_UseADot_WhateverTheLanguage()
        {
            CultureInfo was = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                Assert.AreEqual("1.5", MatchRecord.N(1.5f));
                StringAssert.StartsWith("e,2.25,fell,3", MatchRecord.Event(2.25f, "fell", 3));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = was;
            }
        }

        [Test]
        public void Sample_HasEveryColumn_InOrder()
        {
            string line = MatchRecord.Sample(1f, 2, true, new Vector3(3f, 4f, 5f), new Vector3(6f, 0f, 7f), 8f, 0.5f, 1f, 0f, true, false, true, 90, 1, 55);

            Assert.AreEqual("s,1,2,1,3,4,5,6,7,8,0.5,1,0,1,0,1,90,1,55", line);
        }
    }
}
